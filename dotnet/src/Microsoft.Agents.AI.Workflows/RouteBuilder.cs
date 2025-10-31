﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows.Execution;
using Microsoft.Shared.Diagnostics;
using CatchAllF =
    System.Func<
        Microsoft.Agents.AI.Workflows.PortableValue, // message
        Microsoft.Agents.AI.Workflows.IWorkflowContext, // context
        System.Threading.CancellationToken, // cancellation
        System.Threading.Tasks.ValueTask<Microsoft.Agents.AI.Workflows.Execution.CallResult>
    >;
using MessageHandlerF =
    System.Func<
        object, // message
        Microsoft.Agents.AI.Workflows.IWorkflowContext, // context
        System.Threading.CancellationToken, // cancellation
        System.Threading.Tasks.ValueTask<Microsoft.Agents.AI.Workflows.Execution.CallResult>
    >;

namespace Microsoft.Agents.AI.Workflows;

/// <summary>
/// Provides a builder for configuring message type handlers for an <see cref="Executor"/>.
/// </summary>
/// <remarks>
/// Override the <see cref="Executor.ConfigureRoutes"/> method to customize the routing of messages to handlers.
/// </remarks>
public class RouteBuilder
{
    private readonly Dictionary<Type, MessageHandlerF> _typedHandlers = [];
    private readonly Dictionary<Type, Type> _outputTypes = [];
    private CatchAllF? _catchAll;

    internal RouteBuilder AddHandlerInternal(Type messageType, MessageHandlerF handler, Type? outputType, bool overwrite = false)
    {
        Throw.IfNull(messageType);
        Throw.IfNull(handler);

        if (messageType == typeof(PortableValue))
        {
            throw new InvalidOperationException("Cannot register a handler for PortableValue. Use AddCatchAll() instead.");
        }

        Debug.Assert(typeof(CallResult) != outputType, "Must not double-wrap message handlers in the RouteBuilder. " +
            "Use AddHandlerInternal() or do not wrap user-provided handler.");

        // Overwrite must be false if the type is not registered. Overwrite must be true if the type is registered.
        if (this._typedHandlers.ContainsKey(messageType) == overwrite)
        {
            this._typedHandlers[messageType] = handler;

            if (outputType is not null)
            {
                this._outputTypes[messageType] = outputType;
            }
            else
            {
                this._outputTypes.Remove(messageType);
            }
        }
        else if (overwrite)
        {
            // overwrite is true, but the type is not registered.
            throw new ArgumentException($"A handler for message type {messageType.FullName} has not yet been registered (overwrite = true).");
        }
        else if (!overwrite)
        {
            throw new ArgumentException($"A handler for message type {messageType.FullName} is already registered (overwrite = false).");
        }

        return this;
    }

    internal RouteBuilder AddHandlerUntyped(Type type, Func<object, IWorkflowContext, CancellationToken, ValueTask> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(type, WrappedHandlerAsync, outputType: null, overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            await handler.Invoke(message, context, cancellationToken).ConfigureAwait(false);
            return CallResult.ReturnVoid();
        }
    }

    internal RouteBuilder AddHandlerUntyped<TResult>(Type type, Func<object, IWorkflowContext, CancellationToken, ValueTask<TResult>> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(type, WrappedHandlerAsync, outputType: typeof(TResult), overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            TResult result = await handler.Invoke(message, context, cancellationToken).ConfigureAwait(false);
            return CallResult.ReturnResult(result);
        }
    }

    /// <summary>
    /// Registers a handler for messages of the specified input type in the workflow route.
    /// </summary>
    /// <remarks>If a handler for the specified input type already exists and <paramref name="overwrite"/> is
    /// <see langword="false"/>, the existing handler will not be replaced. Handlers are invoked asynchronously and are
    /// expected to complete their processing before the workflow continues.</remarks>
    /// <typeparam name="TInput"></typeparam>
    /// <param name="handler">A delegate that processes messages of type <typeparamref name="TInput"/> within the workflow context. The
    /// delegate is invoked for each incoming message of the specified type.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the specified input type; otherwise, <see
    /// langword="false"/> to preserve the existing handler.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of additional handlers or route
    /// options.</returns>
    public RouteBuilder AddHandler<TInput>(Action<TInput, IWorkflowContext, CancellationToken> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(typeof(TInput), WrappedHandlerAsync, outputType: null, overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            handler.Invoke((TInput)message, context, cancellationToken);
            return CallResult.ReturnVoid();
        }
    }

