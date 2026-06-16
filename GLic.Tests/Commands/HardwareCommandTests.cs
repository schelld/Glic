// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using Google.Apis.Admin.Directory.directory_v1.Data;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Tests.Commands;

public class HardwareCommandTests
{
    [Fact]
    public void BuildRow_MapsAllTelemetryFields()
    {
        var telemetry = new GoogleChromeManagementV1TelemetryDevice
        {
            DeviceId     = "device-123",
            SerialNumber = "SN-ABC",
            CpuInfo = new List<GoogleChromeManagementV1CpuInfo>
            {
                new GoogleChromeManagementV1CpuInfo
                {
                    Model         = "Intel Core i5",
                    Architecture  = "X86_64",
                    MaxClockSpeed = 2400,
                }
            },
            MemoryInfo = new GoogleChromeManagementV1MemoryInfo
            {
                TotalRamBytes = 8589934592L,
            },
            StorageInfo = new GoogleChromeManagementV1StorageInfo
            {
                TotalDiskBytes = 107374182400L,
            },
            StorageStatusReport = new List<GoogleChromeManagementV1StorageStatusReport>
            {
                new GoogleChromeManagementV1StorageStatusReport
                {
                    ReportTimeRaw = "2026-06-05T10:00:00Z",
                    Disk = new List<GoogleChromeManagementV1DiskInfo>
                    {
                        new GoogleChromeManagementV1DiskInfo
                        {
                            Model        = "Samsung SSD 860",
                            Type         = "SSD",
                            SizeBytes    = 107374182400L,
                            Health       = "95",
                            Manufacturer = "Samsung",
                        }
                    },
                }
            },
            BatteryInfo = new List<GoogleChromeManagementV1BatteryInfo>
            {
                new GoogleChromeManagementV1BatteryInfo
                {
                    Manufacturer   = "LGC",
                    DesignCapacity = 50000,
                }
            },
            BatteryStatusReport = new List<GoogleChromeManagementV1BatteryStatusReport>
            {
                new GoogleChromeManagementV1BatteryStatusReport
                {
                    ReportTimeRaw      = "2026-06-05T10:00:00Z",
                    BatteryHealth      = "Good",
                    FullChargeCapacity = 45000,
                    CycleCount         = 120,
                }
            },
            NetworkInfo = new GoogleChromeManagementV1NetworkInfo
            {
                NetworkDevices = new List<GoogleChromeManagementV1NetworkDevice>
                {
                    new GoogleChromeManagementV1NetworkDevice
                    {
                        MacAddress = "AA:BB:CC:DD:EE:FF",
                        Type       = "WIFI",
                    }
                }
            },
            GraphicsInfo = new GoogleChromeManagementV1GraphicsInfo
            {
                AdapterInfo = new GoogleChromeManagementV1GraphicsAdapterInfo
                {
                    Adapter       = "Intel UHD Graphics 620",
                    DriverVersion = "30.0.101.1340",
                }
            },
            OsUpdateStatus = new List<GoogleChromeManagementV1OsUpdateStatus>
            {
                new GoogleChromeManagementV1OsUpdateStatus
                {
                    LastRebootTimeRaw = "2026-06-01T08:00:00Z",
                    UpdateState       = "OS_UP_TO_DATE",
                }
            },
        };

        var device = new ChromeOsDevice
        {
            DeviceId              = "device-123",
            SerialNumber          = "SN-ABC",
            Model                 = "Lenovo 100e Chromebook",
            Status                = "ACTIVE",
            OrgUnitPath           = "/Chromebooks",
            AnnotatedUser         = "jdoe@example.com",
            AnnotatedLocation     = "Lab 5",
            AnnotatedAssetId      = "ASSET-001",
            LastEnrollmentTimeRaw = "2023-01-15T10:00:00Z",
            OsVersion             = "115.0.5790.170",
        };

        var row = GetGlicHardwareCmdlet.BuildRow(device, telemetry, "2026-06-06", "C03fxe4vs");

        Assert.Equal("2026-06-06",          row.ReportDate);
        Assert.Equal("C03fxe4vs",           row.CustomerId);
        Assert.Equal("device-123",          row.DeviceId);
        Assert.Equal("SN-ABC",              row.SerialNumber);
        Assert.Equal("Lenovo",              row.Manufacturer);
        Assert.Equal("Intel Core i5",       row.CpuModel);
        Assert.Equal("X86_64",              row.CpuArchitecture);
        Assert.Equal(2400L,                                              row.CpuMaxClockSpeedKhz);
        Assert.Equal(8589934592L,                                        row.RamTotalBytes);
        Assert.Equal(107374182400L,                                      row.TotalDiskBytes);
        Assert.Equal(8.0,    row.RamGb);
        Assert.Equal(100.0,  row.TotalDiskGb);
        Assert.Equal("100",  row.DiskSizeGb);
        Assert.Equal("Samsung SSD 860",     row.DiskModels);
        Assert.Equal("SSD",                 row.DiskTypes);
        Assert.Equal("107374182400",        row.DiskSizeBytes);
        Assert.Equal("95",                  row.DiskHealths);
        Assert.Equal("Samsung",             row.DiskManufacturers);
        Assert.Equal("LGC",                 row.BatteryManufacturer);
        Assert.Equal(50000L,                                             row.BatteryDesignCapacity);
        Assert.Equal(45000L,                                             row.BatteryFullChargeCapacity);
        Assert.Equal("Good",                row.BatteryHealth);
        Assert.Equal(120L,                                               row.BatteryCycleCount);
        Assert.Equal("AA:BB:CC:DD:EE:FF",   row.NetworkMacAddresses);
        Assert.Equal("WIFI",                row.NetworkTypes);
        Assert.Equal("Intel UHD Graphics 620", row.GpuAdapter);
        Assert.Equal("30.0.101.1340",       row.GpuDriverVersion);
        Assert.Equal("Up To Date",           row.OsUpdateState);
        Assert.Equal(DateTimeOffset.Parse("2026-06-01T08:00:00Z"),      row.OsLastRebootTime);
        Assert.Equal("Lenovo 100e Chromebook", row.Model);
        Assert.Equal("ACTIVE",              row.Status);
        Assert.Equal("/Chromebooks",        row.OrgUnitPath);
        Assert.Equal("jdoe@example.com",    row.AnnotatedUser);
        Assert.Equal("",                    row.LastSyncUser);   // RecentUsers not set
        Assert.Equal("Lab 5",               row.AnnotatedLocation);
        Assert.Equal("ASSET-001",           row.AnnotatedAssetId);
        Assert.Equal(DateTimeOffset.Parse("2023-01-15T10:00:00Z"),      row.EnrollmentTime);
        Assert.Equal("115.0.5790.170",      row.OsVersion);
    }

