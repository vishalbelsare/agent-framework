// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using OpenAI.Responses;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Internal;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

internal class ResponsesService
{
    public Task<Response> CreateModelResponseAsync(CreateResponse createResponse)
    {
        throw new NotImplementedException();
    }

    public Task<Response> GetModelResponseAsync(string responseId, string? includeObfuscation, string? startingAfter, bool stream)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteModelResponseAsync(string responseId)
    {
        throw new NotImplementedException();
    }

    public Task<Response> CancelResponseAsync(string responseId)
    {
        throw new NotImplementedException();
    }

    public Task<IList<ResponseItem>> ListInputItemsAsync(string responseId, string? after, IList<IncludeParameter>? include, int? limit = 20, string? order = "desc")
    {
        throw new NotImplementedException();
    }
}

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
