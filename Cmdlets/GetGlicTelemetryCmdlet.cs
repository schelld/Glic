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

[Cmdlet(VerbsCommon.Get, "GlicTelemetry")]
[OutputType(typeof(TelemetryRow))]
public sealed class GetGlicTelemetryCmdlet : GlicCmdletBase
{
    [Parameter]
    [ValidateSet("all", "active", "deprovisioned", "disabled", IgnoreCase = true)]
    public string Status { get; set; } = "active";
    [Parameter] public string? OrgUnit { get; set; }

    protected override async Task RunAsync(CancellationToken ct)
    {
        var cfg = LoadConfig();
        var clients = await BuildClientsAsync(cfg);
        var rows = GetRowsAsync(clients, cfg.CustomerId, Status, OrgUnit, ct);
        await EmitRowsAsync(rows, ct);
    }

    private async IAsyncEnumerable<TelemetryRow> GetRowsAsync(
        ApiClients clients, string customer, string status, string? orgUnit,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var telemetryDict = new Dictionary<string, GoogleChromeManagementV1TelemetryDevice>(
            StringComparer.OrdinalIgnoreCase);

        var parent = $"customers/{customer}";
        await foreach (var td in Paginator.FetchAllAsync<GoogleChromeManagementV1TelemetryDevice>(
            async pageToken =>
            {
                var req = clients.ChromeManagement.Customers.Telemetry.Devices.List(parent);
                req.ReadMask  = "name,device_id,serial_number,os_update_status";
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
            yield return BuildRow(device, telemetry);
        }
    }

    internal static TelemetryRow BuildRow(
        ChromeOsDevice device,
        GoogleChromeManagementV1TelemetryDevice? telemetry)
    {
        var latestOs = telemetry?.OsUpdateStatus?.MaxBy(r => r.LastUpdateCheckTimeRaw);
        return new TelemetryRow(
            DeviceId:             telemetry?.DeviceId     ?? device.DeviceId     ?? "",
            SerialNumber:         telemetry?.SerialNumber ?? device.SerialNumber ?? "",
            Status:               device.Status           ?? "",
            OrgUnitPath:          device.OrgUnitPath      ?? "",
            AnnotatedUser:        device.AnnotatedUser    ?? "",
            OsVersion:            device.OsVersion        ?? "",
            PlatformVersion:      device.PlatformVersion  ?? "",
            FirmwareVersion:      device.FirmwareVersion  ?? "",
            AutoUpdateExpiration: ToDto(device.AutoUpdateThrough),
            UpdateState:          latestOs?.UpdateState            ?? "",
            LastUpdateCheckTime:  ToDto(latestOs?.LastUpdateCheckTimeRaw),
            LastUpdateTime:       ToDto(latestOs?.LastUpdateTimeRaw),
            LastRebootTime:       ToDto(latestOs?.LastRebootTimeRaw),
            NewPlatformVersion:   latestOs?.NewPlatformVersion     ?? "");
    }

    private static DateTimeOffset? ToDto(string? raw)
        => raw is not null && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
               DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
}

public record TelemetryRow(
    string DeviceId, string SerialNumber, string Status, string OrgUnitPath,
    string AnnotatedUser, string OsVersion, string PlatformVersion, string FirmwareVersion,
    DateTimeOffset? AutoUpdateExpiration, string UpdateState, DateTimeOffset? LastUpdateCheckTime,
    DateTimeOffset? LastUpdateTime, DateTimeOffset? LastRebootTime, string NewPlatformVersion);
