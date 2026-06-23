// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Tests.Commands;

public class AppsCommandTests
{
    [Fact]
    public void BuildRow_MapsAllFields()
    {
        var app = new GoogleChromeManagementV1InstalledApp
        {
            DisplayName        = "Google Docs",
            AppId              = "aohghmighlieiainnegkcijnfilokake",
            AppType            = "WEB_APP",
            BrowserDeviceCount = 42,
        };

        var row = GetGlicAppsCmdlet.BuildRow(app, "2026-06-09", "C03fxe4vs");

        Assert.Equal("2026-06-09",                        row.ReportDate);
        Assert.Equal("C03fxe4vs",                        row.CustomerId);
        Assert.Equal("Google Docs",                      row.DisplayName);
        Assert.Equal("aohghmighlieiainnegkcijnfilokake", row.AppId);
        Assert.Equal("WEB_APP",                          row.AppType);
        Assert.Equal("",                                 row.Publisher);
        Assert.Equal(42L,                                row.BrowserDeviceCount);
    }

    [Fact]
    public void BuildRow_NullBrowserDeviceCount_ProducesZero()
    {
        var app = new GoogleChromeManagementV1InstalledApp { BrowserDeviceCount = null };

        var row = GetGlicAppsCmdlet.BuildRow(app, "2026-06-09", "C03fxe4vs");

        Assert.Equal(0L, row.BrowserDeviceCount);
    }
}
