// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Workflows.Execution;

internal class OffThreadRunEventStream : IRunEventStream
{
    private class OffThreadHaltSignal(int epoch) : WorkflowEvent
    {
        public int Epoch => epoch;
    }

    public ValueTask<RunStatus> GetStatusAsync(CancellationToken cancellation = default) => new(this.RunStatus);

    public RunStatus RunStatus { get; private set; } = RunStatus.NotStarted;

    private readonly IInputCoordinator _inputCoordinator;
    private readonly AsyncCoordinator _outputCoordinator = new();

    private readonly CancellationTokenSource _endRunSource = new();
    private readonly InitLocked<Task> _runLoopTask = new();
    private readonly InitLocked<Task> _disposeTask = new();

    private int _isTaken;
    private int _streamEpoch = -1;
    private bool _lastEpochWasEmpty;

    private readonly ConcurrentQueue<WorkflowEvent> _eventSink = new();

    public OffThreadRunEventStream(ISuperStepRunner stepRunner, IInputCoordinator inputCoordinator)
    {
        this.StepRunner = stepRunner;

        this._inputCoordinator = inputCoordinator;
    }

    private ISuperStepRunner StepRunner { get; }

    public void Start()
    {
        Console.WriteLine("Starting OffThreadRunEventStream run loop.");
        this._runLoopTask.Init(() => Task.Run(() => this.RunLoopAsync(), this._endRunSource.Token));
    }

    public async IAsyncEnumerable<WorkflowEvent> TakeEventStreamAsync([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        using CancellationTokenRegistration registration = cancellation.Register(this._endRunSource.Cancel);

        try
        {
            if (Interlocked.Exchange(ref this._isTaken, 1) != 0)
            {
                throw new InvalidOperationException("Can only have one active watcher on the event stream at a time.");
            }

            int newEpoch;
            if (!this._lastEpochWasEmpty)
            {
                newEpoch = Interlocked.Increment(ref this._streamEpoch);
                Console.WriteLine($"Taking event stream for epoch {newEpoch}.");
                this._lastEpochWasEmpty = true;
            }
            else
            {
                newEpoch = Volatile.Read(ref this._streamEpoch);
                Console.WriteLine($"Taking event stream for existing epoch {newEpoch} (last epoch was empty).");
            }

            if (newEpoch > 50000)
            {
                throw new InvalidOperationException("BAD!");
            }

            while (!cancellation.IsCancellationRequested)
            {
                if (this._eventSink.TryDequeue(out WorkflowEvent? @event) &&
                    !cancellation.IsCancellationRequested)
                {
                    Console.WriteLine($"Got event: {@event.GetType()}");
                    this._lastEpochWasEmpty = false;

                    if (@event is OffThreadHaltSignal haltSignal)
                    {
                        if (haltSignal.Epoch == newEpoch)
                        {
                            Console.WriteLine($"Received halt signal for epoch {newEpoch}, stopping stream.");
                            // We hit a halt signal for our current epoch, so we are done.
                            yield break;
                        }

                        // We hit a halt signal for a previous epoch, so we ignore it.
                        Console.WriteLine($"Ignoring halt signal for epoch {haltSignal.Epoch}, current epoch is {newEpoch}.");
                    }
                    else
                    {
                        yield return @event;
                    }
                }
                else
                {
                    Console.WriteLine($"No events available, waiting for output coordination (cancel requested: {cancellation.IsCancellationRequested}).");
                    await this._outputCoordinator.WaitForCoordinationAsync(cancellation).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            Volatile.Write(ref this._isTaken, 0);
        }
    }

    private void NotifyHalt()
    {
        int epoch = Math.Max(Volatile.Read(ref this._streamEpoch), 0);
        Console.WriteLine($"Notifying halt in epoch= {epoch}");
        this._eventSink.Enqueue(new OffThreadHaltSignal(epoch));
    }

    private async Task RunLoopAsync(CancellationToken cancellation = default)
    {
        this.StepRunner.OutgoingEvents.EventRaised += InspectAndForwardWorkflowEventAsync;
        bool hadRequestHaltEvent = false;

        try
        {
            while (!cancellation.IsCancellationRequested && !hadRequestHaltEvent)
            {
                this.RunStatus = RunStatus.Running;

                do
                {
                    Console.WriteLine($"Trying to run superstep in epoch = {Volatile.Read(ref this._streamEpoch)}");
                    bool hadActions = await this.StepRunner.RunSuperStepAsync(cancellation).ConfigureAwait(false);
                    Console.WriteLine($"Completed superstep with hadActions={hadActions}");
                } while (this.StepRunner.HasUnprocessedMessages &&
                         !hadRequestHaltEvent &&
                         !cancellation.IsCancellationRequested);

                this.RunStatus = this.StepRunner.HasUnservicedRequests ? RunStatus.PendingRequests : RunStatus.Idle;
                Console.WriteLine($"Drained all messages with status= {this.RunStatus}");

                this.NotifyHalt();
                this._outputCoordinator.MarkCoordinationPoint();

                Console.WriteLine("Waiting for input");
                await this._inputCoordinator.WaitForNextInputAsync(cancellation).ConfigureAwait(false);
                Console.WriteLine("Got input");
            }
        }
        finally
        {
            this.StepRunner.OutgoingEvents.EventRaised -= InspectAndForwardWorkflowEventAsync;
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Internal event handler - nowhere to return exceptions to.")]
        async ValueTask InspectAndForwardWorkflowEventAsync(object? sender, WorkflowEvent e)
        {
            if (e is RequestHaltEvent)
            {
                Console.WriteLine("Saw RequestHaltEvent, setting halt flag and notifying.");
                hadRequestHaltEvent = true;
                this.NotifyHalt();
            }
            else
            {
                Console.WriteLine($"Forwarding event {e.GetType()}");
                this._eventSink.Enqueue(e);
            }

            Console.WriteLine("Releasing output.");
            this._outputCoordinator.MarkCoordinationPoint();
            Console.Out.Flush();
        }
    }

    private async Task DisposeCoreAsync()
    {
        this._endRunSource.Cancel();
        this._endRunSource.Dispose();

        this.NotifyHalt();
        this._outputCoordinator.MarkCoordinationPoint();

        try
        {
            // Wait for the cancellation to propagate
            Task? loopTask = this._runLoopTask.Get();

            if (loopTask != null)
            {
                await loopTask.ConfigureAwait(false);
            }
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        this._disposeTask.Init(this.DisposeCoreAsync);
        await this._disposeTask.Get()!.ConfigureAwait(false);
    }
}
