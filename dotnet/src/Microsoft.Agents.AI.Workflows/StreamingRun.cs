﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Agents.AI.Workflows.Execution;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Agents.AI.Workflows;

/// <summary>
/// A <see cref="Workflow"/> run instance supporting a streaming form of receiving workflow events, and providing
/// a mechanism to send responses back to the workflow.
/// </summary>
public sealed class StreamingRun : IAsyncDisposable
{
    private readonly AsyncRunHandle _runHandle;

    internal StreamingRun(AsyncRunHandle runHandle)
    {
        this._runHandle = Throw.IfNull(runHandle);
    }

    /// <summary>
    /// A unique identifier for the run. Can be provided at the start of the run, or auto-generated.
    /// </summary>
    public string RunId => this._runHandle.RunId;

    /// <summary>
    /// Gets the current execution status of the workflow run.
    /// </summary>
    public ValueTask<RunStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => this._runHandle.GetStatusAsync(cancellationToken);

    /// <summary>
    /// Asynchronously sends the specified response to the external system and signals completion of the current
    /// response wait operation.
    /// </summary>
    /// <remarks>The response will be queued for processing for the next superstep.</remarks>
    /// <param name="response">The <see cref="ExternalResponse"/> to send. Must not be <c>null</c>.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous send operation.</returns>
    public ValueTask SendResponseAsync(ExternalResponse response)
        => this._runHandle.EnqueueResponseAsync(response);

    /// <summary>
    /// Attempts to send the specified message asynchronously and returns a value indicating whether the operation was
    /// successful.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message to send. Must be compatible with the expected message types for
    /// the starting executor, or receiving port.</typeparam>
    /// <param name="message">The message instance to send. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask{Boolean}"/> that represents the asynchronous send operation. It's
    /// <see cref="ValueTask{Boolean}.Result"/> is <see langword="true"/> if the message was sent
    /// successfully; otherwise, <see langword="false"/>.</returns>
    public ValueTask<bool> TrySendMessageAsync<TMessage>(TMessage message)
        => this._runHandle.EnqueueMessageAsync(message);

    internal ValueTask<bool> TrySendMessageUntypedAsync(object message, Type? declaredType = null)
        => this._runHandle.EnqueueMessageUntypedAsync(message, declaredType);

    /// <summary>
    /// Asynchronously streams workflow events as they occur during workflow execution.
    /// </summary>
    /// <remarks>This method yields <see cref="WorkflowEvent"/> instances in real time as the workflow
    /// progresses. The stream completes when a <see cref="RequestHaltEvent"/> is encountered. Events are
    /// delivered in the order they are raised.</remarks>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the streaming operation. If cancellation is
    /// requested, the stream will end and no further events will be yielded, but this will not cancel the workflow execution.</param>
    /// <returns>An asynchronous stream of <see cref="WorkflowEvent"/> objects representing significant workflow state changes.
    /// The stream ends when the workflow completes or when cancellation is requested.</returns>
    public IAsyncEnumerable<WorkflowEvent> WatchStreamAsync(
        CancellationToken cancellationToken = default)
        => this.WatchStreamAsync(blockOnPendingRequest: true, cancellationToken);

    internal IAsyncEnumerable<WorkflowEvent> WatchStreamAsync(
        bool blockOnPendingRequest,
        CancellationToken cancellationToken = default)
        => this._runHandle.TakeEventStreamAsync(blockOnPendingRequest, cancellationToken);

    /// <summary>
    /// Attempt to cancel the streaming run.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous send operation.</returns>
    public ValueTask CancelRunAsync() => this._runHandle.CancelRunAsync();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => this._runHandle.DisposeAsync();
}

/// <summary>
/// Provides extension methods for processing and executing workflows using streaming runs.
/// </summary>
public static class StreamingRunExtensions
{
    /// <summary>
    /// Processes all events from the workflow execution stream until completion.
    /// </summary>
    /// <remarks>This method continuously monitors the workflow execution stream provided by <paramref
    /// name="handle"/> and invokes the  <paramref name="eventCallback"/> for each event. If the callback returns a
    /// non-<see langword="null"/> response, the response  is sent back to the workflow using the handle.</remarks>
    /// <param name="handle">The <see cref="StreamingRun"/> representing the workflow execution stream to monitor.</param>
    /// <param name="eventCallback">An optional callback function invoked for each <see cref="WorkflowEvent"/> received from the stream.
    /// The callback can return a response object to be sent back to the workflow, or <see langword="null"/> if no response
    /// is required.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation. The task completes when the workflow
    /// execution stream is fully processed.</returns>
    public static async ValueTask RunToCompletionAsync(this StreamingRun handle, Func<WorkflowEvent, ExternalResponse?>? eventCallback = null, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(handle);

        await foreach (WorkflowEvent @event in handle.WatchStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            ExternalResponse? maybeResponse = eventCallback?.Invoke(@event);
            if (maybeResponse is not null)
            {
                await handle.SendResponseAsync(maybeResponse).ConfigureAwait(false);
            }
        }
    }
}
