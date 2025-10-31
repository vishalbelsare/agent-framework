﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows.Specialized;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Agents.AI.Workflows;

/// <summary>
/// Extension methods for configuring executors and functions as <see cref="ExecutorIsh"/> instances.
/// </summary>
public static class ExecutorIshConfigurationExtensions
{
    /// <summary>
    /// Configures a factory method for creating an <see cref="Executor"/> of type <typeparamref name="TExecutor"/>, using the
    /// type name as the id.
    /// </summary>
    /// <remarks>
    /// Note that Executor Ids must be unique within a workflow.
    ///
    /// Although this will generally result in a delay-instantiated <see cref="Executor"/> once messages are available
    /// for it, it will be instantiated if a <see cref="ProtocolDescriptor"/> for the <see cref="Workflow"/> is requested,
    /// and it is the starting executor.
    /// </remarks>
    /// <typeparam name="TExecutor">The type of the resulting executor</typeparam>
    /// <param name="factoryAsync">The factory method.</param>
    /// <returns>An ExecutorIsh instance that resolves to the result of the factory call when messages get sent to it.</returns>
    public static ExecutorIsh ConfigureFactory<TExecutor>(this Func<string, string, ValueTask<TExecutor>> factoryAsync)
        where TExecutor : Executor
        => ConfigureFactory<TExecutor, ExecutorOptions>((config, runId) => factoryAsync(config.Id, runId), typeof(TExecutor).Name, options: null);

    /// <summary>
    /// Configures a factory method for creating an <see cref="Executor"/> of type <typeparamref name="TExecutor"/>, with
    /// the specified id.
    /// </summary>
    /// <remarks>
    /// Although this will generally result in a delay-instantiated <see cref="Executor"/> once messages are available
    /// for it, it will be instantiated if a <see cref="ProtocolDescriptor"/> for the <see cref="Workflow"/> is requested,
    /// and it is the starting executor.
    /// </remarks>
    /// <typeparam name="TExecutor">The type of the resulting executor</typeparam>
    /// <param name="factoryAsync">The factory method.</param>
    /// <param name="id">An id for the executor to be instantiated.</param>
    /// <returns>An ExecutorIsh instance that resolves to the result of the factory call when messages get sent to it.</returns>
    public static ExecutorIsh ConfigureFactory<TExecutor>(this Func<string, string, ValueTask<TExecutor>> factoryAsync, string id)
        where TExecutor : Executor
        => ConfigureFactory<TExecutor, ExecutorOptions>((_, runId) => factoryAsync(id, runId), id, options: null);

    /// <summary>
    /// Configures a factory method for creating an <see cref="Executor"/> of type <typeparamref name="TExecutor"/>, with
    /// the specified id and options.
    /// </summary>
    /// <remarks>
    /// Although this will generally result in a delay-instantiated <see cref="Executor"/> once messages are available
    /// for it, it will be instantiated if a <see cref="ProtocolDescriptor"/> for the <see cref="Workflow"/> is requested,
    /// and it is the starting executor.
    /// </remarks>
    /// <typeparam name="TExecutor">The type of the resulting executor</typeparam>
    /// <typeparam name="TOptions">The type of options object to be passed to the factory method.</typeparam>
    /// <param name="factoryAsync">The factory method.</param>
    /// <param name="id">An id for the executor to be instantiated.</param>
    /// <param name="options">An optional parameter specifying the options.</param>
    /// <returns>An ExecutorIsh instance that resolves to the result of the factory call when messages get sent to it.</returns>
    public static ExecutorIsh ConfigureFactory<TExecutor, TOptions>(this Func<Config<TOptions>, string, ValueTask<TExecutor>> factoryAsync, string id, TOptions? options = null)
        where TExecutor : Executor
        where TOptions : ExecutorOptions
    {
        Configured<TExecutor, TOptions> configured = new(factoryAsync, id, options);

        return new ExecutorIsh(configured.Super<TExecutor, Executor, TOptions>(), typeof(TExecutor), ExecutorIsh.Type.Executor);
    }

    private static ExecutorIsh ToExecutorIsh<TInput>(this FunctionExecutor<TInput> executor, Delegate raw) => new(Configured.FromInstance(executor, raw: raw)
                                         .Super<FunctionExecutor<TInput>, Executor>(),
                               typeof(FunctionExecutor<TInput>),
                               ExecutorIsh.Type.Function);

    private static ExecutorIsh ToExecutorIsh<TInput, TOutput>(this FunctionExecutor<TInput, TOutput> executor, Delegate raw) => new(Configured.FromInstance(executor, raw: raw)
                                         .Super<FunctionExecutor<TInput, TOutput>, Executor>(),
                               typeof(FunctionExecutor<TInput, TOutput>),
                               ExecutorIsh.Type.Function);

