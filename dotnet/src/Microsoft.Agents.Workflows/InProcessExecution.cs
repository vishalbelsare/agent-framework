// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Workflows.Checkpointing;
using Microsoft.Agents.Workflows.Execution;
using Microsoft.Agents.Workflows.InProc;

namespace Microsoft.Agents.Workflows;

/// <summary>
/// Provides methods to initiate and manage in-process workflow executions, supporting both streaming and
/// non-streaming modes with asynchronous operations.
/// </summary>
public sealed class InProcessExecution
{
    private static InProcessExecution DefaultInstance { get; } = new(ExecutionMode.Lockstep);

    private readonly ExecutionMode _executionMode;
    private InProcessExecution(ExecutionMode mode)
    {
        this._executionMode = mode;
    }

    internal static ExecutionMode DefaultMode => DefaultInstance._executionMode;

    internal ValueTask<AsyncRunHandle> BeginRunAsync(Workflow workflow, ICheckpointManager? checkpointManager, string? runId, IEnumerable<Type> knownValidInputTypes, CancellationToken cancellation)
    {
        InProcessRunner runner = new(workflow, checkpointManager, runId, knownValidInputTypes: knownValidInputTypes);
        return runner.BeginStreamAsync(this._executionMode, cancellation);
    }

    internal ValueTask<AsyncRunHandle> ResumeRunAsync(Workflow workflow, ICheckpointManager? checkpointManager, string? runId, CheckpointInfo fromCheckpoint, IEnumerable<Type> knownValidInputTypes, CancellationToken cancellation)
    {
        InProcessRunner runner = new(workflow, checkpointManager, runId, knownValidInputTypes: knownValidInputTypes);
        return runner.ResumeStreamAsync(this._executionMode, fromCheckpoint, cancellation);
    }

