// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Internal;
using Response = Microsoft.Extensions.AI.Agents.Hosting.Responses.Model.Response;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Mapping;

internal static class OpenAIResponseMapper
{
    public static IEnumerable<ChatMessage> ToChatMessages(this ResponseInput? responseInput)
    {
        if (responseInput is null)
        {
            return [];
        }
        if (responseInput.IsText)
        {
            return [new ChatMessage(ChatRole.User, responseInput.Text)];
        }
        if (responseInput.IsItemList)
        {
            return responseInput.Items!.Select(item =>
            {
                var role = item.Role.ToChatRole();
                return new ChatMessage(role, item.Content);
            });
        }

        return [];
    }

    public static Response ToOpenAIResponse(
        this AgentRunResponse agentRunResponse,
        AgentThread thread,
        OpenAIResponsesRunOptions options)
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
            Background = options.Background,
            Conversation = conversation,
            CreatedAt = agentRunResponse.CreatedAt is not null ? agentRunResponse.CreatedAt.Value.ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Output = output
        };
    }

    public static Response ToOpenAIResponse(
        this AgentRunResponseUpdate agentRunResponseUpdate,
        AgentThread thread,
        OpenAIResponsesRunOptions options)
    {
        throw new NotImplementedException();
    }
}
