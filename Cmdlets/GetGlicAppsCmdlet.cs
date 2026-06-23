// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Runtime.CompilerServices;
using GLic.Auth;
using GLic.Helpers;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommon.Get, "GlicApps")]
[OutputType(typeof(AppRow))]
public sealed class GetGlicAppsCmdlet : GlicCmdletBase
{
    protected override async Task RunAsync(CancellationToken ct)
    {
        var cfg = LoadConfig();
        var clients = await BuildClientsAsync(cfg);
        var reportDate = DateTime.Now.ToString("yyyy-MM-dd");
        var rows = GetRowsAsync(clients, cfg.CustomerId, reportDate, ct);
        await EmitRowsAsync(rows, ct);
    }

    private async IAsyncEnumerable<AppRow> GetRowsAsync(
        ApiClients clients, string customer, string reportDate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var app in Paginator.FetchAllAsync<GoogleChromeManagementV1InstalledApp>(
            async pageToken =>
            {
                var req = clients.ChromeManagement.Customers.Reports.CountInstalledApps($"customers/{customer}");
                req.PageToken = pageToken;
                req.PageSize = 100;
                var resp = await req.ExecuteAsync(ct);
                return (resp.InstalledApps, resp.NextPageToken);
            }, ct))
        {
            yield return BuildRow(app, reportDate, customer);
        }
    }

    internal static AppRow BuildRow(
        GoogleChromeManagementV1InstalledApp app,
        string reportDate, string customerId)
        => new(
            ReportDate:         reportDate,
            CustomerId:         customerId,
            DisplayName:        app.DisplayName ?? "",
            AppId:              app.AppId ?? "",
            AppType:            app.AppType ?? "",
            Publisher:          "",
            BrowserDeviceCount: app.BrowserDeviceCount ?? 0L);
}

// Publisher is intentionally empty: countInstalledApps does not return publisher data.
public sealed class AppRow
{
    public string ReportDate         { get; }
    public string CustomerId         { get; }
    public string DisplayName        { get; }
    public string AppId              { get; }
    public string AppType            { get; }
    public string Publisher          { get; }
    public long   BrowserDeviceCount { get; }

    public AppRow(
        string ReportDate, string CustomerId, string DisplayName,
        string AppId, string AppType, string Publisher, long BrowserDeviceCount)
    {
        this.ReportDate         = ReportDate;
        this.CustomerId         = CustomerId;
        this.DisplayName        = DisplayName;
        this.AppId              = AppId;
        this.AppType            = AppType;
        this.Publisher          = Publisher;
        this.BrowserDeviceCount = BrowserDeviceCount;
    }
}