    /// <summary>
    /// Initiates an asynchronous streaming execution using the specified input.
    /// </summary>
    /// <remarks>The returned <see cref="StreamingRun"/> provides methods to observe and control
    /// the ongoing streaming execution. The operation will continue until the streaming execution is finished or
    /// cancelled.</remarks>
    /// <typeparam name="TInput">A type of input accepted by the workflow. Must be non-nullable.</typeparam>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="input">The input message to be processed as part of the streaming run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{StreamingRun}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="StreamingRun"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<StreamingRun> StreamAsync<TInput>(
        Workflow workflow,
        TInput input,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.BeginRunAsync(workflow, checkpointManager: null, runId: runId, [], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.EnqueueAndStreamAsync(input, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates an asynchronous streaming execution using the specified input.
    /// </summary>
    /// <remarks>The returned <see cref="StreamingRun"/> provides methods to observe and control
    /// the ongoing streaming execution. The operation will continue until the streaming execution is finished or
    /// cancelled.</remarks>
    /// <typeparam name="TInput">A type of input accepted by the workflow. Must be non-nullable.</typeparam>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="input">The input message to be processed as part of the streaming run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{StreamingRun}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="StreamingRun"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<StreamingRun> StreamAsync<TInput>(
        Workflow<TInput> workflow,
        TInput input,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.BeginRunAsync(workflow, checkpointManager: null, runId: runId, [typeof(TInput)], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.EnqueueAndStreamAsync(input, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates an asynchronous streaming execution using the specified input, with checkpointing.
    /// </summary>
    /// <remarks>The returned <see cref="StreamingRun"/> provides methods to observe and control
    /// the ongoing streaming execution. The operation will continue until the streaming execution is finished or
    /// cancelled.</remarks>
    /// <typeparam name="TInput">The type of input accepted by the workflow. Must be non-nullable.</typeparam>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="input">The input message to be processed as part of the streaming run.</param>
    /// <param name="checkpointManager">The <see cref="CheckpointManager"/> to use with this run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{StreamingRun}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="StreamingRun"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<Checkpointed<StreamingRun>> StreamAsync<TInput>(
        Workflow workflow,
        TInput input,
        CheckpointManager checkpointManager,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.BeginRunAsync(workflow, checkpointManager, runId: runId, [], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.WithCheckpointingAsync(() => runHandle.EnqueueAndStreamAsync(input, cancellation))
                              .ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates an asynchronous streaming execution using the specified input, with checkpointing.
    /// </summary>
    /// <remarks>The returned <see cref="StreamingRun"/> provides methods to observe and control
    /// the ongoing streaming execution. The operation will continue until the streaming execution is finished or
    /// cancelled.</remarks>
    /// <typeparam name="TInput">The type of input accepted by the workflow. Must be non-nullable.</typeparam>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="input">The input message to be processed as part of the streaming run.</param>
    /// <param name="checkpointManager">The <see cref="CheckpointManager"/> to use with this run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{StreamingRun}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="StreamingRun"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<Checkpointed<StreamingRun>> StreamAsync<TInput>(
        Workflow<TInput> workflow,
        TInput input,
        CheckpointManager checkpointManager,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.BeginRunAsync(workflow, checkpointManager, runId: runId, [typeof(TInput)], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.WithCheckpointingAsync(() => runHandle.EnqueueAndStreamAsync(input, cancellation))
                              .ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes an asynchronous streaming execution for the specified input from a checkpoint.
    /// </summary>
    /// <remarks>If the operation is cancelled via the <paramref name="cancellation"/> token, the streaming execution will
    /// be terminated.</remarks>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="fromCheckpoint">The <see cref="CheckpointInfo"/> corresponding to the checkpoint from which to resume.</param>
    /// <param name="checkpointManager">The <see cref="CheckpointManager"/> to use with this run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="StreamingRun"/> that provides access to the results of the streaming run.</returns>
    public static async ValueTask<Checkpointed<StreamingRun>> ResumeStreamAsync(
        Workflow workflow,
        CheckpointInfo fromCheckpoint,
        CheckpointManager checkpointManager,
        string? runId = null,
        CancellationToken cancellation = default)
    {
        AsyncRunHandle runHandle = await DefaultInstance.ResumeRunAsync(workflow, checkpointManager, runId: runId, fromCheckpoint, [], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.WithCheckpointingAsync<StreamingRun>(() => new(new StreamingRun(runHandle)))
                              .ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes an asynchronous streaming execution for the specified input from a checkpoint.
    /// </summary>
    /// <remarks>If the operation is cancelled via the <paramref name="cancellation"/> token, the streaming execution will
    /// be terminated.</remarks>
    /// <typeparam name="TInput">The type of input accepted by the workflow. Must be non-nullable.</typeparam>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="fromCheckpoint">The <see cref="CheckpointInfo"/> corresponding to the checkpoint from which to resume.</param>
    /// <param name="checkpointManager">The <see cref="CheckpointManager"/> to use with this run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="StreamingRun"/> that provides access to the results of the streaming run.</returns>
    public static async ValueTask<Checkpointed<StreamingRun>> ResumeStreamAsync<TInput>(
        Workflow<TInput> workflow,
        CheckpointInfo fromCheckpoint,
        CheckpointManager checkpointManager,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.ResumeRunAsync(workflow, checkpointManager, runId: runId, fromCheckpoint, [typeof(TInput)], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.WithCheckpointingAsync<StreamingRun>(() => new(new StreamingRun(runHandle)))
                              .ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates a non-streaming execution of the workflow with the specified input.
    /// </summary>
    /// <remarks>The workflow will run until its first halt, and the returned <see cref="Run"/> will capture
    /// all outgoing events. Use the <c>Run</c> instance to resume execution with responses to outgoing events.</remarks>
    /// <typeparam name="TInput">The type of input accepted by the workflow. Must be non-nullable.</typeparam>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="input">The input message to be processed as part of the run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{Run}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="Run"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<Run> RunAsync<TInput>(
        Workflow workflow,
        TInput input,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.BeginRunAsync(workflow, checkpointManager: null, runId: runId, [], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.EnqueueAndRunAsync(input, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates a non-streaming execution of the workflow with the specified input.
    /// </summary>
    /// <remarks>The workflow will run until its first halt, and the returned <see cref="Run"/> will capture
    /// all outgoing events. Use the <c>Run</c> instance to resume execution with responses to outgoing events.</remarks>
    /// <typeparam name="TInput">The type of input accepted by the workflow. Must be non-nullable.</typeparam>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="input">The input message to be processed as part of the run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{Run}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="Run"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<Run> RunAsync<TInput>(
        Workflow<TInput> workflow,
        TInput input,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.BeginRunAsync(workflow, checkpointManager: null, runId: runId, [typeof(TInput)], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.EnqueueAndRunAsync(input, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates a non-streaming execution of the workflow with the specified input, with checkpointing.
    /// </summary>
    /// <remarks>The workflow will run until its first halt, and the returned <see cref="Run"/> will capture
    /// all outgoing events. Use the <c>Run</c> instance to resume execution with responses to outgoing events.</remarks>
    /// <typeparam name="TInput">The type of input accepted by the workflow. Must be non-nullable.</typeparam>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="input">The input message to be processed as part of the run.</param>
    /// <param name="checkpointManager">The <see cref="CheckpointManager"/> to use with this run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{Run}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="Run"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<Checkpointed<Run>> RunAsync<TInput>(
        Workflow workflow,
        TInput input,
        CheckpointManager checkpointManager,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.BeginRunAsync(workflow, checkpointManager, runId: runId, [], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.WithCheckpointingAsync(() => runHandle.EnqueueAndRunAsync(input, cancellation))
                              .ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates a non-streaming execution of the workflow with the specified input, with checkpointing.
    /// </summary>
    /// <remarks>The workflow will run until its first halt, and the returned <see cref="Run"/> will capture
    /// all outgoing events. Use the <c>Run</c> instance to resume execution with responses to outgoing events.</remarks>
    /// <typeparam name="TInput">The type of input accepted by the workflow. Must be non-nullable.</typeparam>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="input">The input message to be processed as part of the run.</param>
    /// <param name="checkpointManager">The <see cref="CheckpointManager"/> to use with this run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{Run}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="Run"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<Checkpointed<Run>> RunAsync<TInput>(
        Workflow<TInput> workflow,
        TInput input,
        CheckpointManager checkpointManager,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.BeginRunAsync(workflow, checkpointManager, runId: runId, [typeof(TInput)], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.WithCheckpointingAsync(() => runHandle.EnqueueAndRunAsync(input, cancellation))
                              .ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes a non-streaming execution of the workflow from a checkpoint.
    /// </summary>
    /// <remarks>The workflow will run until its first halt, and the returned <see cref="Run"/> will capture
    /// all outgoing events. Use the <c>Run</c> instance to resume execution with responses to outgoing events.</remarks>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="fromCheckpoint">The <see cref="CheckpointInfo"/> corresponding to the checkpoint from which to resume.</param>
    /// <param name="checkpointManager">The <see cref="CheckpointManager"/> to use with this run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{Run}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="Run"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<Checkpointed<Run>> ResumeAsync(
        Workflow workflow,
        CheckpointInfo fromCheckpoint,
        CheckpointManager checkpointManager,
        string? runId = null,
        CancellationToken cancellation = default)
    {
        AsyncRunHandle runHandle = await DefaultInstance.ResumeRunAsync(workflow, checkpointManager, runId: runId, fromCheckpoint, [], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.WithCheckpointingAsync<Run>(() => new(new Run(runHandle)))
                              .ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes a non-streaming execution of the workflow from a checkpoint.
    /// </summary>
    /// <remarks>The workflow will run until its first halt, and the returned <see cref="Run"/> will capture
    /// all outgoing events. Use the <c>Run</c> instance to resume execution with responses to outgoing events.</remarks>
    /// <param name="workflow">The workflow to be executed. Must not be <c>null</c>.</param>
    /// <param name="fromCheckpoint">The <see cref="CheckpointInfo"/> corresponding to the checkpoint from which to resume.</param>
    /// <param name="checkpointManager">The <see cref="CheckpointManager"/> to use with this run.</param>
    /// <param name="runId">An optional unique identifier for the run. If not provided, a new identifier will be generated.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation.</param>
    /// <returns>A <see cref="ValueTask{Run}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="Run"/> for managing and interacting with the streaming run.</returns>
    public static async ValueTask<Checkpointed<Run>> ResumeAsync<TInput>(
        Workflow<TInput> workflow,
        CheckpointInfo fromCheckpoint,
        CheckpointManager checkpointManager,
        string? runId = null,
        CancellationToken cancellation = default) where TInput : notnull
    {
        AsyncRunHandle runHandle = await DefaultInstance.ResumeRunAsync(workflow, checkpointManager, runId: runId, fromCheckpoint, [typeof(TInput)], cancellation)
                                                        .ConfigureAwait(false);

        return await runHandle.WithCheckpointingAsync<Run>(() => new(new Run(runHandle)))
                              .ConfigureAwait(false);
    }
}
