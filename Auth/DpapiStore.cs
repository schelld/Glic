// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Security.Cryptography;

namespace GLic.Auth;

internal static class DpapiStore
{
    internal static byte[] Protect(byte[] data) =>
        ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    internal static byte[] Unprotect(byte[] data) =>
        ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