    [Fact]
    public void BuildRow_JoinsMultiValueFields_WithSemicolon()
    {
        var telemetry = new GoogleChromeManagementV1TelemetryDevice
        {
            DeviceId = "device-multi",
            StorageStatusReport = new List<GoogleChromeManagementV1StorageStatusReport>
            {
                new GoogleChromeManagementV1StorageStatusReport
                {
                    ReportTimeRaw = "2026-06-05T10:00:00Z",
                    Disk = new List<GoogleChromeManagementV1DiskInfo>
                    {
                        new GoogleChromeManagementV1DiskInfo
                        {
                            Model = "Samsung SSD", Type = "SSD",
                            SizeBytes = 107374182400L, Health = "95", Manufacturer = "Samsung",
                        },
                        new GoogleChromeManagementV1DiskInfo
                        {
                            Model = "WD HDD", Type = "HDD",
                            SizeBytes = 500107862016L, Health = "80", Manufacturer = "WD",
                        },
                    },
                }
            },
            NetworkInfo = new GoogleChromeManagementV1NetworkInfo
            {
                NetworkDevices = new List<GoogleChromeManagementV1NetworkDevice>
                {
                    new GoogleChromeManagementV1NetworkDevice { MacAddress = "AA:BB:CC:DD:EE:FF", Type = "WIFI" },
                    new GoogleChromeManagementV1NetworkDevice { MacAddress = "11:22:33:44:55:66", Type = "ETHERNET" },
                }
            },
        };
        var device = new ChromeOsDevice { DeviceId = "device-multi" };

        var row = GetGlicHardwareCmdlet.BuildRow(device, telemetry, "2026-06-06", "C03fxe4vs");

        Assert.Equal("Samsung SSD;WD HDD",             row.DiskModels);
        Assert.Equal("SSD;HDD",                        row.DiskTypes);
        Assert.Equal("107374182400;500107862016",       row.DiskSizeBytes);
        Assert.Equal("100;465.76", row.DiskSizeGb);
        Assert.Equal("95;80",                          row.DiskHealths);
        Assert.Equal("Samsung;WD",                     row.DiskManufacturers);
        Assert.Equal("AA:BB:CC:DD:EE:FF;11:22:33:44:55:66", row.NetworkMacAddresses);
        Assert.Equal("WIFI;ETHERNET",                  row.NetworkTypes);
    }

