// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Helpers;

namespace GLic.Tests.Helpers;

public class PaginatorTests
{
    [Fact]
    public async Task FetchAllAsync_SinglePage_ReturnsAllItems()
    {
        var callCount = 0;

        Task<(IEnumerable<string>? Items, string? NextPageToken)> Fetcher(string? _)
        {
            callCount++;
            return Task.FromResult<(IEnumerable<string>?, string?)>((["a", "b", "c"], null));
        }

        var result = new List<string>();
        await foreach (var item in Paginator.FetchAllAsync<string>(Fetcher))
            result.Add(item);

        Assert.Equal(["a", "b", "c"], result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task FetchAllAsync_MultiplePages_ReturnsAllItems()
    {
        var pages = new Queue<(IEnumerable<string>?, string?)>([
            (["a", "b"], "t1"),
            (["c", "d"], "t2"),
            (["e"],      null),
        ]);

        Task<(IEnumerable<string>? Items, string? NextPageToken)> Fetcher(string? _)
            => Task.FromResult(pages.Dequeue());

        var result = new List<string>();
        await foreach (var item in Paginator.FetchAllAsync<string>(Fetcher))
            result.Add(item);

        Assert.Equal(["a", "b", "c", "d", "e"], result);
    }

    [Fact]
    public async Task FetchAllAsync_NullItems_YieldsNothing()
    {
        Task<(IEnumerable<string>? Items, string? NextPageToken)> Fetcher(string? _)
            => Task.FromResult<(IEnumerable<string>?, string?)>((null, null));

        var result = new List<string>();
        await foreach (var item in Paginator.FetchAllAsync<string>(Fetcher))
            result.Add(item);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchAllAsync_PassesPageTokenToNextFetch()
    {
        var receivedTokens = new List<string?>();
        var pages = new Queue<(IEnumerable<string>?, string?)>([
            (["x"], "page2"),
            (["y"], null),
        ]);

        Task<(IEnumerable<string>? Items, string? NextPageToken)> Fetcher(string? token)
        {
            receivedTokens.Add(token);
            return Task.FromResult(pages.Dequeue());
        }

        await foreach (var _ in Paginator.FetchAllAsync<string>(Fetcher)) { }

        Assert.Equal([null, "page2"], receivedTokens);
    }

    [Fact]
    public async Task FetchAllAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in Paginator.FetchAllAsync<string>(
                _ => Task.FromResult<(IEnumerable<string>?, string?)>((["x"], null)),
                cts.Token)) { }
        });
    }
}
