// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

namespace GLic.Auth;

internal static class GlicSession
{
    private static ApiClients? _clients;
    private static GlicConfig?  _config;

    internal static bool         IsConnected => _clients != null;
    internal static ApiClients?  Clients     => _clients;
    internal static GlicConfig?  Config      => _config;

    internal static void Set(ApiClients clients, GlicConfig config)
    {
        _clients = clients;
        _config  = config;
    }

    internal static void Clear()
    {
        _clients = null;
        _config  = null;
    }
}