    [Fact]
    public void BuildRow_NullTelemetry_ProducesEmptyTelemetryFields()
    {
        var device = new ChromeOsDevice
        {
            DeviceId     = "device-no-telemetry",
            SerialNumber = "SN-XYZ",
            Model        = "HP Chromebook",
            Status       = "ACTIVE",
            OrgUnitPath  = "/Students",
        };

        var row = GetGlicHardwareCmdlet.BuildRow(device, null, "2026-06-06", "C03fxe4vs");

        // Identity falls back to Directory
        Assert.Equal("device-no-telemetry", row.DeviceId);
        Assert.Equal("SN-XYZ",              row.SerialNumber);
        // All telemetry fields are empty
        Assert.Equal("", row.CpuModel);
        Assert.Equal("", row.CpuArchitecture);
        Assert.Null(row.CpuMaxClockSpeedKhz);
        Assert.Null(row.RamTotalBytes);
        Assert.Null(row.TotalDiskBytes);
        Assert.Null(row.RamGb);
        Assert.Null(row.TotalDiskGb);
        Assert.Equal("", row.DiskSizeGb);
        Assert.Equal("", row.DiskModels);
        Assert.Equal("", row.BatteryManufacturer);
        Assert.Equal("", row.BatteryHealth);
        Assert.Equal("", row.NetworkMacAddresses);
        Assert.Equal("", row.GpuAdapter);
        Assert.Equal("", row.OsUpdateState);
        // Directory fields still populated
        Assert.Equal("HP Chromebook", row.Model);
        Assert.Equal("HP",             row.Manufacturer);
        Assert.Equal("ACTIVE",        row.Status);
        Assert.Equal("/Students",     row.OrgUnitPath);
        Assert.Equal("",              row.LastSyncUser);
        Assert.Equal("",              row.AnnotatedLocation);
    }

    [Fact]
    public void BuildRow_NullCpuInfoOnTelemetry_ProducesEmptyCpuFields()
    {
        var telemetry = new GoogleChromeManagementV1TelemetryDevice
        {
            DeviceId = "device-no-cpu",
            CpuInfo  = null,
            MemoryInfo = new GoogleChromeManagementV1MemoryInfo { TotalRamBytes = 4294967296L },
        };
        var device = new ChromeOsDevice { DeviceId = "device-no-cpu" };

        var row = GetGlicHardwareCmdlet.BuildRow(device, telemetry, "2026-06-06", "C03fxe4vs");

        Assert.Equal("",           row.CpuModel);
        Assert.Equal("",           row.CpuArchitecture);
        Assert.Null(row.CpuMaxClockSpeedKhz);
        Assert.Equal(4294967296L, row.RamTotalBytes);
        Assert.Equal("", row.Manufacturer);   // null Model → empty manufacturer
    }

