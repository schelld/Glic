// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Runtime.CompilerServices;

namespace GLic.Helpers;

public static class Paginator
{
    public static async IAsyncEnumerable<T> FetchAllAsync<T>(
        Func<string?, Task<(IEnumerable<T>? Items, string? NextPageToken)>> fetcher,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string? pageToken = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            var (items, nextPageToken) = await fetcher(pageToken);
            if (items != null)
                foreach (var item in items)
                    yield return item;
            pageToken = nextPageToken;
        } while (pageToken != null);
    }
}
