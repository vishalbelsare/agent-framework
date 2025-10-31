﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Agents.AI.Workflows.Execution;
using Microsoft.Agents.AI.Workflows.Observability;
using Microsoft.Agents.AI.Workflows.Specialized;
using Microsoft.Extensions.Logging;
using Microsoft.Shared.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Microsoft.Agents.AI.Workflows.InProc;

internal sealed class InProcessRunnerContext : IRunnerContext
{
    private int _runEnded;
    private readonly string _runId;
    private readonly Workflow _workflow;

    private readonly EdgeMap _edgeMap;
    private readonly OutputFilter _outputFilter;

    private StepContext _nextStep = new();

    private readonly ConcurrentDictionary<string, Task<Executor>> _executors = new();
    private readonly ConcurrentQueue<Func<ValueTask>> _queuedExternalDeliveries = new();
    private readonly ConcurrentDictionary<string, ISuperStepRunner> _joinedSubworkflowRunners = new();

    private readonly ConcurrentDictionary<string, ExternalRequest> _externalRequests = new();

    public InProcessRunnerContext(
        Workflow workflow,
        string runId,
        bool withCheckpointing,
        IEventSink outgoingEvents,
        IStepTracer? stepTracer,
        object? existingOwnershipSignoff = null,
        bool subworkflow = false,
        bool enableConcurrentRuns = false,
        ILogger? logger = null)
    {
        if (enableConcurrentRuns)
        {
            workflow.CheckOwnership(existingOwnershipSignoff: existingOwnershipSignoff);
        }
        else
        {
            workflow.TakeOwnership(this, existingOwnershipSignoff: existingOwnershipSignoff);
        }
        this._workflow = workflow;
        this._runId = runId;

        this._edgeMap = new(this, this._workflow, stepTracer);
        this._outputFilter = new(workflow);

        this.WithCheckpointing = withCheckpointing;
        this.ConcurrentRunsEnabled = enableConcurrentRuns;
        this.OutgoingEvents = outgoingEvents;
    }

    public async ValueTask<Executor> EnsureExecutorAsync(string executorId, IStepTracer? tracer, CancellationToken cancellationToken = default)
    {
        this.CheckEnded();
        Task<Executor> executorTask = this._executors.GetOrAdd(executorId, CreateExecutorAsync);

        async Task<Executor> CreateExecutorAsync(string id)
        {
            if (!this._workflow.Registrations.TryGetValue(executorId, out var registration))
            {
                throw new InvalidOperationException($"Executor with ID '{executorId}' is not registered.");
            }

            Executor executor = await registration.CreateInstanceAsync(this._runId).ConfigureAwait(false);
            await executor.InitializeAsync(this.Bind(executorId), cancellationToken: cancellationToken)
                          .ConfigureAwait(false);

            tracer?.TraceActivated(executorId);

            if (executor is RequestInfoExecutor requestInputExecutor)
            {
                requestInputExecutor.AttachRequestSink(this);
            }

            if (executor is WorkflowHostExecutor workflowHostExecutor)
            {
                await workflowHostExecutor.AttachSuperStepContextAsync(this).ConfigureAwait(false);
            }

            return executor;
        }

        return await executorTask.ConfigureAwait(false);
    }

    public async ValueTask<IEnumerable<Type>> GetStartingExecutorInputTypesAsync(CancellationToken cancellationToken = default)
    {
        Executor startingExecutor = await this.EnsureExecutorAsync(this._workflow.StartExecutorId, tracer: null, cancellationToken)
                                              .ConfigureAwait(false);

        return startingExecutor.InputTypes;
    }

    public ValueTask AddExternalMessageAsync(object message, Type declaredType)
    {
        this.CheckEnded();
        Throw.IfNull(message);

        this._queuedExternalDeliveries.Enqueue(PrepareExternalDeliveryAsync);
        return default;

        async ValueTask PrepareExternalDeliveryAsync()
        {
            DeliveryMapping? maybeMapping =
                await this._edgeMap.PrepareDeliveryForInputAsync(new(message, ExecutorIdentity.None, declaredType))
                                   .ConfigureAwait(false);

            maybeMapping?.MapInto(this._nextStep);
        }
    }

