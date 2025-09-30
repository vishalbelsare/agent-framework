// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model.Contents;
using Response = Microsoft.Extensions.AI.Agents.Hosting.Responses.Model.Response;
using Microsoft.Agents.AI.Hosting.Responses.Internal;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.Hosting.Responses.Mapping;

internal static class OpenAIResponseMapper
{
    public static IEnumerable<ChatMessage> ToChatMessages(this ResponseInputItem? responseInput)
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

                var aiContents = item.Content.Select(x => new TextContent(x.Text)).ToList<AIContent>();
                return new ChatMessage(role, aiContents);
            });
        }

        return [];
    }

    public static Response ToOpenAIResponse(
        this AgentRunResponse agentRunResponse,
        string agentName,
        AgentThread thread,
        OpenAIResponsesRunOptions options)
    {
        // load conversation from the thread?
        Conversation conversation = null!; // thread.ConversationId is not null ? new Conversation { Id = thread.ConversationId } : null;

        var output = agentRunResponse.Messages.Select(msg => new MessageOutput
        {
            Id = msg.MessageId ?? Guid.NewGuid().ToString(),
            Status = "completed", // todo
            Content = msg.Contents.OfType<TextContent>().Select(textContent => new MessageContent
            {
                Type = "output_text",
                Text = textContent.Text
            }).ToList(),
        }).ToList<ResponseOutputItem>();

        var responseId = agentRunResponse.ResponseId ?? $"resp_{Guid.NewGuid():N}";

        return new()
        {
            Id = responseId,
            Background = options.Background,
            Conversation = conversation,
            CreatedAt = agentRunResponse.CreatedAt is not null ? agentRunResponse.CreatedAt.Value.ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Output = output
        };
    }

    public static Response ToOpenAIResponse(
#pragma warning disable RCS1175 // Unused 'this' parameter
        this ActorResponseHandle responseHandle,
#pragma warning restore RCS1175 // Unused 'this' parameter
        AgentRunResponseUpdate update,
        AgentProxyThread thread,
        OpenAIResponsesRunOptions options)
    {
        var conversation = thread.ConversationId is not null ? new Conversation { Id = thread.ConversationId } : null;

        // for simplicity only works with text content
        var output = new MessageOutput
        {
            Id = "todo",
            Status = "completed",
            Content = update.Contents.OfType<TextContent>().Select(textContent => new MessageContent
            {
                Type = "output_text",
                Text = textContent.Text
            }).ToList()
        };

        var responseId = update.ResponseId ?? $"resp_{Guid.NewGuid():N}";

        return new()
        {
            Id = responseId,
            Background = options.Background,
            Conversation = conversation,
            CreatedAt = update.CreatedAt is not null ? update.CreatedAt.Value.ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Output = [output]
        };
    }
}
