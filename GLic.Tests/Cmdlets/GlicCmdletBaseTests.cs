// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Linq;
using GLic.Cmdlets;
using Xunit;

namespace GLic.Tests.Cmdlets;

public class GlicCmdletBaseTests
{
    [Fact]
    public void StopProcessing_SetsCancellationRequested()
    {
        var cmdlet = new FakeCmdlet();
        cmdlet.InvokeStopProcessing();
        Assert.True(cmdlet.IsCancelled);
    }

    [Fact]
    public async Task EmitRowsAsync_PipelineMode_BuffersRowsForPipelineThread()
    {
        var cmdlet = new FakeCmdlet
        {
            RowsToEmit = new[] { new[] { "A", "1" }, new[] { "B", "2" } }.ToAsyncEnumerable()
        };

        await cmdlet.CallRunAsync();

        Assert.Equal(2, cmdlet.PendingOutput.Count);
        Assert.Equal(new[] { "A", "1" }, cmdlet.PendingOutput[0]);
        Assert.Equal(new[] { "B", "2" }, cmdlet.PendingOutput[1]);
    }

    private sealed class FakeCmdlet : GlicCmdletBase
    {
        public IAsyncEnumerable<string[]>? RowsToEmit { get; set; }

        protected override async Task RunAsync(CancellationToken ct)
        {
            if (RowsToEmit != null)
                await EmitRowsAsync(RowsToEmit, ct);
        }

        public async Task CallRunAsync() => await RunAsync(Cts.Token);

        public void InvokeStopProcessing() => StopProcessing();
        public bool IsCancelled => Cts.Token.IsCancellationRequested;
    }
}
