// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Workflows.Execution;

internal sealed class LockstepRunEventStream : IRunEventStream
{
    public ValueTask<RunStatus> GetStatusAsync(CancellationToken cancellation = default) => new(this.RunStatus);

    public LockstepRunEventStream(ISuperStepRunner stepRunner)
    {
        this.StepRunner = stepRunner;
    }

    private RunStatus RunStatus { get; set; } = RunStatus.NotStarted;
    private ISuperStepRunner StepRunner { get; }

    public async IAsyncEnumerable<WorkflowEvent> WatchStreamAsync([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        List<WorkflowEvent> eventSink = [];

        this.StepRunner.WorkflowEvent += OnWorkflowEvent;

        try
        {
            this.RunStatus = RunStatus.Running;
            do
            {
                // Drain SuperSteps while there are steps to run
                await this.StepRunner.RunSuperStepAsync(cancellation).ConfigureAwait(false);

                if (cancellation.IsCancellationRequested)
                {
                    yield break; // Exit if cancellation is requested
                }

                bool hadRequestHaltEvent = false;
                foreach (WorkflowEvent raisedEvent in Interlocked.Exchange(ref eventSink, []))
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        yield break; // Exit if cancellation is requested
                    }

                    // TODO: Do we actually want to interpret this as a termination request?
                    if (raisedEvent is RequestHaltEvent)
                    {
                        hadRequestHaltEvent = true;
                    }
                    else
                    {
                        yield return raisedEvent;
                    }
                }

                if (hadRequestHaltEvent)
                {
                    // If we had a completion event, we are done.
                    yield break;
                }
            } while (this.StepRunner.HasUnprocessedMessages &&
                     !cancellation.IsCancellationRequested);
        }
        finally
        {
            this.RunStatus = this.StepRunner.HasUnservicedRequests ? RunStatus.PendingRequests : RunStatus.Idle;
            this.StepRunner.WorkflowEvent -= OnWorkflowEvent;
        }

        void OnWorkflowEvent(object? sender, WorkflowEvent e)
        {
            eventSink.Add(e);
        }
    }

    public ValueTask DisposeAsync() => default;
}
