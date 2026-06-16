// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Runtime.CompilerServices;
using GLic.Auth;
using GLic.Helpers;
using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Admin.Directory.directory_v1.Data;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommon.Get, "GlicDevices")]
[OutputType(typeof(DeviceRow))]
public sealed class GetGlicDevicesCmdlet : GlicCmdletBase
{
    [Parameter]
    [ValidateSet("all", "active", "deprovisioned", "disabled", IgnoreCase = true)]
    public string Status { get; set; } = "active";

    protected override async Task RunAsync(CancellationToken ct)
    {
        var cfg = LoadConfig();
        var clients = await BuildClientsAsync(cfg);
        var reportDate = DateTime.Now.ToString("yyyy-MM-dd");
        var rows = GetRowsAsync(clients, cfg.CustomerId, Status, reportDate, ct);
        await EmitRowsAsync(rows, ct);
    }

    private async IAsyncEnumerable<DeviceRow> GetRowsAsync(
        ApiClients clients, string customer, string status, string reportDate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var device in Paginator.FetchAllAsync<ChromeOsDevice>(
            async pageToken =>
            {
                var req = clients.Directory.Chromeosdevices.List(customer);
                req.Projection = ChromeosdevicesResource.ListRequest.ProjectionEnum.FULL;
                if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
                    req.Query = $"status:{status.ToUpperInvariant()}";
                req.PageToken = pageToken;
                req.MaxResults = 200;
                var resp = await req.ExecuteAsync(ct);
                return (resp.Chromeosdevices, resp.NextPageToken);
            }, ct))
        {
            yield return BuildRow(device, reportDate, customer);
        }
    }

    internal static DeviceRow BuildRow(ChromeOsDevice device, string reportDate, string customer)
        => new(
            ReportDate:         reportDate,
            CustomerId:         customer,
            DeviceId:           device.DeviceId ?? "",
            SerialNumber:       device.SerialNumber ?? "",
            Model:              device.Model ?? "",
            Status:             device.Status ?? "",
            OrgUnitPath:        device.OrgUnitPath ?? "",
            AnnotatedUser:      device.AnnotatedUser ?? "",
            LastSyncUser:       device.RecentUsers?.FirstOrDefault()?.Email ?? "",
            AnnotatedLocation:  device.AnnotatedLocation ?? "",
            LastSync:           ToDto(device.LastSyncRaw),
            EnrollmentTime:     ToDto(device.LastEnrollmentTimeRaw),
            OsVersion:          device.OsVersion ?? "",
            MacAddress:         device.MacAddress ?? "",
            EthernetMacAddress: device.EthernetMacAddress ?? "",
            LastKnownIp:        device.LastKnownNetwork?.FirstOrDefault()?.IpAddress ?? "",
            AnnotatedAssetId:   device.AnnotatedAssetId ?? "",
            OrderNumber:        device.OrderNumber != null ? string.Join(",", device.OrderNumber) : "",
            PlatformVersion:    device.PlatformVersion ?? "",
            FirmwareVersion:    device.FirmwareVersion ?? "",
            BootMode:           device.BootMode ?? "",
            Notes:              device.Notes ?? "",
            Meid:               device.Meid ?? "");

    private static DateTimeOffset? ToDto(string? raw)
        => raw is not null && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
               DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
}

public record DeviceRow(
    string ReportDate, string CustomerId, string DeviceId, string SerialNumber,
    string Model, string Status, string OrgUnitPath, string AnnotatedUser,
    string LastSyncUser, string AnnotatedLocation, DateTimeOffset? LastSync, DateTimeOffset? EnrollmentTime,
    string OsVersion, string MacAddress, string EthernetMacAddress, string LastKnownIp,
    string AnnotatedAssetId, string OrderNumber, string PlatformVersion, string FirmwareVersion,
    string BootMode, string Notes, string Meid);
