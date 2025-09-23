// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using OpenAIResponse = Microsoft.Extensions.AI.Agents.Hosting.Responses.Model.OpenAIResponse;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Mapping;

internal static class OpenAIResponseMapper
{
    public static IEnumerable<ChatMessage> ToChatMessages(this IList<ResponseInputItem> inputItems)
        => inputItems.Select(item => item.ToChatMessage());

    public static ChatMessage ToChatMessage(this ResponseInputItem inputItem)
    {
        // super easy case for now 
        return new ChatMessage(ChatRole.User, inputItem.Content);
    }

    public static OpenAIResponse ToOpenAIResponse(
        this AgentRunResponse agentRunResponse,
        AgentThread? thread = null)
    {
        var conversation = thread?.ConversationId is not null ? new Conversation { Id = thread.ConversationId } : new Conversation { Id = Guid.NewGuid().ToString() };

        var output = agentRunResponse.Messages.Select(msg => new MessageOutput
        {
            Id = msg.MessageId ?? Guid.NewGuid().ToString(),
            Status = "completed", // todo
            Content = msg.Contents.OfType<TextContent>().Select(textContent => new MessageContent
            {
                Text = textContent.Text
            }).ToList(),
        }).ToList<ResponseOutputItem>();

        return new()
        {
            Id = agentRunResponse.ResponseId ?? Guid.NewGuid().ToString(),
            Background = false /* TODO */,
            Conversation = conversation,
            CreatedAt = agentRunResponse.CreatedAt is not null ? agentRunResponse.CreatedAt.Value.ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Output = output
        };
    }
}
