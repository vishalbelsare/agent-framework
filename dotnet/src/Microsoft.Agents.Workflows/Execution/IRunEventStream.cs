// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Workflows.Execution;

internal interface IRunEventStream : IAsyncDisposable
{
    void Start();
    ValueTask<RunStatus> GetStatusAsync(CancellationToken cancellation = default);
    IAsyncEnumerable<WorkflowEvent> TakeEventStreamAsync(CancellationToken cancellation = default);
}
