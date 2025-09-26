// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Mapping;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.AI.Agents.Runtime;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Internal;

internal sealed class AIAgentConversationsProcessor
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly ILogger _logger;
#pragma warning restore IDE0052 // Remove unread private members
    private readonly AgentProxy _agentProxy;

    private ActorType AgentType => new(this._agentProxy.Name);

    public AIAgentConversationsProcessor(AgentProxy agentProxy, ILoggerFactory? loggerFactory = null)
    {
        this._logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AIAgentConversationsProcessor>();
        this._agentProxy = agentProxy ?? throw new ArgumentNullException(nameof(agentProxy));
    }

    public async Task<IResult> GetConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        // this should go into underlying IActorStateStorage and load more metadata out of it
        // var agentThread = this._agentProxy.GetThread(conversationId);

        return Results.Ok(new ConversationObject
        {
            Id = conversationId
        });
    }
}
