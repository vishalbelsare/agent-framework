// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public static void MapOpenAIResponses(
        this IEndpointRouteBuilder endpoints,
        string agentName,
        [StringSyntax("Route")] string? responsesPath = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(agentName, nameof(agentName));

        responsesPath ??= $"/{agentName}/v1/responses";
        var routeGroup = endpoints.MapGroup(responsesPath);

        var aiAgent = endpoints.ServiceProvider.GetRequiredKeyedService<AIAgent>(agentName);
        MapGetResponse(routeGroup, aiAgent);
    }

    private static void MapGetResponse(IEndpointRouteBuilder routeGroup, AIAgent agent)
    {
        var loggerFactory = routeGroup.ServiceProvider.GetService<ILoggerFactory>();
        var actorClient = routeGroup.ServiceProvider.GetRequiredService<IActorClient>();

        var agentName = agent.Name ?? throw new ArgumentException("The specified agent must have a valid name to map OpenAI Responses endpoints.", nameof(agent));
        var agentProxy = new AgentProxy(agent.Name, actorClient);
        var agentResponsesProcessor = new AIAgentResponsesProcessor(agentProxy, loggerFactory);

        routeGroup.MapPost("/", (CreateResponse createResponse, CancellationToken cancellationToken)
            => agentResponsesProcessor.CreateModelResponseAsync(createResponse, cancellationToken)
        ).WithName(agentName + "/CreateResponse");

        routeGroup.MapGet("/{responseId}", async (string responseId,
            CancellationToken cancellationToken,
            [FromQuery(Name = "include_obfuscation")] string? includeObfuscation,
            [FromQuery(Name = "starting_after")] string? startingAfter,
            [FromQuery(Name = "stream")] bool stream = false) =>
        {
            var response = await agentResponsesProcessor.GetModelResponseAsync(responseId, includeObfuscation, startingAfter, stream, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        }).WithName(agentName + "/GetModelResponse");

        routeGroup.MapDelete("/{responseId}", async (string responseId, CancellationToken cancellationToken) =>
        {
            var deleted = await agentResponsesProcessor.DeleteModelResponseAsync(responseId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new DeleteModelResponse(deleted));
        }).WithName(agentName + "/DeleteResponse");

        routeGroup.MapPost("/{responseId}/cancel", async (string responseId, CancellationToken cancellationToken) =>
        {
            var response = await agentResponsesProcessor.CancelResponseAsync(responseId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        }).WithName(agentName + "/CancelResponse");

        routeGroup.MapGet("/{responseId}/input-items", (string responseId,
            CancellationToken cancellationToken,
            [FromQuery(Name = "after")] string? after,
            [FromQuery(Name = "include")] IncludeParameter[]? include,
            [FromQuery(Name = "limit")] int? limit = 20,
            [FromQuery(Name = "order")] string? order = "desc") =>
                agentResponsesProcessor.ListInputItemsAsync(responseId, after, include, limit, order, cancellationToken)
            )
            .WithName(agentName + "/ListInputItems");
    }
}
