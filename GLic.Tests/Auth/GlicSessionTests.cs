// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.ChromeManagement.v1;
using Google.Apis.Licensing.v1;
using Google.Apis.Services;
using GLic.Auth;

namespace GLic.Tests.Auth;

[Collection("GlicSession")]
public class GlicSessionTests : IDisposable
{
    // Clear static state between tests
    public void Dispose() => GlicSession.Clear();

    private static ApiClients MakeClients()
    {
        // Unauthenticated initializer — enough to construct service objects for testing
        var init = new BaseClientService.Initializer();
        return new ApiClients(
            new ChromeManagementService(init),
            new LicensingService(init),
            new DirectoryService(init));
    }

    [Fact]
    public void IsConnected_Initially_IsFalse()
    {
        GlicSession.Clear();
        Assert.False(GlicSession.IsConnected);
        Assert.Null(GlicSession.Clients);
        Assert.Null(GlicSession.Config);
    }

    [Fact]
    public void Set_WithClientsAndConfig_IsConnectedIsTrue()
    {
        var config = new GlicConfig("C03test", "admin@example.com");
        GlicSession.Set(MakeClients(), config);

        Assert.True(GlicSession.IsConnected);
        Assert.NotNull(GlicSession.Clients);
        Assert.Equal("C03test",           GlicSession.Config!.CustomerId);
        Assert.Equal("admin@example.com", GlicSession.Config.AdminEmail);
    }

    [Fact]
    public void Clear_AfterSet_ResetsAllState()
    {
        GlicSession.Set(MakeClients(), new GlicConfig("C03test", "admin@example.com"));
        GlicSession.Clear();

        Assert.False(GlicSession.IsConnected);
        Assert.Null(GlicSession.Clients);
        Assert.Null(GlicSession.Config);
    }
}