    /// <summary>
    /// Configures a sub-workflow executor for the specified workflow, using the provided identifier and options.
    /// </summary>
    /// <param name="workflow">The workflow instance to be executed as a sub-workflow. Cannot be null.</param>
    /// <param name="id">A unique identifier for the sub-workflow execution. Used to distinguish this sub-workflow instance.</param>
    /// <param name="options">Optional configuration options for the sub-workflow executor. If null, default options are used.</param>
    /// <returns>An ExecutorIsh instance representing the configured sub-workflow executor.</returns>
    public static ExecutorIsh ConfigureSubWorkflow(this Workflow workflow, string id, ExecutorOptions? options = null)
    {
        object ownershipToken = new();
        workflow.TakeOwnership(ownershipToken, subworkflow: true);

        Configured<WorkflowHostExecutor, ExecutorOptions> configured = new(InitHostExecutorAsync, id, options, raw: workflow);
        return new ExecutorIsh(configured.Super<WorkflowHostExecutor, Executor, ExecutorOptions>(), typeof(WorkflowHostExecutor), ExecutorIsh.Type.Workflow);

        ValueTask<WorkflowHostExecutor> InitHostExecutorAsync(Config<ExecutorOptions> config, string runId)
        {
            return new(new WorkflowHostExecutor(config.Id, workflow, runId, ownershipToken, config.Options));
        }
    }

    /// <summary>
    /// Configures a function-based asynchronous message handler as an executor with the specified identifier and
    /// options.
    /// </summary>
    /// <typeparam name="TInput">The type of input message.</typeparam>
    /// <param name="messageHandlerAsync">A delegate that defines the asynchronous function to execute for each input message.</param>
    /// <param name="id">A optional unique identifier for the executor. If <c>null</c>, will use the function argument as an id.</param>
    /// <param name="options">Configuration options for the executor. If <c>null</c>, default options will be used.</param>
    /// <param name="threadsafe">Declare that the message handler may be used simultaneously by multiple runs concurrently.</param>
    /// <returns>An ExecutorIsh instance that wraps the provided asynchronous message handler and configuration.</returns>
    public static ExecutorIsh AsExecutor<TInput>(this Func<TInput, IWorkflowContext, CancellationToken, ValueTask> messageHandlerAsync, string id, ExecutorOptions? options = null, bool threadsafe = false)
        => new FunctionExecutor<TInput>(id, messageHandlerAsync, options, declareCrossRunShareable: threadsafe).ToExecutorIsh(messageHandlerAsync);

    /// <summary>
    /// Configures a function-based asynchronous message handler as an executor with the specified identifier and
    /// options.
    /// </summary>
    /// <typeparam name="TInput">The type of input message.</typeparam>
    /// <typeparam name="TOutput">The type of output message.</typeparam>
    /// <param name="messageHandlerAsync">A delegate that defines the asynchronous function to execute for each input message.</param>
    /// <param name="id">A unique identifier for the executor.</param>
    /// <param name="options">Configuration options for the executor. If <c>null</c>, default options will be used.</param>
    /// <param name="threadsafe">Declare that the message handler may be used simultaneously by multiple runs concurrently.</param>
    /// <returns>An ExecutorIsh instance that wraps the provided asynchronous message handler and configuration.</returns>
    public static ExecutorIsh AsExecutor<TInput, TOutput>(this Func<TInput, IWorkflowContext, CancellationToken, ValueTask<TOutput>> messageHandlerAsync, string id, ExecutorOptions? options = null, bool threadsafe = false)
        => new FunctionExecutor<TInput, TOutput>(Throw.IfNull(id), messageHandlerAsync, options, declareCrossRunShareable: threadsafe).ToExecutorIsh(messageHandlerAsync);

    /// <summary>
    /// Configures a function-based aggregating executor with the specified identifier and options.
    /// </summary>
    /// <typeparam name="TInput">The type of input message.</typeparam>
    /// <typeparam name="TAccumulate">The type of the accumulating object.</typeparam>
    /// <param name="aggregatorFunc">A delegate the defines the aggregation procedure</param>
    /// <param name="id">A unique identifier for the executor.</param>
    /// <param name="options">Configuration options for the executor. If <c>null</c>, default options will be used.</param>
    /// <param name="threadsafe">Declare that the message handler may be used simultaneously by multiple runs concurrently.</param>
    /// <returns>An ExecutorIsh instance that wraps the provided asynchronous message handler and configuration.</returns>
    public static ExecutorIsh AsExecutor<TInput, TAccumulate>(this Func<TAccumulate?, TInput, TAccumulate?> aggregatorFunc, string id, ExecutorOptions? options = null, bool threadsafe = false)
        => new AggregatingExecutor<TInput, TAccumulate>(id, aggregatorFunc, options, declareCrossRunShareable: threadsafe);
}

