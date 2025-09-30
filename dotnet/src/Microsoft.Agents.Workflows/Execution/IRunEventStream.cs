// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Workflows.Execution;

internal interface IRunEventStream : IAsyncDisposable
{
    ValueTask<RunStatus> GetStatusAsync(CancellationToken cancellation = default);
    IAsyncEnumerable<WorkflowEvent> WatchStreamAsync(CancellationToken cancellation = default);
}
