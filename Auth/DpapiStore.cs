// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Security.Cryptography;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace GLic.Auth;

internal static class DpapiStore
{
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    internal static byte[] Protect(byte[] data) =>
        ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    internal static byte[] Unprotect(byte[] data) =>
        ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
