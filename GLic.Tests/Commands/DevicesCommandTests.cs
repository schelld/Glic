// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using Google.Apis.Admin.Directory.directory_v1.Data;

namespace GLic.Tests.Commands;

public class DevicesCommandTests
{
    [Fact]
    public void BuildRow_MapsAllFields()
    {
        var device = new ChromeOsDevice
        {
            DeviceId              = "device-001",
            SerialNumber          = "SN-001",
            Model                 = "HP Chromebook 14",
            Status                = "ACTIVE",
            OrgUnitPath           = "/Students",
            AnnotatedUser         = "student@example.com",
            AnnotatedLocation     = "Library",
            LastSyncRaw           = "2026-06-01T08:00:00Z",
            LastEnrollmentTimeRaw = "2023-09-01T12:00:00Z",
            OsVersion             = "120.0.6099.234",
            MacAddress            = "AA:BB:CC:DD:EE:FF",
            EthernetMacAddress    = "11:22:33:44:55:66",
            AnnotatedAssetId      = "ASSET-001",
            PlatformVersion       = "15388.0.0",
            FirmwareVersion       = "Google_Kefka.14145.0.0",
            BootMode              = "VERIFIED",
            Notes                 = "Loaner device",
            Meid                  = "35-123456-789012-3",
        };

        var row = GetGlicDevicesCmdlet.BuildRow(device, "2026-06-09", "C03fxe4vs");

        Assert.Equal("2026-06-09",           row.ReportDate);
        Assert.Equal("C03fxe4vs",           row.CustomerId);
        Assert.Equal("device-001",          row.DeviceId);
        Assert.Equal("SN-001",              row.SerialNumber);
        Assert.Equal("HP Chromebook 14",    row.Model);
        Assert.Equal("ACTIVE",              row.Status);
        Assert.Equal("/Students",           row.OrgUnitPath);
        Assert.Equal("student@example.com", row.AnnotatedUser);
        Assert.Equal("",                    row.LastSyncUser);  // RecentUsers not set
        Assert.Equal("Library",             row.AnnotatedLocation);
        Assert.Equal(DateTimeOffset.Parse("2026-06-01T08:00:00Z"),  row.LastSync);
        Assert.Equal(DateTimeOffset.Parse("2023-09-01T12:00:00Z"),  row.EnrollmentTime);
        Assert.Equal("120.0.6099.234",      row.OsVersion);
        Assert.Equal("AA:BB:CC:DD:EE:FF",   row.MacAddress);
        Assert.Equal("11:22:33:44:55:66",   row.EthernetMacAddress);
        Assert.Equal("",                    row.LastKnownIp);   // LastKnownNetwork not set
        Assert.Equal("ASSET-001",           row.AnnotatedAssetId);
        Assert.Equal("",                    row.OrderNumber);   // OrderNumber not set
        Assert.Equal("15388.0.0",           row.PlatformVersion);
        Assert.Equal("Google_Kefka.14145.0.0", row.FirmwareVersion);
        Assert.Equal("VERIFIED",            row.BootMode);
        Assert.Equal("Loaner device",       row.Notes);
        Assert.Equal("35-123456-789012-3",  row.Meid);
    }

    [Fact]
    public void BuildRow_NullTimestamps_ProducesNull()
    {
        var device = new ChromeOsDevice
        {
            DeviceId              = "device-002",
            LastSyncRaw           = null,
            LastEnrollmentTimeRaw = null,
        };

        var row = GetGlicDevicesCmdlet.BuildRow(device, "2026-06-09", "C03fxe4vs");

        Assert.Null(row.LastSync);
        Assert.Null(row.EnrollmentTime);
    }
}