    /// <summary>
    /// Registers a handler for messages of the specified input type in the workflow route.
    /// </summary>
    /// <remarks>If a handler for the specified input type already exists and <paramref name="overwrite"/> is
    /// <see langword="false"/>, the existing handler will not be replaced. Handlers are invoked asynchronously and are
    /// expected to complete their processing before the workflow continues.</remarks>
    /// <typeparam name="TInput"></typeparam>
    /// <param name="handler">A delegate that processes messages of type <typeparamref name="TInput"/> within the workflow context. The
    /// delegate is invoked for each incoming message of the specified type.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the specified input type; otherwise, <see
    /// langword="false"/> to preserve the existing handler.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of additional handlers or route
    /// options.</returns>
    public RouteBuilder AddHandler<TInput>(Action<TInput, IWorkflowContext> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(typeof(TInput), WrappedHandlerAsync, outputType: null, overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            handler.Invoke((TInput)message, context);
            return CallResult.ReturnVoid();
        }
    }

    /// <summary>
    /// Registers a handler for messages of the specified input type in the workflow route.
    /// </summary>
    /// <remarks>If a handler for the specified input type already exists and <paramref name="overwrite"/> is
    /// <see langword="false"/>, the existing handler will not be replaced. Handlers are invoked asynchronously and are
    /// expected to complete their processing before the workflow continues.</remarks>
    /// <typeparam name="TInput"></typeparam>
    /// <param name="handler">A delegate that processes messages of type <typeparamref name="TInput"/> within the workflow context. The
    /// delegate is invoked for each incoming message of the specified type.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the specified input type; otherwise, <see
    /// langword="false"/> to preserve the existing handler.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of additional handlers or route
    /// options.</returns>
    public RouteBuilder AddHandler<TInput>(Func<TInput, IWorkflowContext, CancellationToken, ValueTask> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(typeof(TInput), WrappedHandlerAsync, outputType: null, overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            await handler.Invoke((TInput)message, context, cancellationToken).ConfigureAwait(false);
            return CallResult.ReturnVoid();
        }
    }

    /// <summary>
    /// Registers a handler for messages of the specified input type in the workflow route.
    /// </summary>
    /// <remarks>If a handler for the specified input type already exists and <paramref name="overwrite"/> is
    /// <see langword="false"/>, the existing handler will not be replaced. Handlers are invoked asynchronously and are
    /// expected to complete their processing before the workflow continues.</remarks>
    /// <typeparam name="TInput"></typeparam>
    /// <param name="handler">A delegate that processes messages of type <typeparamref name="TInput"/> within the workflow context. The
    /// delegate is invoked for each incoming message of the specified type.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the specified input type; otherwise, <see
    /// langword="false"/> to preserve the existing handler.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of additional handlers or route
    /// options.</returns>
    public RouteBuilder AddHandler<TInput>(Func<TInput, IWorkflowContext, ValueTask> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(typeof(TInput), WrappedHandlerAsync, outputType: null, overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            await handler.Invoke((TInput)message, context).ConfigureAwait(false);
            return CallResult.ReturnVoid();
        }
    }

