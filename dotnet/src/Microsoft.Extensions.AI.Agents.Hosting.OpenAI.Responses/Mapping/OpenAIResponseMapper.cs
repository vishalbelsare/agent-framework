// Copyright (c) Microsoft. All rights reserved.

using OpenAIResponse = Microsoft.Extensions.AI.Agents.Hosting.Responses.Model.OpenAIResponse;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Mapping;

internal static class OpenAIResponseMapper
{
    public static ChatMessage To(this AgentRunResponse agentRunResponse)
    {

    }

    public static OpenAIResponse ToOpenAIResponse(this AgentRunResponse agentRunResponse)
    {
        return new()
        {
            Id = agentRunResponse.ResponseId,

        };
    }
}
