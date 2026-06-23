// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Tests.Commands;

public class ManagedBrowsersCommandTests
{
    [Fact]
    public void BuildRow_MapsAllFields()
    {
        var profile = new GoogleChromeManagementVersionsV1ChromeBrowserProfile
        {
            ProfilePermanentId  = "perm-001",
            ProfileId           = "prof-001",
            DisplayName         = "Work Profile",
            UserEmail           = "user@example.com",
            UserId              = "uid-001",
            AnnotatedUser       = "John Doe",
            BrowserVersion      = "120.0.6099.234",
            BrowserChannel      = "STABLE",
            OsPlatformType      = "WINDOWS",
            OsVersion           = "10.0.19045",
            OsPlatformVersion   = "10.0.19045.3693",
            DeviceInfo = new GoogleChromeManagementVersionsV1DeviceInfo
            {
                Hostname          = "LAPTOP-001",
                Machine           = "LAPTOP-001",
                DeviceType        = "COMPUTER",
                AffiliatedDeviceId = "aff-001"
            },
            AnnotatedLocation     = "HQ",
            ExtensionCount        = 5,
            PolicyCount           = 12,
            FirstEnrollmentTimeRaw  = "2023-01-15T10:00:00Z",
            LastActivityTimeRaw     = "2024-06-01T14:30:00Z",
            LastPolicySyncTimeRaw   = "2024-06-05T09:00:00Z",
            LastStatusReportTimeRaw = "2024-06-05T09:15:00Z",
        };

        var row = GetGlicManagedBrowsersCmdlet.BuildRow(profile, "2026-06-06", "C03fxe4vs");

        Assert.Equal("2026-06-06",             row.ReportDate);
        Assert.Equal("C03fxe4vs",             row.CustomerId);
        Assert.Equal("perm-001",              row.ProfilePermanentId);
        Assert.Equal("prof-001",              row.ProfileId);
        Assert.Equal("Work Profile",          row.DisplayName);
        Assert.Equal("user@example.com",      row.UserEmail);
        Assert.Equal("uid-001",               row.UserId);
        Assert.Equal("John Doe",              row.AnnotatedUser);
        Assert.Equal("120.0.6099.234",        row.BrowserVersion);
        Assert.Equal("STABLE",                row.BrowserChannel);
        Assert.Equal("WINDOWS",               row.OsPlatformType);
        Assert.Equal("10.0.19045",            row.OsVersion);
        Assert.Equal("10.0.19045.3693",       row.OsPlatformVersion);
        Assert.Equal("LAPTOP-001",            row.Hostname);
        Assert.Equal("LAPTOP-001",            row.Machine);
        Assert.Equal("COMPUTER",              row.DeviceType);
        Assert.Equal("aff-001",              row.AffiliatedDeviceId);
        Assert.Equal("HQ",                    row.AnnotatedLocation);
        Assert.Equal(5L,                                                   row.ExtensionCount);
        Assert.Equal(12L,                                                  row.PolicyCount);
        Assert.Equal(DateTimeOffset.Parse("2023-01-15T10:00:00Z"),        row.FirstEnrollmentTime);
        Assert.Equal(DateTimeOffset.Parse("2024-06-01T14:30:00Z"),        row.LastActivityTime);
        Assert.Equal(DateTimeOffset.Parse("2024-06-05T09:00:00Z"),        row.LastPolicySyncTime);
        Assert.Equal(DateTimeOffset.Parse("2024-06-05T09:15:00Z"),        row.LastStatusReportTime);
    }

    [Fact]
    public void BuildRow_NullDeviceInfo_ProducesEmptyStrings()
    {
        var profile = new GoogleChromeManagementVersionsV1ChromeBrowserProfile
        {
            ProfilePermanentId = "perm-002",
            DeviceInfo = null
        };

        var row = GetGlicManagedBrowsersCmdlet.BuildRow(profile, "2026-06-06", "C03fxe4vs");

        Assert.Equal("", row.Hostname);
        Assert.Equal("", row.Machine);
        Assert.Equal("", row.DeviceType);
        Assert.Equal("", row.AffiliatedDeviceId);
    }

    [Fact]
    public void BuildRow_NullCounts_ProducesNull()
    {
        var profile = new GoogleChromeManagementVersionsV1ChromeBrowserProfile
        {
            ExtensionCount = null,
            PolicyCount    = null
        };

        var row = GetGlicManagedBrowsersCmdlet.BuildRow(profile, "2026-06-06", "C03fxe4vs");

        Assert.Null(row.ExtensionCount);
        Assert.Null(row.PolicyCount);
    }
}