    [Fact]
    public void BuildRow_PicksLatestOsUpdateStatus()
    {
        var telemetry = new GoogleChromeManagementV1TelemetryDevice
        {
            DeviceId = "device-os",
            OsUpdateStatus = new List<GoogleChromeManagementV1OsUpdateStatus>
            {
                new GoogleChromeManagementV1OsUpdateStatus
                {
                    LastUpdateCheckTimeRaw = "2026-05-01T10:00:00Z",
                    LastRebootTimeRaw      = "2026-04-30T08:00:00Z",
                    UpdateState            = "OS_IMAGE_DOWNLOAD_IN_PROGRESS",
                },
                new GoogleChromeManagementV1OsUpdateStatus
                {
                    LastUpdateCheckTimeRaw = "2026-06-05T10:00:00Z",
                    LastRebootTimeRaw      = "2026-06-01T08:00:00Z",
                    UpdateState            = "OS_UP_TO_DATE",
                },
            },
        };
        var device = new ChromeOsDevice { DeviceId = "device-os" };

        var row = GetGlicHardwareCmdlet.BuildRow(device, telemetry, "2026-06-06", "C03fxe4vs");

        Assert.Equal("Up To Date",             row.OsUpdateState);
        Assert.Equal(DateTimeOffset.Parse("2026-06-01T08:00:00Z"), row.OsLastRebootTime);
    }

    [Fact]
    public void BuildRow_MapsAutoUpdateExpiration()
    {
        var device = new ChromeOsDevice
        {
            DeviceId         = "device-aue",
            AutoUpdateThrough = "2027-06-01",
        };

        var row = GetGlicHardwareCmdlet.BuildRow(device, null, "2026-06-10", "C03fxe4vs");

        Assert.Equal(DateTimeOffset.Parse("2027-06-01"), row.AutoUpdateExpiration);
    }

    [Fact]
    public void BuildRow_NullAutoUpdateThrough_ProducesNullExpiration()
    {
        var device = new ChromeOsDevice { DeviceId = "device-no-aue" };

        var row = GetGlicHardwareCmdlet.BuildRow(device, null, "2026-06-10", "C03fxe4vs");

        Assert.Null(row.AutoUpdateExpiration);
    }

    [Fact]
    public void BuildRow_PicksLatestBatteryStatusReport()
    {
        var telemetry = new GoogleChromeManagementV1TelemetryDevice
        {
            DeviceId = "device-bat",
            BatteryStatusReport = new List<GoogleChromeManagementV1BatteryStatusReport>
            {
                new GoogleChromeManagementV1BatteryStatusReport
                {
                    ReportTimeRaw      = "2026-05-01T10:00:00Z",
                    BatteryHealth      = "Degraded",
                    FullChargeCapacity = 30000,
                    CycleCount         = 200,
                },
                new GoogleChromeManagementV1BatteryStatusReport
                {
                    ReportTimeRaw      = "2026-06-05T10:00:00Z",
                    BatteryHealth      = "Good",
                    FullChargeCapacity = 45000,
                    CycleCount         = 120,
                },
            },
        };
        var device = new ChromeOsDevice { DeviceId = "device-bat" };

        var row = GetGlicHardwareCmdlet.BuildRow(device, telemetry, "2026-06-06", "C03fxe4vs");

        Assert.Equal("Good",  row.BatteryHealth);
        Assert.Equal(45000L, row.BatteryFullChargeCapacity);
        Assert.Equal(120L,   row.BatteryCycleCount);
    }

    [Theory]
    [InlineData("OS_UP_TO_DATE",                 "Up To Date")]
    [InlineData("OS_UPDATE_AVAILABLE",           "Update Available")]
    [InlineData("OS_IMAGE_DOWNLOAD_IN_PROGRESS", "Downloading Update")]
    [InlineData("OS_IMAGE_DOWNLOAD_NOT_STARTED", "Download Not Started")]
    [InlineData("OS_UPDATE_NEED_REBOOT",         "Reboot Required")]
    public void NormalizeOsUpdateState_KnownValues_ReturnReadableString(string raw, string expected)
    {
        Assert.Equal(expected, GetGlicHardwareCmdlet.NormalizeOsUpdateState(raw));
    }

    [Theory]
    [InlineData("OS_SOME_FUTURE_STATE", "Some Future State")]
    [InlineData("UNKNOWN_VALUE",        "Unknown Value")]
    [InlineData(null,                   "")]
    [InlineData("",                     "")]
    public void NormalizeOsUpdateState_UnknownOrEmpty_ReturnsTitleCaseFallback(string? raw, string expected)
    {
        Assert.Equal(expected, GetGlicHardwareCmdlet.NormalizeOsUpdateState(raw));
    }
}
