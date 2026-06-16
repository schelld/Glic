// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Tests.Commands;

public class BrowserExtensionsCommandTests
{
    [Fact]
    public void BuildRow_MapsAllFields()
    {
        var app = new GoogleChromeManagementV1InstalledApp
        {
            AppId       = "ehoadneljpdggcbbknedodolkkjodefl",
            AppType     = "EXTENSION",
            DisplayName = "Google Docs Offline",
        };
        var profile = new GoogleChromeManagementV1ProfileAppInstallInstance
        {
            ProfilePermanentId = "perm-001",
            ProfileId          = "prof-001",
            Email              = "user@example.com",
            ProfileOrgUnitId   = "id:abc123",
        };

        var row = GetGlicBrowserExtensionsCmdlet.BuildRow(app, profile, "2026-06-06", "C03fxe4vs");

        Assert.Equal("2026-06-06",                        row.ReportDate);
        Assert.Equal("C03fxe4vs",                         row.CustomerId);
        Assert.Equal("perm-001",                          row.ProfilePermanentId);
        Assert.Equal("prof-001",                          row.ProfileId);
        Assert.Equal("user@example.com",                  row.Email);
        Assert.Equal("id:abc123",                         row.ProfileOrgUnitId);
        Assert.Equal("ehoadneljpdggcbbknedodolkkjodefl",  row.AppId);
        Assert.Equal("EXTENSION",                         row.AppType);
        Assert.Equal("Google Docs Offline",               row.DisplayName);
    }

    [Fact]
    public void BuildRow_NullFields_ProducesEmptyStrings()
    {
        var app     = new GoogleChromeManagementV1InstalledApp();
        var profile = new GoogleChromeManagementV1ProfileAppInstallInstance();

        var row = GetGlicBrowserExtensionsCmdlet.BuildRow(app, profile, "2026-06-06", "C03fxe4vs");

        Assert.Equal("", row.ProfilePermanentId);
        Assert.Equal("", row.ProfileId);
        Assert.Equal("", row.Email);
        Assert.Equal("", row.ProfileOrgUnitId);
        Assert.Equal("", row.AppId);
        Assert.Equal("", row.AppType);
        Assert.Equal("", row.DisplayName);
    }
}
