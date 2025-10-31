﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows.Declarative.Extensions;
using Microsoft.Agents.AI.Workflows.Declarative.Kit;
using Microsoft.Agents.AI.Workflows.Declarative.PowerFx;
using Microsoft.Bot.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.Agents.AI.Workflows.Declarative.Interpreter;

internal abstract class DeclarativeActionExecutor<TAction>(TAction model, WorkflowFormulaState state) :
    DeclarativeActionExecutor(model, state)
    where TAction : DialogAction
{
    public new TAction Model => (TAction)base.Model;
}

internal abstract class DeclarativeActionExecutor : Executor<ActionExecutorResult>, IResettableExecutor, IModeledAction
{
    private string? _parentId;
    private readonly WorkflowFormulaState _state;

    protected DeclarativeActionExecutor(DialogAction model, WorkflowFormulaState state)
        : base(model.Id.Value)
    {
        if (!model.HasRequiredProperties)
        {
            throw new DeclarativeModelException($"Missing required properties for element: {model.GetId()} ({model.GetType().Name}).");
        }

        this._state = state;

        this.Model = model;
    }

    public DialogAction Model { get; }

    public string ParentId => this._parentId ??= this.Model.GetParentId() ?? WorkflowActionVisitor.Steps.Root();

    public RecalcEngine Engine => this._state.Engine;

    public WorkflowExpressionEngine Evaluator => this._state.Evaluator;

    internal ILogger Logger { get; set; } = NullLogger<DeclarativeActionExecutor>.Instance;

    protected virtual bool IsDiscreteAction => true;

    protected virtual bool EmitResultEvent => true;

    /// <inheritdoc/>
    public ValueTask ResetAsync()
    {
        return default;
    }

    /// <inheritdoc/>
    public override async ValueTask HandleAsync(ActionExecutorResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (this.Model.Disabled)
        {
            Debug.WriteLine($"DISABLED {this.GetType().Name} [{this.Id}]");
            return;
        }

        await context.RaiseInvocationEventAsync(this.Model, message.ExecutorId, cancellationToken).ConfigureAwait(false);

        try
        {
            object? result = await this.ExecuteAsync(new DeclarativeWorkflowContext(context, this._state), cancellationToken).ConfigureAwait(false);
            Debug.WriteLine($"RESULT #{this.Id} - {result ?? "(null)"}");

            if (this.EmitResultEvent)
            {
                await context.SendResultMessageAsync(this.Id, result, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DeclarativeActionException exception)
        {
            Debug.WriteLine($"ERROR [{this.Id}] {exception.GetType().Name}\n{exception.Message}");
            throw;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"ERROR [{this.Id}] {exception.GetType().Name}\n{exception.Message}");
            throw new DeclarativeActionException($"Unhandled workflow failure - #{this.Id} ({this.Model.GetType().Name})", exception);
        }
        finally
        {
            if (this.IsDiscreteAction)
            {
                await context.RaiseCompletionEventAsync(this.Model, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    protected abstract ValueTask<object?> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restore the state of the executor from a checkpoint.
    /// This must be overridden to restore any state that was saved during checkpointing.
    /// </summary>
    protected override ValueTask OnCheckpointRestoredAsync(IWorkflowContext context, CancellationToken cancellationToken = default) =>
        this._state.RestoreAsync(context, cancellationToken);

    protected async ValueTask AssignAsync(PropertyPath? targetPath, FormulaValue result, IWorkflowContext context)
    {
        if (targetPath is null)
        {
            return;
        }

        await context.QueueStateUpdateAsync(targetPath, result).ConfigureAwait(false);

#if DEBUG
        string? resultValue = result.Format();
        string valuePosition = (resultValue?.IndexOf('\n') ?? -1) >= 0 ? Environment.NewLine : " ";
        Debug.WriteLine(
            $"""
            STATE: {this.GetType().Name} [{this.Id}]
             NAME: {targetPath}
            VALUE:{valuePosition}{resultValue} ({result.GetType().Name})
            """);
#endif
    }

    protected DeclarativeActionException Exception(string text, Exception? exception = null)
    {
        string message = $"Unexpected workflow failure during {this.Model.GetType().Name} [{this.Id}]: {text}";
        return exception is null ? new(message) : new(message, exception);
    }
}
