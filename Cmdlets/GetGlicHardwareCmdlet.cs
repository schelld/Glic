// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Runtime.CompilerServices;
using GLic.Auth;
using GLic.Helpers;
using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Admin.Directory.directory_v1.Data;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommon.Get, "GlicHardware")]
[OutputType(typeof(HardwareRow))]
public sealed class GetGlicHardwareCmdlet : GlicCmdletBase
{
    [Parameter]
    [ValidateSet("all", "active", "deprovisioned", "disabled", IgnoreCase = true)]
    public string Status { get; set; } = "all";
    [Parameter] public string? OrgUnit { get; set; }

    protected override async Task RunAsync(CancellationToken ct)
    {
        var cfg = LoadConfig();
        var clients = await BuildClientsAsync(cfg);
        var reportDate = DateTime.Now.ToString("yyyy-MM-dd");
        var rows = GetRowsAsync(clients, cfg.CustomerId, Status, OrgUnit, reportDate, ct);
        await EmitRowsAsync(rows, ct);
    }

    private async IAsyncEnumerable<HardwareRow> GetRowsAsync(
        ApiClients clients, string customer, string status, string? orgUnit,
        string reportDate, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var telemetryDict = new Dictionary<string, GoogleChromeManagementV1TelemetryDevice>(
            StringComparer.OrdinalIgnoreCase);

        var parent = $"customers/{customer}";
        await foreach (var td in Paginator.FetchAllAsync<GoogleChromeManagementV1TelemetryDevice>(
            async pageToken =>
            {
                var req = clients.ChromeManagement.Customers.Telemetry.Devices.List(parent);
                req.ReadMask = "name,device_id,serial_number,cpu_info,memory_info,battery_info," +
                               "battery_status_report,storage_info,storage_status_report," +
                               "network_info,graphics_info,os_update_status";
                req.PageSize  = 100;
                req.PageToken = pageToken;
                var resp = await req.ExecuteAsync(ct);
                return (resp.Devices, resp.NextPageToken);
            }, ct))
        {
            if (td.DeviceId is not null)
                telemetryDict[td.DeviceId] = td;
        }

        await foreach (var device in Paginator.FetchAllAsync<ChromeOsDevice>(
            async pageToken =>
            {
                var req = clients.Directory.Chromeosdevices.List(customer);
                req.Projection = ChromeosdevicesResource.ListRequest.ProjectionEnum.FULL;
                if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
                    req.Query = $"status:{status.ToUpperInvariant()}";
                if (orgUnit is not null)
                    req.OrgUnitPath = orgUnit;
                req.MaxResults = 200;
                req.PageToken  = pageToken;
                var resp = await req.ExecuteAsync(ct);
                return (resp.Chromeosdevices, resp.NextPageToken);
            }, ct))
        {
            telemetryDict.TryGetValue(device.DeviceId ?? "", out var telemetry);
            yield return BuildRow(device, telemetry, reportDate, customer);
        }
    }

    internal static HardwareRow BuildRow(
        ChromeOsDevice device,
        GoogleChromeManagementV1TelemetryDevice? telemetry,
        string reportDate,
        string customerId)
    {
        var cpu       = telemetry?.CpuInfo?.FirstOrDefault();
        var memory    = telemetry?.MemoryInfo;
        var battery   = telemetry?.BatteryInfo?.FirstOrDefault();
        var latestBat = telemetry?.BatteryStatusReport?.MaxBy(r => r.ReportTimeRaw);
        var latestSto = telemetry?.StorageStatusReport?.MaxBy(r => r.ReportTimeRaw);
        var latestOs  = telemetry?.OsUpdateStatus?.MaxBy(r => r.LastUpdateCheckTimeRaw);
        var network   = telemetry?.NetworkInfo?.NetworkDevices;
        var gpu       = telemetry?.GraphicsInfo?.AdapterInfo;
        var storage   = telemetry?.StorageInfo;

        return new HardwareRow(
            ReportDate:                reportDate,
            CustomerId:                customerId,
            DeviceId:                  telemetry?.DeviceId ?? device.DeviceId ?? "",
            SerialNumber:              telemetry?.SerialNumber ?? device.SerialNumber ?? "",
            CpuModel:                  cpu?.Model ?? "",
            CpuArchitecture:           cpu?.Architecture ?? "",
            CpuMaxClockSpeedKhz:       cpu?.MaxClockSpeed,
            RamTotalBytes:             memory?.TotalRamBytes,
            TotalDiskBytes:            storage?.TotalDiskBytes,
            DiskModels:                Join(latestSto?.Disk, d => d.Model),
            DiskTypes:                 Join(latestSto?.Disk, d => d.Type),
            DiskSizeBytes:             Join(latestSto?.Disk, d => d.SizeBytes?.ToString()),
            DiskHealths:               Join(latestSto?.Disk, d => d.Health),
            DiskManufacturers:         Join(latestSto?.Disk, d => d.Manufacturer),
            BatteryManufacturer:       battery?.Manufacturer ?? "",
            BatteryDesignCapacity:     battery?.DesignCapacity,
            BatteryFullChargeCapacity: latestBat?.FullChargeCapacity,
            BatteryHealth:             latestBat?.BatteryHealth ?? "",
            BatteryCycleCount:         latestBat?.CycleCount,
            NetworkMacAddresses:       Join(network, n => n.MacAddress),
            NetworkTypes:              Join(network, n => n.Type),
            GpuAdapter:                gpu?.Adapter ?? "",
            GpuDriverVersion:          gpu?.DriverVersion ?? "",
            OsUpdateState:             NormalizeOsUpdateState(latestOs?.UpdateState),
            OsLastRebootTime:          ToDto(latestOs?.LastRebootTimeRaw),
            AutoUpdateExpiration:      ToDto(device.AutoUpdateThrough),
            Manufacturer:              ManufacturerFromModel(device.Model),
            Model:                     device.Model ?? "",
            Status:                    device.Status ?? "",
            OrgUnitPath:               device.OrgUnitPath ?? "",
            AnnotatedUser:             device.AnnotatedUser ?? "",
            LastSyncUser:              device.RecentUsers?.FirstOrDefault()?.Email ?? "",
            AnnotatedLocation:         device.AnnotatedLocation ?? "",
            AnnotatedAssetId:          device.AnnotatedAssetId ?? "",
            EnrollmentTime:            ToDto(device.LastEnrollmentTimeRaw),
            OsVersion:                 device.OsVersion ?? "",
            LastSync:                  ToDto(device.LastSyncRaw),
            MacAddress:                device.MacAddress ?? "",
            EthernetMacAddress:        device.EthernetMacAddress ?? "",
            LastKnownIp:               device.LastKnownNetwork?.FirstOrDefault()?.IpAddress ?? "",
            OrderNumber:               device.OrderNumber != null ? string.Join(";", device.OrderNumber) : "",
            PlatformVersion:           device.PlatformVersion ?? "",
            FirmwareVersion:           device.FirmwareVersion ?? "",
            BootMode:                  device.BootMode ?? "",
            Notes:                     device.Notes ?? "",
            Meid:                      device.Meid ?? "");
    }

