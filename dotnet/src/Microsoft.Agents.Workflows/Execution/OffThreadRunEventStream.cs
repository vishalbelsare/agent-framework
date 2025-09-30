// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Agents.Workflows.Execution;

internal class OffThreadRunEventStream : IRunEventStream
{
    public ValueTask<RunStatus> GetStatusAsync(CancellationToken cancellation = default) => new(this.RunStatus);

    public RunStatus RunStatus { get; private set; } = RunStatus.NotStarted;

    private readonly CancellationTokenSource _endRunSource = new();
    private readonly Task _runLoopTask;
    private readonly InitLocked<Task> _disposeTask = new();

    public OffThreadRunEventStream(ISuperStepRunner stepRunner)
    {
        this.StepRunner = stepRunner;
        this._runLoopTask = Task.Run(() => this.RunLoopAsync(), this._endRunSource.Token);
    }

    private ISuperStepRunner StepRunner { get; }

    private Channel<WorkflowEvent> EventChannel { get; } = Channel.CreateUnbounded<WorkflowEvent>();

    public async IAsyncEnumerable<WorkflowEvent> WatchStreamAsync([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        using CancellationTokenRegistration registration = cancellation.Register(this._endRunSource.Cancel);

        await foreach (var @event in this.EventChannel.Reader.ReadAllAsync(cancellation).ConfigureAwait(false))
        {
            yield return @event;
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellation = default)
    {
        this.RunStatus = RunStatus.Running;
        this.StepRunner.WorkflowEvent += InspectAndForwardWorkflowEvent;
        bool hadRequestHaltEvent = false;

        try
        {
            do
            {
                await this.StepRunner.RunSuperStepAsync(cancellation).ConfigureAwait(false);
            } while (this.StepRunner.HasUnprocessedMessages &&
                     !hadRequestHaltEvent &&
                     !cancellation.IsCancellationRequested);
        }
        finally
        {
            this.StepRunner.WorkflowEvent -= InspectAndForwardWorkflowEvent;
            this.RunStatus = this.StepRunner.HasUnservicedRequests ? RunStatus.PendingRequests : RunStatus.Idle;
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Internal event handler - nowhere to return exceptions to.")]
        async void InspectAndForwardWorkflowEvent(object? sender, WorkflowEvent e)
        {
            try
            {
                if (e is RequestHaltEvent)
                {
                    hadRequestHaltEvent = true;
                }
                else
                {
                    _ = this.EventChannel.Writer.WriteAsync(e, cancellation).AsTask();
                }
            }
            catch
            {
            }
        }
    }

    private async Task DisposeCoreAsync()
    {
        this._endRunSource.Cancel();
        this._endRunSource.Dispose();

        try
        {
            this.EventChannel.Writer.Complete();
        }
        catch { }

        try
        {
            // Wait for the cancellation to propagate
            await this._runLoopTask.ConfigureAwait(false);
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        this._disposeTask.Init(this.DisposeCoreAsync);
        await this._disposeTask.Get()!.ConfigureAwait(false);
    }
}
