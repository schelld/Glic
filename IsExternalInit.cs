// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

// Required polyfill: net472 does not ship System.Runtime.CompilerServices.IsExternalInit,
// which the C# compiler emits for record init-only properties.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
