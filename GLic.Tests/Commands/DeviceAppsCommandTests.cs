// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Tests.Commands;

public class DeviceAppsCommandTests
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
        var device = new GoogleChromeManagementV1Device
        {
            DeviceId = "dev-001",
            Machine  = "CHROMEBOOK-01",
        };

        var row = GetGlicDeviceAppsCmdlet.BuildRow(app, device, "2026-06-06", "C03fxe4vs");

        Assert.Equal("2026-06-06",                        row.ReportDate);
        Assert.Equal("C03fxe4vs",                         row.CustomerId);
        Assert.Equal("dev-001",                           row.DeviceId);
        Assert.Equal("CHROMEBOOK-01",                     row.Machine);
        Assert.Equal("ehoadneljpdggcbbknedodolkkjodefl",  row.AppId);
        Assert.Equal("EXTENSION",                         row.AppType);
        Assert.Equal("Google Docs Offline",               row.DisplayName);
    }

    [Fact]
    public void BuildRow_NullFields_ProducesEmptyStrings()
    {
        var app    = new GoogleChromeManagementV1InstalledApp();
        var device = new GoogleChromeManagementV1Device();

        var row = GetGlicDeviceAppsCmdlet.BuildRow(app, device, "2026-06-06", "C03fxe4vs");

        Assert.Equal("", row.DeviceId);
        Assert.Equal("", row.Machine);
        Assert.Equal("", row.AppId);
        Assert.Equal("", row.AppType);
        Assert.Equal("", row.DisplayName);
    }
}
