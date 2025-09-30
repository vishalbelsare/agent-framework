// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Workflows.Execution;

internal interface IInputCoordinator
{
    ValueTask WaitForNextInputAsync(CancellationToken cancellation = default);
}