    public ValueTask AddExternalResponseAsync(ExternalResponse response)
    {
        this.CheckEnded();
        Throw.IfNull(response);

        this._queuedExternalDeliveries.Enqueue(PrepareExternalDeliveryAsync);
        return default;

        async ValueTask PrepareExternalDeliveryAsync()
        {
            if (!this.CompleteRequest(response.RequestId))
            {
                throw new InvalidOperationException($"No pending request with ID {response.RequestId} found in the workflow context.");
            }

            DeliveryMapping? maybeMapping =
                await this._edgeMap.PrepareDeliveryForResponseAsync(response)
                                   .ConfigureAwait(false);

            maybeMapping?.MapInto(this._nextStep);
        }
    }

    public bool HasQueuedExternalDeliveries => !this._queuedExternalDeliveries.IsEmpty;
    public bool JoinedRunnersHaveActions => this._joinedSubworkflowRunners.Values.Any(runner => runner.HasUnprocessedMessages);

    public bool NextStepHasActions => this._nextStep.HasMessages ||
                                      this.HasQueuedExternalDeliveries ||
                                      this.JoinedRunnersHaveActions;
    public bool HasUnservicedRequests => !this._externalRequests.IsEmpty ||
                                         this._joinedSubworkflowRunners.Values.Any(runner => runner.HasUnservicedRequests);

    public async ValueTask<StepContext> AdvanceAsync(CancellationToken cancellationToken = default)
    {
        this.CheckEnded();

        while (this._queuedExternalDeliveries.TryDequeue(out var deliveryPrep))
        {
            // It's important we do not try to run these in parallel, because they make be modifying
            // inner edge state, etc.
            await deliveryPrep().ConfigureAwait(false);
        }

        return Interlocked.Exchange(ref this._nextStep, new StepContext());
    }

    public ValueTask AddEventAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
    {
        this.CheckEnded();
        return this.OutgoingEvents.EnqueueAsync(workflowEvent);
    }

    private static readonly string s_namespace = typeof(IWorkflowContext).Namespace!;
    private static readonly ActivitySource s_activitySource = new(s_namespace);

    public async ValueTask SendMessageAsync(string sourceId, object message, string? targetId = null, CancellationToken cancellationToken = default)
    {
        using Activity? activity = s_activitySource.StartActivity(ActivityNames.MessageSend, ActivityKind.Producer);
        // Create a carrier for trace context propagation
        var traceContext = activity is null ? null : new Dictionary<string, string>();
        if (traceContext is not null)
        {
            // Inject the current activity context into the carrier
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(activity?.Context ?? default, Baggage.Current),
                traceContext,
                (carrier, key, value) => carrier[key] = value);
        }

        this.CheckEnded();
        MessageEnvelope envelope = new(message, sourceId, targetId: targetId, traceContext: traceContext);

