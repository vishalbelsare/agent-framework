// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Internal;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using Microsoft.Extensions.AI.Agents.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses;

/// <summary>
/// Provides extension methods for mapping OpenAI Responses capabilities to an <see cref="AIAgent"/>.
/// </summary>
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps OpenAI Responses endpoints to the specified <see cref="IEndpointRouteBuilder"/> for the given <see cref="AIAgent"/>.
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="agentName"></param>
    /// <param name="responsesPath"></param>
    /// <param name="conversationsPath"></param>
    public static void MapOpenAIResponses(
        this IEndpointRouteBuilder endpoints,
        string agentName,
        [StringSyntax("Route")] string? responsesPath = null,
        [StringSyntax("Route")] string? conversationsPath = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(agentName, nameof(agentName));

        var agent = endpoints.ServiceProvider.GetRequiredKeyedService<AIAgent>(agentName);
        ArgumentNullException.ThrowIfNull(agent.Name, nameof(agent.Name));

        var loggerFactory = endpoints.ServiceProvider.GetService<ILoggerFactory>();
        var actorClient = endpoints.ServiceProvider.GetRequiredService<IActorClient>();
        var agentProxy = new AgentProxy(agent.Name, actorClient);

        responsesPath ??= $"/{agentName}/v1/responses";
        var responsesRouteGroup = endpoints.MapGroup(responsesPath);
        MapResponses(responsesRouteGroup, agentProxy, loggerFactory);

        conversationsPath ??= $"/{agentName}/v1/conversations";
        var conversationsRouteGroup = endpoints.MapGroup(conversationsPath);
        MapConversations(conversationsRouteGroup, agentProxy, loggerFactory);
    }

    private static void MapResponses(IEndpointRouteBuilder routeGroup, AgentProxy agentProxy, ILoggerFactory? loggerFactory)
    {
        var agentName = agentProxy.Name;
        var responsesProcessor = new AIAgentResponsesProcessor(agentProxy, loggerFactory);

        routeGroup.MapPost("/", (CreateResponse createResponse, CancellationToken cancellationToken)
            => responsesProcessor.CreateModelResponseAsync(createResponse, cancellationToken)
        ).WithName(agentName + "/CreateResponse");

        // Endpoints below are related to the response management.
        // Currently, there is no way to persistently track the actual response
        // (we can do on conversation / message level).
        // --
        // will be added after actor model rework.

        //routeGroup.MapGet("/{responseId}", (string responseId,
        //    CancellationToken cancellationToken,
        //    [FromQuery(Name = "include_obfuscation")] string? includeObfuscation,
        //    [FromQuery(Name = "starting_after")] string? startingAfter,
        //    [FromQuery(Name = "stream")] bool stream = false)
        //    => agentResponsesProcessor.GetModelResponseAsync(responseId, includeObfuscation, startingAfter, stream, cancellationToken).ConfigureAwait(false)
        //).WithName(agentName + "/GetModelResponse");

        //routeGroup.MapDelete("/{responseId}", async (string responseId, CancellationToken cancellationToken) =>
        //{
        //    var deleted = await agentResponsesProcessor.DeleteModelResponseAsync(responseId, cancellationToken).ConfigureAwait(false);
        //    return Results.Ok(new DeleteModelResponse(deleted));
        //}).WithName(agentName + "/DeleteResponse");

        //routeGroup.MapPost("/{responseId}/cancel", async (string responseId, CancellationToken cancellationToken) =>
        //{
        //    var response = await agentResponsesProcessor.CancelResponseAsync(responseId, cancellationToken).ConfigureAwait(false);
        //    return Results.Ok(response);
        //}).WithName(agentName + "/CancelResponse");

        //routeGroup.MapGet("/{responseId}/input-items", (string responseId,
        //    CancellationToken cancellationToken,
        //    [FromQuery(Name = "after")] string? after,
        //    [FromQuery(Name = "include")] IncludeParameter[]? include,
        //    [FromQuery(Name = "limit")] int? limit = 20,
        //    [FromQuery(Name = "order")] string? order = "desc") =>
        //        agentResponsesProcessor.ListInputItemsAsync(responseId, after, include, limit, order, cancellationToken)
        //    )
        //    .WithName(agentName + "/ListInputItems");
    }

    private static void MapConversations(IEndpointRouteBuilder routeGroup, AgentProxy agentProxy, ILoggerFactory? loggerFactory)
    {
        var agentName = agentProxy.Name;
        var conversationsProcessor = new AIAgentConversationsProcessor(agentProxy, loggerFactory);

        routeGroup.MapGet("/{conversation_id}", (string conversationId, CancellationToken cancellationToken)
            => conversationsProcessor.GetConversationAsync(conversationId, cancellationToken)
        ).WithName(agentName + "/RetrieveConversation");
    }
}
