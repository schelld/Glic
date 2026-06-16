// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Runtime.CompilerServices;
using GLic.Auth;
using GLic.Helpers;
using Google.Apis.ChromeManagement.v1;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommon.Get, "GlicDeviceApps")]
[OutputType(typeof(DeviceAppRow))]
public sealed class GetGlicDeviceAppsCmdlet : GlicCmdletBase
{
    [Parameter] public string? OrgUnit { get; set; }

    protected override async Task RunAsync(CancellationToken ct)
    {
        var cfg = LoadConfig();
        var clients = await BuildClientsAsync(cfg);
        var reportDate = DateTime.Now.ToString("yyyy-MM-dd");

        string? orgUnitId = null;
        if (OrgUnit is not null)
        {
            try
            {
                var ou = await clients.Directory.Orgunits
                    .Get(cfg.CustomerId, OrgUnit)
                    .ExecuteAsync(ct);
                orgUnitId = ou.OrgUnitId;
            }
            catch (Google.GoogleApiException ex) when ((int)ex.HttpStatusCode == 404)
            {
                throw new InvalidOperationException($"Org unit not found: {OrgUnit}", ex);
            }
        }

        var rows = GetRowsAsync(clients, cfg.CustomerId, orgUnitId, reportDate, ct);
        await EmitRowsAsync(rows, ct);
    }

    private async IAsyncEnumerable<DeviceAppRow> GetRowsAsync(
        ApiClients clients, string customer, string? orgUnitId, string reportDate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var app in Paginator.FetchAllAsync<GoogleChromeManagementV1InstalledApp>(
            async pageToken =>
            {
                var req = clients.ChromeManagement.Customers.Reports.CountInstalledApps($"customers/{customer}");
                req.PageToken = pageToken;
                req.PageSize  = 100;
                if (orgUnitId is not null) req.OrgUnitId = orgUnitId;
                var resp = await req.ExecuteAsync(ct);
                return (resp.InstalledApps, resp.NextPageToken);
            }, ct))
        {
            if (app.AppId is null) continue;

            var appTypeEnum = app.AppType switch
            {
                "EXTENSION"   => CustomersResource.ReportsResource.FindInstalledAppDevicesRequest.AppTypeEnum.EXTENSION,
                "APP"         => CustomersResource.ReportsResource.FindInstalledAppDevicesRequest.AppTypeEnum.APP,
                "THEME"       => CustomersResource.ReportsResource.FindInstalledAppDevicesRequest.AppTypeEnum.THEME,
                "HOSTED_APP"  => CustomersResource.ReportsResource.FindInstalledAppDevicesRequest.AppTypeEnum.HOSTEDAPP,
                "ANDROID_APP" => CustomersResource.ReportsResource.FindInstalledAppDevicesRequest.AppTypeEnum.ANDROIDAPP,
                _             => CustomersResource.ReportsResource.FindInstalledAppDevicesRequest.AppTypeEnum.APPTYPEUNSPECIFIED
            };

            await foreach (var device in Paginator.FetchAllAsync<GoogleChromeManagementV1Device>(
                async pageToken =>
                {
                    var req = clients.ChromeManagement.Customers.Reports.FindInstalledAppDevices($"customers/{customer}");
                    req.AppId     = app.AppId;
                    req.AppType   = appTypeEnum;
                    req.PageToken = pageToken;
                    req.PageSize  = 100;
                    if (orgUnitId is not null) req.OrgUnitId = orgUnitId;
                    var resp = await req.ExecuteAsync(ct);
                    return (resp.Devices, resp.NextPageToken);
                }, ct))
            {
                yield return BuildRow(app, device, reportDate, customer);
            }
        }
    }

    internal static DeviceAppRow BuildRow(
        GoogleChromeManagementV1InstalledApp app,
        GoogleChromeManagementV1Device device,
        string reportDate, string customerId)
        => new(
            ReportDate:  reportDate,
            CustomerId:  customerId,
            DeviceId:    device.DeviceId    ?? "",
            Machine:     device.Machine     ?? "",
            AppId:       app.AppId          ?? "",
            AppType:     app.AppType        ?? "",
            DisplayName: app.DisplayName    ?? "");
}

public record DeviceAppRow(
    string ReportDate, string CustomerId, string DeviceId, string Machine,
    string AppId, string AppType, string DisplayName);