/// <summary>
/// A tagged union representing an object that can function like an <see cref="Executor"/> in a <see cref="Workflow"/>,
/// or a reference to one by ID.
/// </summary>
public sealed class ExecutorIsh :
    IIdentified,
    IEquatable<ExecutorIsh>,
    IEquatable<IIdentified>,
    IEquatable<string>
{
    /// <summary>
    /// The type of the <see cref="ExecutorIsh"/>.
    /// </summary>
    public enum Type
    {
        /// <summary>
        /// An unbound executor reference, identified only by ID.
        /// </summary>
        Unbound,
        /// <summary>
        /// An actual <see cref="Executor"/> instance.
        /// </summary>
        Executor,
        /// <summary>
        /// A function delegate to be wrapped as an executor.
        /// </summary>
        Function,
        /// <summary>
        /// An <see cref="RequestPort"/> for servicing external requests.
        /// </summary>
        RequestPort,
        /// <summary>
        /// An <see cref="AIAgent"/> instance.
        /// </summary>
        Agent,
        /// <summary>
        /// A nested <see cref="Workflow"/> instance.
        /// </summary>
        Workflow,
    }

    /// <summary>
    /// Gets the type of data contained in this <see cref="ExecutorIsh" /> instance.
    /// </summary>
    public Type ExecutorType { get; init; }

    private readonly string? _idValue;

    private readonly Configured<Executor>? _configuredExecutor;
    private readonly System.Type? _configuredExecutorType;

    internal readonly RequestPort? _requestPortValue;
    private readonly AIAgent? _aiAgentValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorIsh"/> class as an unbound reference by ID.
    /// </summary>
    /// <param name="id">A unique identifier for an <see cref="Executor"/> in the <see cref="Workflow"/></param>
    public ExecutorIsh(string id)
    {
        this.ExecutorType = Type.Unbound;
        this._idValue = Throw.IfNull(id);
    }

    internal ExecutorIsh(Configured<Executor> configured, System.Type configuredExecutorType, Type type)
    {
        this.ExecutorType = type;
        this._configuredExecutor = configured;
        this._configuredExecutorType = configuredExecutorType;
    }

    /// <summary>
    /// Initializes a new instance of the ExecutorIsh class using the specified executor.
    /// </summary>
    /// <param name="executor">The executor instance to be wrapped.</param>
    public ExecutorIsh(Executor executor)
    {
        this.ExecutorType = Type.Executor;
        this._configuredExecutor = Configured.FromInstance(Throw.IfNull(executor));
        this._configuredExecutorType = executor.GetType();
    }

    /// <summary>
    /// Initializes a new instance of the ExecutorIsh class using the specified input port.
    /// </summary>
    /// <param name="port">The input port to associate to be wrapped.</param>
    public ExecutorIsh(RequestPort port)
    {
        this.ExecutorType = Type.RequestPort;
        this._requestPortValue = Throw.IfNull(port);
    }

    /// <summary>
    /// Initializes a new instance of the ExecutorIsh class using the specified AI agent.
    /// </summary>
    /// <param name="aiAgent"></param>
    public ExecutorIsh(AIAgent aiAgent)
    {
        this.ExecutorType = Type.Agent;
        this._aiAgentValue = Throw.IfNull(aiAgent);
    }

    internal bool IsUnbound => this.ExecutorType == Type.Unbound;

    /// <inheritdoc/>
    public string Id => this.ExecutorType switch
    {
        Type.Unbound => this._idValue ?? throw new InvalidOperationException("This ExecutorIsh is unbound and has no ID."),
        Type.Executor => this._configuredExecutor!.Id,
        Type.RequestPort => this._requestPortValue!.Id,
        Type.Agent => this._aiAgentValue!.Id,
        Type.Function => this._configuredExecutor!.Id,
        Type.Workflow => this._configuredExecutor!.Id,
        _ => throw new InvalidOperationException($"Unknown ExecutorIsh type: {this.ExecutorType}")
    };

    internal object? RawData => this.ExecutorType switch
    {
        Type.Unbound => this._idValue,
        Type.Executor => this._configuredExecutor!.Raw ?? this._configuredExecutor,
        Type.RequestPort => this._requestPortValue,
        Type.Agent => this._aiAgentValue,
        Type.Function => this._configuredExecutor!.Raw ?? this._configuredExecutor,
        Type.Workflow => this._configuredExecutor!.Raw ?? this._configuredExecutor,
        _ => throw new InvalidOperationException($"Unknown ExecutorIsh type: {this.ExecutorType}")
    };

    /// <summary>
    /// Gets the registration details for the current executor.
    /// </summary>
    /// <remarks>The returned registration depends on the type of the executor. If the executor is unbound, an
    /// <see cref="InvalidOperationException"/> is thrown. For other executor types, the registration  includes the
    /// appropriate ID, type, and provider based on the executor's configuration.</remarks>
    internal ExecutorRegistration Registration => new(this.Id, this.RuntimeType, this.ExecutorProvider, this.RawData);

    private System.Type RuntimeType => this.ExecutorType switch
    {
        Type.Unbound => throw new InvalidOperationException($"ExecutorIsh with ID '{this.Id}' is unbound."),
        Type.Executor => this._configuredExecutorType!,
        Type.RequestPort => typeof(RequestInfoExecutor),
        Type.Agent => typeof(AIAgentHostExecutor),
        Type.Function => this._configuredExecutorType!,
        Type.Workflow => this._configuredExecutorType!,
        _ => throw new InvalidOperationException($"Unknown ExecutorIsh type: {this.ExecutorType}")
    };

    /// <summary>
    /// Gets an <see cref="Func{Executor}"/> that can be used to obtain an <see cref="Executor"/> instance
    /// corresponding to this <see cref="ExecutorIsh"/>.
    /// </summary>
    private Func<string, ValueTask<Executor>> ExecutorProvider => this.ExecutorType switch
    {
        Type.Unbound => throw new InvalidOperationException($"Executor with ID '{this.Id}' is unbound."),
        Type.Executor => this._configuredExecutor!.BoundFactoryAsync,
        Type.RequestPort => (runId) => new(new RequestInfoExecutor(this._requestPortValue!)),
        Type.Agent => (runId) => new(new AIAgentHostExecutor(this._aiAgentValue!)),
        Type.Function => this._configuredExecutor!.BoundFactoryAsync,
        Type.Workflow => this._configuredExecutor!.BoundFactoryAsync,
        _ => throw new InvalidOperationException($"Unknown ExecutorIsh type: {this.ExecutorType}")
    };

    /// <summary>
    /// Defines an implicit conversion from an <see cref="Executor"/> instance to an <see cref="ExecutorIsh"/> object.
    /// </summary>
    /// <param name="executor">The <see cref="Executor"/> instance to convert to <see cref="ExecutorIsh"/>.</param>
    public static implicit operator ExecutorIsh(Executor executor) => new(executor);

    /// <summary>
    /// Defines an implicit conversion from an <see cref="RequestPort"/> to an <see cref="ExecutorIsh"/> instance.
    /// </summary>
    /// <param name="inputPort">The <see cref="RequestPort"/> to convert to an <see cref="ExecutorIsh"/>.</param>
    public static implicit operator ExecutorIsh(RequestPort inputPort) => new(inputPort);

    /// <summary>
    /// Defines an implicit conversion from an <see cref="AIAgent"/> to an <see cref="ExecutorIsh"/> instance.
    /// </summary>
    /// <param name="aiAgent">The <see cref="AIAgent"/> to convert to an <see cref="ExecutorIsh"/>.</param>
    public static implicit operator ExecutorIsh(AIAgent aiAgent) => new(aiAgent);

    /// <summary>
    /// Defines an implicit conversion from a string to an <see cref="ExecutorIsh"/> instance.
    /// </summary>
    /// <param name="id">The string ID to convert to an <see cref="ExecutorIsh"/>.</param>
    public static implicit operator ExecutorIsh(string id) => new(id);

    /// <inheritdoc/>
    public bool Equals(ExecutorIsh? other) =>
        other is not null && other.Id == this.Id;

    /// <inheritdoc/>
    public bool Equals(IIdentified? other) =>
        other is not null && other.Id == this.Id;

    /// <inheritdoc/>
    public bool Equals(string? other) =>
        other is not null && other == this.Id;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj switch
        {
            null => false,
            ExecutorIsh ish => this.Equals(ish),
            IIdentified identified => this.Equals(identified),
            string str => this.Equals(str),
            _ => false
        };

    /// <inheritdoc/>
    public override int GetHashCode() => this.Id.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => this.ExecutorType switch
    {
        Type.Unbound => $"'{this.Id}':<unbound>",
        Type.Executor => $"'{this.Id}':{this._configuredExecutorType!.Name}",
        Type.RequestPort => $"'{this.Id}':Input({this._requestPortValue!.Request.Name}->{this._requestPortValue!.Response.Name})",
        Type.Agent => $"{this.Id}':AIAgent(@{this._aiAgentValue!.GetType().Name})",
        Type.Function => $"'{this.Id}':{this._configuredExecutorType!.Name}",
        Type.Workflow => $"'{this.Id}':{this._configuredExecutorType!.Name}",
        _ => $"'{this.Id}':<unknown[{this.ExecutorType}]>"
    };
}
