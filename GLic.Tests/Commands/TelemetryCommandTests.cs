// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using Google.Apis.Admin.Directory.directory_v1.Data;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Tests.Commands;

public class TelemetryCommandTests
{
    [Fact]
    public void BuildRow_MapsAllFields()
    {
        var device = new ChromeOsDevice
        {
            DeviceId       = "dir-device-id",
            SerialNumber   = "dir-serial",
            Status         = "ACTIVE",
            OrgUnitPath    = "/Schools/East",
            AnnotatedUser  = "student@example.com",
            OsVersion      = "15912.0.0",
            PlatformVersion = "15912.0.0",
            FirmwareVersion = "Google_Kindred.12345.0.0",
            AutoUpdateThrough = "2027-06-01",
        };

        var telemetry = new GoogleChromeManagementV1TelemetryDevice
        {
            DeviceId     = "tel-device-id",
            SerialNumber = "tel-serial",
            OsUpdateStatus =
            [
                new GoogleChromeManagementV1OsUpdateStatus
                {
                    UpdateState             = "OS_UP_TO_DATE",
                    LastUpdateCheckTimeRaw  = "2026-06-01T10:00:00Z",
                    LastUpdateTimeRaw       = "2026-05-28T08:00:00Z",
                    LastRebootTimeRaw       = "2026-05-28T09:00:00Z",
                    NewPlatformVersion      = "",
                },
            ],
        };

        var row = GetGlicTelemetryCmdlet.BuildRow(device, telemetry);

        Assert.Equal("tel-device-id",              row.DeviceId);
        Assert.Equal("tel-serial",                 row.SerialNumber);
        Assert.Equal("ACTIVE",                     row.Status);
        Assert.Equal("/Schools/East",              row.OrgUnitPath);
        Assert.Equal("student@example.com",        row.AnnotatedUser);
        Assert.Equal("15912.0.0",                  row.OsVersion);
        Assert.Equal("15912.0.0",                  row.PlatformVersion);
        Assert.Equal("Google_Kindred.12345.0.0",   row.FirmwareVersion);
        Assert.Equal(DateTimeOffset.Parse("2027-06-01"),                 row.AutoUpdateExpiration);
        Assert.Equal("OS_UP_TO_DATE",              row.UpdateState);
        Assert.Equal(DateTimeOffset.Parse("2026-06-01T10:00:00Z"),       row.LastUpdateCheckTime);
        Assert.Equal(DateTimeOffset.Parse("2026-05-28T08:00:00Z"),       row.LastUpdateTime);
        Assert.Equal(DateTimeOffset.Parse("2026-05-28T09:00:00Z"),       row.LastRebootTime);
        Assert.Equal("",                           row.NewPlatformVersion);
    }

    [Fact]
    public void BuildRow_NullTelemetry_EmitsNullForTimestampColumns()
    {
        var device = new ChromeOsDevice
        {
            DeviceId      = "dir-device-id",
            SerialNumber  = "dir-serial",
            Status        = "DEPROVISIONED",
            OrgUnitPath   = "/Retired",
            AnnotatedUser = "",
            OsVersion     = "14000.0.0",
            AutoUpdateThrough = "2024-06-01",
        };

        var row = GetGlicTelemetryCmdlet.BuildRow(device, null);

        Assert.Equal("dir-device-id",  row.DeviceId);
        Assert.Equal("dir-serial",     row.SerialNumber);
        Assert.Equal("DEPROVISIONED",  row.Status);
        Assert.Equal("/Retired",       row.OrgUnitPath);
        Assert.Equal("",               row.AnnotatedUser);
        Assert.Equal("14000.0.0",      row.OsVersion);
        Assert.Equal("",               row.PlatformVersion);
        Assert.Equal("",               row.FirmwareVersion);
        Assert.Equal(DateTimeOffset.Parse("2024-06-01"),   row.AutoUpdateExpiration);
        Assert.Equal("",               row.UpdateState);
        Assert.Null(row.LastUpdateCheckTime);
        Assert.Null(row.LastUpdateTime);
        Assert.Null(row.LastRebootTime);
        Assert.Equal("",               row.NewPlatformVersion);
    }
}
