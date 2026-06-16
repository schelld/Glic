// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

#nullable disable
// Runtime polyfills for APIs not present in .NET Framework 4.7.2.
namespace GLic
{
    internal static class Polyfills
    {
        internal static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default)
            => dict.TryGetValue(key, out var v) ? v : defaultValue;

        internal static TSource MaxBy<TSource, TKey>(
            this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
            where TKey : IComparable<TKey>
        {
            using var e = source.GetEnumerator();
            if (!e.MoveNext()) return default;
            var max = e.Current;
            var maxKey = keySelector(max);
            while (e.MoveNext())
            {
                var key = keySelector(e.Current);
                if (key != null && (maxKey == null || key.CompareTo(maxKey) > 0))
                {
                    max = e.Current;
                    maxKey = key;
                }
            }
            return max;
        }

        internal static void Deconstruct<TKey, TValue>(
            this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
    }
}