    /// <summary>
    /// Registers a handler function for messages of the specified input type in the workflow route.
    /// </summary>
    /// <remarks>If a handler for the given input type already exists, setting <paramref name="overwrite"/> to
    /// <see langword="true"/> will replace the existing handler; otherwise, an exception may be thrown. The handler
    /// receives the input message and workflow context, and returns a result asynchronously.</remarks>
    /// <typeparam name="TInput">The type of input message the handler will process.</typeparam>
    /// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
    /// <param name="handler">A function that processes messages of type <typeparamref name="TInput"/> within the workflow context and returns
    /// a <see cref="ValueTask{TResult}"/> representing the asynchronous result.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddHandler<TInput, TResult>(Func<TInput, IWorkflowContext, CancellationToken, TResult> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(typeof(TInput), WrappedHandlerAsync, outputType: typeof(TResult), overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            TResult result = handler.Invoke((TInput)message, context, cancellationToken);
            return CallResult.ReturnResult(result);
        }
    }

    /// <summary>
    /// Registers a handler function for messages of the specified input type in the workflow route.
    /// </summary>
    /// <remarks>If a handler for the given input type already exists, setting <paramref name="overwrite"/> to
    /// <see langword="true"/> will replace the existing handler; otherwise, an exception may be thrown. The handler
    /// receives the input message and workflow context, and returns a result asynchronously.</remarks>
    /// <typeparam name="TInput">The type of input message the handler will process.</typeparam>
    /// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
    /// <param name="handler">A function that processes messages of type <typeparamref name="TInput"/> within the workflow context and returns
    /// a <see cref="ValueTask{TResult}"/> representing the asynchronous result.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddHandler<TInput, TResult>(Func<TInput, IWorkflowContext, TResult> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(typeof(TInput), WrappedHandlerAsync, outputType: typeof(TResult), overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            TResult result = handler.Invoke((TInput)message, context);
            return CallResult.ReturnResult(result);
        }
    }

    /// <summary>
    /// Registers a handler function for messages of the specified input type in the workflow route.
    /// </summary>
    /// <remarks>If a handler for the given input type already exists, setting <paramref name="overwrite"/> to
    /// <see langword="true"/> will replace the existing handler; otherwise, an exception may be thrown. The handler
    /// receives the input message and workflow context, and returns a result asynchronously.</remarks>
    /// <typeparam name="TInput">The type of input message the handler will process.</typeparam>
    /// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
    /// <param name="handler">A function that processes messages of type <typeparamref name="TInput"/> within the workflow context and returns
    /// a <see cref="ValueTask{TResult}"/> representing the asynchronous result.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddHandler<TInput, TResult>(Func<TInput, IWorkflowContext, CancellationToken, ValueTask<TResult>> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(typeof(TInput), WrappedHandlerAsync, outputType: typeof(TResult), overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            TResult result = await handler.Invoke((TInput)message, context, cancellationToken).ConfigureAwait(false);
            return CallResult.ReturnResult(result);
        }
    }

    /// <summary>
    /// Registers a handler function for messages of the specified input type in the workflow route.
    /// </summary>
    /// <remarks>If a handler for the given input type already exists, setting <paramref name="overwrite"/> to
    /// <see langword="true"/> will replace the existing handler; otherwise, an exception may be thrown. The handler
    /// receives the input message and workflow context, and returns a result asynchronously.</remarks>
    /// <typeparam name="TInput">The type of input message the handler will process.</typeparam>
    /// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
    /// <param name="handler">A function that processes messages of type <typeparamref name="TInput"/> within the workflow context and returns
    /// a <see cref="ValueTask{TResult}"/> representing the asynchronous result.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddHandler<TInput, TResult>(Func<TInput, IWorkflowContext, ValueTask<TResult>> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddHandlerInternal(typeof(TInput), WrappedHandlerAsync, outputType: typeof(TResult), overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(object message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            TResult result = await handler.Invoke((TInput)message, context).ConfigureAwait(false);
            return CallResult.ReturnResult(result);
        }
    }

    private RouteBuilder AddCatchAll(CatchAllF handler, bool overwrite = false)
    {
        if (!overwrite && this._catchAll != null)
        {
            throw new InvalidOperationException("A catch-all is already registered (overwrite = false).");
        }

        this._catchAll = handler;

        return this;
    }

    /// <summary>
    /// Register a handler function as a catch-all handler: It will be used if not type-matching handler is registered.
    /// </summary>
    /// <remarks>If a catch-all handler for already exists, setting <paramref name="overwrite"/> to <see langword="true"/>
    /// will replace the existing handler; otherwise, an exception may be thrown. The handler receives the input message
    /// wrapped as <see cref="PortableValue"/> and workflow context, and returns a result asynchronously.</remarks>
    /// <param name="handler">A function that processes messages wrapped as <see cref="PortableValue"/> within the
    /// workflow context. The delegate is invoked for each incoming message not otherwise handled.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddCatchAll(Func<PortableValue, IWorkflowContext, CancellationToken, ValueTask> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddCatchAll(WrappedHandlerAsync, overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(PortableValue message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            await handler.Invoke(message, context, cancellationToken).ConfigureAwait(false);
            return CallResult.ReturnVoid();
        }
    }

    /// <summary>
    /// Register a handler function as a catch-all handler: It will be used if not type-matching handler is registered.
    /// </summary>
    /// <remarks>If a catch-all handler for already exists, setting <paramref name="overwrite"/> to <see langword="true"/>
    /// will replace the existing handler; otherwise, an exception may be thrown. The handler receives the input message
    /// wrapped as <see cref="PortableValue"/> and workflow context, and returns a result asynchronously.</remarks>
    /// <param name="handler">A function that processes messages wrapped as <see cref="PortableValue"/> within the
    /// workflow context. The delegate is invoked for each incoming message not otherwise handled.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddCatchAll(Func<PortableValue, IWorkflowContext, ValueTask> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddCatchAll(WrappedHandlerAsync, overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(PortableValue message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            await handler.Invoke(message, context).ConfigureAwait(false);
            return CallResult.ReturnVoid();
        }
    }

    /// <summary>
    /// Register a handler function as a catch-all handler: It will be used if not type-matching handler is registered.
    /// </summary>
    /// <remarks>If a catch-all handler for already exists, setting <paramref name="overwrite"/> to <see langword="true"/>
    /// will replace the existing handler; otherwise, an exception may be thrown. The handler receives the input message
    /// wrapped as <see cref="PortableValue"/> and workflow context, and returns a result asynchronously.</remarks>
    /// <param name="handler">A function that processes messages wrapped as <see cref="PortableValue"/> within the
    /// workflow context and returns a <see cref="ValueTask{TResult}"/> representing the asynchronous result.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddCatchAll<TResult>(Func<PortableValue, IWorkflowContext, CancellationToken, ValueTask<TResult>> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddCatchAll(WrappedHandlerAsync, overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(PortableValue message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            TResult result = await handler.Invoke(message, context, cancellationToken).ConfigureAwait(false);
            return CallResult.ReturnResult(result);
        }
    }

    /// <summary>
    /// Register a handler function as a catch-all handler: It will be used if not type-matching handler is registered.
    /// </summary>
    /// <remarks>If a catch-all handler for already exists, setting <paramref name="overwrite"/> to <see langword="true"/>
    /// will replace the existing handler; otherwise, an exception may be thrown. The handler receives the input message
    /// wrapped as <see cref="PortableValue"/> and workflow context, and returns a result asynchronously.</remarks>
    /// <param name="handler">A function that processes messages wrapped as <see cref="PortableValue"/> within the
    /// workflow context and returns a <see cref="ValueTask{TResult}"/> representing the asynchronous result.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddCatchAll<TResult>(Func<PortableValue, IWorkflowContext, ValueTask<TResult>> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddCatchAll(WrappedHandlerAsync, overwrite);

        async ValueTask<CallResult> WrappedHandlerAsync(PortableValue message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            TResult result = await handler.Invoke(message, context).ConfigureAwait(false);
            return CallResult.ReturnResult(result);
        }
    }

    /// <summary>
    /// Register a handler function as a catch-all handler: It will be used if not type-matching handler is registered.
    /// </summary>
    /// <remarks>If a catch-all handler for already exists, setting <paramref name="overwrite"/> to <see langword="true"/>
    /// will replace the existing handler; otherwise, an exception may be thrown. The handler receives the input message
    /// wrapped as <see cref="PortableValue"/> and workflow context, and returns a result asynchronously.</remarks>
    /// <param name="handler">A function that processes messages wrapped as <see cref="PortableValue"/> within the
    /// workflow context. The delegate is invoked for each incoming message not otherwise handled.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddCatchAll(Action<PortableValue, IWorkflowContext, CancellationToken> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddCatchAll(WrappedHandlerAsync, overwrite);

        ValueTask<CallResult> WrappedHandlerAsync(PortableValue message, IWorkflowContext ctx, CancellationToken cancellationToken)
        {
            handler.Invoke(message, ctx, cancellationToken);
            return new(CallResult.ReturnVoid());
        }
    }

    /// <summary>
    /// Register a handler function as a catch-all handler: It will be used if not type-matching handler is registered.
    /// </summary>
    /// <remarks>If a catch-all handler for already exists, setting <paramref name="overwrite"/> to <see langword="true"/>
    /// will replace the existing handler; otherwise, an exception may be thrown. The handler receives the input message
    /// wrapped as <see cref="PortableValue"/> and workflow context, and returns a result asynchronously.</remarks>
    /// <param name="handler">A function that processes messages wrapped as <see cref="PortableValue"/> within the
    /// workflow context. The delegate is invoked for each incoming message not otherwise handled.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddCatchAll(Action<PortableValue, IWorkflowContext> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddCatchAll(WrappedHandlerAsync, overwrite);

        ValueTask<CallResult> WrappedHandlerAsync(PortableValue message, IWorkflowContext ctx, CancellationToken cancellationToken)
        {
            handler.Invoke(message, ctx);
            return new(CallResult.ReturnVoid());
        }
    }

    /// <summary>
    /// Register a handler function as a catch-all handler: It will be used if not type-matching handler is registered.
    /// </summary>
    /// <remarks>If a catch-all handler for already exists, setting <paramref name="overwrite"/> to <see langword="true"/>
    /// will replace the existing handler; otherwise, an exception may be thrown. The handler receives the input message
    /// wrapped as <see cref="PortableValue"/> and workflow context, and returns a result asynchronously.</remarks>
    /// <param name="handler">A function that processes messages wrapped as <see cref="PortableValue"/> within the
    /// workflow context and returns a <see cref="ValueTask{TResult}"/> representing the asynchronous result.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddCatchAll<TResult>(Func<PortableValue, IWorkflowContext, CancellationToken, TResult> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddCatchAll(WrappedHandlerAsync, overwrite);

        ValueTask<CallResult> WrappedHandlerAsync(PortableValue message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            TResult result = handler.Invoke(message, context, cancellationToken);
            return new(CallResult.ReturnResult(result));
        }
    }

    /// <summary>
    /// Register a handler function as a catch-all handler: It will be used if not type-matching handler is registered.
    /// </summary>
    /// <remarks>If a catch-all handler for already exists, setting <paramref name="overwrite"/> to <see langword="true"/>
    /// will replace the existing handler; otherwise, an exception may be thrown. The handler receives the input message
    /// wrapped as <see cref="PortableValue"/> and workflow context, and returns a result asynchronously.</remarks>
    /// <param name="handler">A function that processes messages wrapped as <see cref="PortableValue"/> within the
    /// workflow context and returns a <see cref="ValueTask{TResult}"/> representing the asynchronous result.</param>
    /// <param name="overwrite"><see langword="true"/> to replace any existing handler for the input type; otherwise, <see langword="false"/> to
    /// preserve existing handlers.</param>
    /// <returns>The current <see cref="RouteBuilder"/> instance, enabling fluent configuration of workflow routes.</returns>
    public RouteBuilder AddCatchAll<TResult>(Func<PortableValue, IWorkflowContext, TResult> handler, bool overwrite = false)
    {
        Throw.IfNull(handler);

        return this.AddCatchAll(WrappedHandlerAsync, overwrite);

        ValueTask<CallResult> WrappedHandlerAsync(PortableValue message, IWorkflowContext context, CancellationToken cancellationToken)
        {
            TResult result = handler.Invoke(message, context);
            return new(CallResult.ReturnResult(result));
        }
    }

    internal MessageRouter Build() => new(this._typedHandlers, [.. this._outputTypes.Values], this._catchAll);
}