    private static readonly Dictionary<string, string> OsUpdateStateMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["OS_UP_TO_DATE"]                 = "Up To Date",
            ["OS_UPDATE_AVAILABLE"]           = "Update Available",
            ["OS_IMAGE_DOWNLOAD_IN_PROGRESS"] = "Downloading Update",
            ["OS_IMAGE_DOWNLOAD_NOT_STARTED"] = "Download Not Started",
            ["OS_UPDATE_NEED_REBOOT"]         = "Reboot Required",
        };

    internal static string NormalizeOsUpdateState(string? raw)
    {
        if (raw is null or "") return "";
        if (OsUpdateStateMap.TryGetValue(raw, out var display)) return display;
        var stripped = raw.StartsWith("OS_", StringComparison.OrdinalIgnoreCase)
            ? raw.Substring(3)
            : raw;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            stripped.Replace('_', ' ').ToLowerInvariant());
    }

    private static string ManufacturerFromModel(string? model)
        => model?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    private static DateTimeOffset? ToDto(string? raw)
        => raw is not null && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
               DateTimeStyles.RoundtripKind, out var dt) ? dt : null;

    private static string Join<T>(IList<T>? items, Func<T, string?> selector)
        => items is null or { Count: 0 } ? "" : string.Join(";", items.Select(i => selector(i) ?? ""));
}

public record HardwareRow(
    string ReportDate, string CustomerId, string DeviceId, string SerialNumber,
    string CpuModel, string CpuArchitecture, long? CpuMaxClockSpeedKhz,
    long? RamTotalBytes, long? TotalDiskBytes, string DiskModels, string DiskTypes,
    string DiskSizeBytes, string DiskHealths, string DiskManufacturers,
    string BatteryManufacturer, long? BatteryDesignCapacity, long? BatteryFullChargeCapacity,
    string BatteryHealth, long? BatteryCycleCount, string NetworkMacAddresses, string NetworkTypes,
    string GpuAdapter, string GpuDriverVersion, string OsUpdateState, DateTimeOffset? OsLastRebootTime,
    DateTimeOffset? AutoUpdateExpiration,
    string Manufacturer, string Model, string Status, string OrgUnitPath, string AnnotatedUser,
    string LastSyncUser, string AnnotatedLocation, string AnnotatedAssetId, DateTimeOffset? EnrollmentTime, string OsVersion,
    DateTimeOffset? LastSync, string MacAddress, string EthernetMacAddress, string LastKnownIp,
    string OrderNumber, string PlatformVersion, string FirmwareVersion,
    string BootMode, string Notes, string Meid)
{
    public double? RamGb  // binary GiB, labelled GB per IT/Flexera convention
        => RamTotalBytes.HasValue
            ? Math.Round(RamTotalBytes.Value / 1_073_741_824.0, 2)
            : null;

    public double? TotalDiskGb
        => TotalDiskBytes.HasValue
            ? Math.Round(TotalDiskBytes.Value / 1_073_741_824.0, 2)
            : null;

    public string DiskSizeGb  // RemoveEmptyEntries: assumes all disks report SizeBytes (true for Chrome OS telemetry)
        => string.Join(";",
            DiskSizeBytes
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s, out var b)
                    ? Math.Round(b / 1_073_741_824.0, 2).ToString(CultureInfo.InvariantCulture)
                    : ""));
}
