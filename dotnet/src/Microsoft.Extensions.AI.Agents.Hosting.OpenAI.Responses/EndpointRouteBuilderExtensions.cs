// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Internal;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using Microsoft.Extensions.DependencyInjection;

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
        [StringSyntax("Route")] string? responsesPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(agentName, nameof(agentName));

        responsesPath ??= $"/{agentName}/v1";
        var routeGroup = endpoints.MapGroup(responsesPath);

        var aiAgent = endpoints.ServiceProvider.GetRequiredKeyedService<AIAgent>(agentName);
        MapGetResponse(routeGroup, aiAgent);
    }

    private static void MapGetResponse(IEndpointRouteBuilder routeGroup, AIAgent agent)
    {
        var responsesService = routeGroup.ServiceProvider.GetRequiredService<ResponsesService>();

        routeGroup.MapPost("/", ([FromBody] CreateResponse createResponse) => responsesService.CreateModelResponseAsync(createResponse))
            .WithName("CreateModelResponse");

        routeGroup.MapGet("/{responseId}", (string responseId,
            [FromQuery(Name = "include_obfuscation")] string? includeObfuscation,
            [FromQuery(Name = "starting_after")] string? startingAfter,
            [FromQuery(Name = "stream")] bool stream = false) =>
                responsesService.GetModelResponseAsync(responseId, includeObfuscation, startingAfter, stream)
            )
            .WithName("GetModelResponse");

        routeGroup.MapDelete("/{responseId}", (string responseId) => responsesService.DeleteModelResponseAsync(responseId))
            .WithName("DeleteResponse");

        routeGroup.MapPost("/{responseId}/cancel", (string responseId) => responsesService.CancelResponseAsync(responseId))
            .WithName("CancelResponse");

        routeGroup.MapGet("/{responseId}/input-items", (string responseId,
            [FromQuery(Name = "after")] string? after,
            [FromQuery(Name = "include")] IncludeParameter[]? include,
            [FromQuery(Name = "limit")] int? limit = 20,
            [FromQuery(Name = "order")] string? order = "desc") =>
                responsesService.ListInputItemsAsync(responseId, after, include, limit, order)
            )
            .WithName("ListInputItems");
    }
}
