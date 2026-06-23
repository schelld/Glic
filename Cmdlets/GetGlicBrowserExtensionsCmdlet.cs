// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Runtime.CompilerServices;
using GLic.Auth;
using GLic.Helpers;
using Google.Apis.ChromeManagement.v1;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommon.Get, "GlicBrowserExtensions")]
[OutputType(typeof(BrowserExtensionRow))]
public sealed class GetGlicBrowserExtensionsCmdlet : GlicCmdletBase
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

    private async IAsyncEnumerable<BrowserExtensionRow> GetRowsAsync(
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
                "EXTENSION"   => CustomersResource.ReportsResource.FindInstalledAppProfilesRequest.AppTypeEnum.EXTENSION,
                "APP"         => CustomersResource.ReportsResource.FindInstalledAppProfilesRequest.AppTypeEnum.APP,
                "THEME"       => CustomersResource.ReportsResource.FindInstalledAppProfilesRequest.AppTypeEnum.THEME,
                "HOSTED_APP"  => CustomersResource.ReportsResource.FindInstalledAppProfilesRequest.AppTypeEnum.HOSTEDAPP,
                "ANDROID_APP" => CustomersResource.ReportsResource.FindInstalledAppProfilesRequest.AppTypeEnum.ANDROIDAPP,
                _             => CustomersResource.ReportsResource.FindInstalledAppProfilesRequest.AppTypeEnum.APPTYPEUNSPECIFIED
            };

            await foreach (var profile in Paginator.FetchAllAsync<GoogleChromeManagementV1ProfileAppInstallInstance>(
                async pageToken =>
                {
                    var req = clients.ChromeManagement.Customers.Reports.FindInstalledAppProfiles($"customers/{customer}");
                    req.AppId     = app.AppId;
                    req.AppType   = appTypeEnum;
                    req.PageToken = pageToken;
                    req.PageSize  = 100;
                    if (orgUnitId is not null) req.OrgUnitId = orgUnitId;
                    var resp = await req.ExecuteAsync(ct);
                    return (resp.Profiles, resp.NextPageToken);
                }, ct))
            {
                yield return BuildRow(app, profile, reportDate, customer);
            }
        }
    }

    internal static BrowserExtensionRow BuildRow(
        GoogleChromeManagementV1InstalledApp app,
        GoogleChromeManagementV1ProfileAppInstallInstance profile,
        string reportDate, string customerId)
        => new(
            ReportDate:         reportDate,
            CustomerId:         customerId,
            ProfilePermanentId: profile.ProfilePermanentId ?? "",
            ProfileId:          profile.ProfileId          ?? "",
            Email:              profile.Email              ?? "",
            ProfileOrgUnitId:   profile.ProfileOrgUnitId   ?? "",
            AppId:              app.AppId                  ?? "",
            AppType:            app.AppType                ?? "",
            DisplayName:        app.DisplayName            ?? "");
}

public sealed class BrowserExtensionRow
{
    public string ReportDate         { get; }
    public string CustomerId         { get; }
    public string ProfilePermanentId { get; }
    public string ProfileId          { get; }
    public string Email              { get; }
    public string ProfileOrgUnitId   { get; }
    public string AppId              { get; }
    public string AppType            { get; }
    public string DisplayName        { get; }

    public BrowserExtensionRow(
        string ReportDate, string CustomerId, string ProfilePermanentId, string ProfileId,
        string Email, string ProfileOrgUnitId, string AppId, string AppType, string DisplayName)
    {
        this.ReportDate         = ReportDate;
        this.CustomerId         = CustomerId;
        this.ProfilePermanentId = ProfilePermanentId;
        this.ProfileId          = ProfileId;
        this.Email              = Email;
        this.ProfileOrgUnitId   = ProfileOrgUnitId;
        this.AppId              = AppId;
        this.AppType            = AppType;
        this.DisplayName        = DisplayName;
    }
}