        if (this._workflow.Edges.TryGetValue(sourceId, out HashSet<Edge>? edges))
        {
            foreach (Edge edge in edges)
            {
                DeliveryMapping? maybeMapping =
                    await this._edgeMap.PrepareDeliveryForEdgeAsync(edge, envelope)
                                       .ConfigureAwait(false);

                maybeMapping?.MapInto(this._nextStep);
            }
        }
    }

    public IWorkflowContext Bind(string executorId, Dictionary<string, string>? traceContext = null)
    {
        this.CheckEnded();
        return new BoundContext(this, executorId, this._outputFilter, traceContext);
    }

    public ValueTask PostAsync(ExternalRequest request)
    {
        this.CheckEnded();
        if (!this._externalRequests.TryAdd(request.RequestId, request))
        {
            throw new ArgumentException($"Pending request with id '{request.RequestId}' already exists.");
        }

        return this.AddEventAsync(new RequestInfoEvent(request));
    }

    public bool CompleteRequest(string requestId)
    {
        this.CheckEnded();
        return this._externalRequests.TryRemove(requestId, out _);
    }

    private IEventSink OutgoingEvents { get; }

    internal StateManager StateManager { get; } = new();

    private sealed class BoundContext(
        InProcessRunnerContext RunnerContext,
        string ExecutorId,
        OutputFilter outputFilter,
        Dictionary<string, string>? traceContext) : IWorkflowContext
    {
        public ValueTask AddEventAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default) => RunnerContext.AddEventAsync(workflowEvent, cancellationToken);

        public ValueTask SendMessageAsync(object message, string? targetId = null, CancellationToken cancellationToken = default)
        {
            return RunnerContext.SendMessageAsync(ExecutorId, message, targetId, cancellationToken);
        }

        public async ValueTask YieldOutputAsync(object output, CancellationToken cancellationToken = default)
        {
            RunnerContext.CheckEnded();
            Throw.IfNull(output);

            Executor sourceExecutor = await RunnerContext.EnsureExecutorAsync(ExecutorId, tracer: null, cancellationToken).ConfigureAwait(false);
            if (!sourceExecutor.CanOutput(output.GetType()))
            {
                throw new InvalidOperationException($"Cannot output object of type {output.GetType().Name}. Expecting one of [{string.Join(", ", sourceExecutor.OutputTypes)}].");
            }

            if (outputFilter.CanOutput(ExecutorId, output))
            {
                await this.AddEventAsync(new WorkflowOutputEvent(output, ExecutorId), cancellationToken).ConfigureAwait(false);
            }
        }

        public ValueTask RequestHaltAsync() => this.AddEventAsync(new RequestHaltEvent());

        public ValueTask<T?> ReadStateAsync<T>(string key, string? scopeName = null, CancellationToken cancellationToken = default)
            => RunnerContext.StateManager.ReadStateAsync<T>(ExecutorId, scopeName, key);

        [return: NotNull]
        public ValueTask<T> ReadOrInitStateAsync<T>(string key, Func<T> initialStateFactory, string? scopeName = null, CancellationToken cancellationToken = default)
            => RunnerContext.StateManager.ReadOrInitStateAsync(ExecutorId, scopeName, key, initialStateFactory);

        public ValueTask<HashSet<string>> ReadStateKeysAsync(string? scopeName = null, CancellationToken cancellationToken = default)
            => RunnerContext.StateManager.ReadKeysAsync(ExecutorId, scopeName);

        public ValueTask QueueStateUpdateAsync<T>(string key, T? value, string? scopeName = null, CancellationToken cancellationToken = default)
            => RunnerContext.StateManager.WriteStateAsync(ExecutorId, scopeName, key, value);

        public ValueTask QueueClearScopeAsync(string? scopeName = null, CancellationToken cancellationToken = default)
            => RunnerContext.StateManager.ClearStateAsync(ExecutorId, scopeName);

        public IReadOnlyDictionary<string, string>? TraceContext => traceContext;

        public bool ConcurrentRunsEnabled => RunnerContext.ConcurrentRunsEnabled;
    }

    public bool WithCheckpointing { get; }
    public bool ConcurrentRunsEnabled { get; }

    internal Task PrepareForCheckpointAsync(CancellationToken cancellationToken = default)
    {
        this.CheckEnded();

        return Task.WhenAll(this._executors.Values.Select(InvokeCheckpointingAsync));

        async Task InvokeCheckpointingAsync(Task<Executor> executorTask)
        {
            Executor executor = await executorTask.ConfigureAwait(false);
            await executor.OnCheckpointingAsync(this.Bind(executor.Id), cancellationToken).ConfigureAwait(false);
        }
    }

    internal Task NotifyCheckpointLoadedAsync(CancellationToken cancellationToken = default)
    {
        this.CheckEnded();

        return Task.WhenAll(this._executors.Values.Select(InvokeCheckpointRestoredAsync));

        async Task InvokeCheckpointRestoredAsync(Task<Executor> executorTask)
        {
            Executor executor = await executorTask.ConfigureAwait(false);
            await executor.OnCheckpointRestoredAsync(this.Bind(executor.Id), cancellationToken).ConfigureAwait(false);
        }
    }

    internal ValueTask<RunnerStateData> ExportStateAsync()
    {
        this.CheckEnded();

        Dictionary<string, List<PortableMessageEnvelope>> queuedMessages = this._nextStep.ExportMessages();
        RunnerStateData result = new(instantiatedExecutors: [.. this._executors.Keys],
                                     queuedMessages,
                                     outstandingRequests: [.. this._externalRequests.Values]);

        return new(result);
    }

    internal async ValueTask RepublishUnservicedRequestsAsync(CancellationToken cancellationToken = default)
    {
        this.CheckEnded();

        if (this.HasUnservicedRequests)
        {
            foreach (string requestId in this._externalRequests.Keys)
            {
                await this.AddEventAsync(new RequestInfoEvent(this._externalRequests[requestId]), cancellationToken)
                          .ConfigureAwait(false);
            }
        }
    }

    internal async ValueTask ImportStateAsync(Checkpoint checkpoint)
    {
        this.CheckEnded();

        RunnerStateData importedState = checkpoint.RunnerData;

        Task<Executor>[] executorTasks = importedState.InstantiatedExecutors
                                                      .Where(id => !this._executors.ContainsKey(id))
                                                      .Select(id => this.EnsureExecutorAsync(id, tracer: null).AsTask())
                                                      .ToArray();

        this._nextStep = new StepContext();
        this._nextStep.ImportMessages(importedState.QueuedMessages);

        this._externalRequests.Clear();

        foreach (ExternalRequest request in importedState.OutstandingRequests)
        {
            // TODO: Reduce the amount of data we need to store in the checkpoint by not storing the entire request object.
            // For example, the Port object is not needed - we should be able to reconstruct it from the ID and the workflow
            // definition.
            this._externalRequests[request.RequestId] = request;
        }

        await Task.WhenAll(executorTasks).ConfigureAwait(false);
    }

    [SuppressMessage("Maintainability", "CA1513:Use ObjectDisposedException throw helper",
        Justification = "Does not exist in NetFx 4.7.2")]
    internal void CheckEnded()
    {
        if (Volatile.Read(ref this._runEnded) == 1)
        {
            throw new InvalidOperationException($"Workflow run '{this._runId}' has been ended. Please start a new Run or StreamingRun.");
        }
    }

    public async ValueTask EndRunAsync()
    {
        if (Interlocked.Exchange(ref this._runEnded, 1) == 0)
        {
            foreach (string executorId in this._executors.Keys)
            {
                Task<Executor> executor = this._executors[executorId];
                if (executor is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (executor is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            if (!this.ConcurrentRunsEnabled)
            {
                await this._workflow.ReleaseOwnershipAsync(this).ConfigureAwait(false);
            }
        }
    }

    public IEnumerable<ISuperStepRunner> JoinedSubworkflowRunners => this._joinedSubworkflowRunners.Values;

    public ValueTask<string> AttachSuperstepAsync(ISuperStepRunner superStepRunner, CancellationToken cancellationToken = default)
    {
        // This needs to be a thread-safe ordered collection because we can potentially instantiate executors
        // in parallel, which means multiple sub-workflows could be attaching at the same time.
        string joinId;
        do
        {
            joinId = Guid.NewGuid().ToString("N");
        } while (!this._joinedSubworkflowRunners.TryAdd(joinId, superStepRunner));

        return default;
    }

    public ValueTask<bool> DetachSuperstepAsync(string joinId) => new(this._joinedSubworkflowRunners.TryRemove(joinId, out _));

    ValueTask ISuperStepJoinContext.ForwardWorkflowEventAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken)
        => this.AddEventAsync(workflowEvent, cancellationToken);

    ValueTask ISuperStepJoinContext.SendMessageAsync<TMessage>(string senderId, [DisallowNull] TMessage message, CancellationToken cancellationToken)
        => this.SendMessageAsync(senderId, Throw.IfNull(message), cancellationToken: cancellationToken);
}
