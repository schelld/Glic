// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Runtime.CompilerServices;
using GLic.Auth;
using GLic.Helpers;
using Google.Apis.ChromeManagement.v1.Data;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommon.Get, "GlicManagedBrowsers")]
[OutputType(typeof(ManagedBrowserRow))]
public sealed class GetGlicManagedBrowsersCmdlet : GlicCmdletBase
{
    [Parameter] public string? OrgUnit { get; set; }

    protected override async Task RunAsync(CancellationToken ct)
    {
        var cfg = LoadConfig();
        var clients = await BuildClientsAsync(cfg);
        var reportDate = DateTime.Now.ToString("yyyy-MM-dd");

        string? filter = null;
        if (OrgUnit is not null)
        {
            try
            {
                var ou = await clients.Directory.Orgunits
                    .Get(cfg.CustomerId, OrgUnit)
                    .ExecuteAsync(ct);
                filter = $"org_unit_id = \"{ou.OrgUnitId}\"";
            }
            catch (Google.GoogleApiException ex) when ((int)ex.HttpStatusCode == 404)
            {
                throw new InvalidOperationException($"Org unit not found: {OrgUnit}", ex);
            }
        }

        try
        {
            await EmitRowsAsync(GetRowsAsync(clients, cfg.CustomerId, filter, reportDate, ct), ct);
        }
        catch (Google.GoogleApiException ex) when (filter is not null && (int)ex.HttpStatusCode == 400)
        {
            WriteWarning("Org unit filter not supported — returning all profiles");
            await EmitRowsAsync(GetRowsAsync(clients, cfg.CustomerId, null, reportDate, ct), ct);
        }
    }

    private async IAsyncEnumerable<ManagedBrowserRow> GetRowsAsync(
        ApiClients clients, string customer, string? filter, string reportDate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var profile in Paginator.FetchAllAsync<GoogleChromeManagementVersionsV1ChromeBrowserProfile>(
            async pageToken =>
            {
                var req = clients.ChromeManagement.Customers.Profiles.List($"customers/{customer}");
                req.PageToken = pageToken;
                req.PageSize  = 100;
                if (filter is not null)
                    req.Filter = filter;
                var resp = await req.ExecuteAsync(ct);
                return (resp.ChromeBrowserProfiles, resp.NextPageToken);
            }, ct))
        {
            yield return BuildRow(profile, reportDate, customer);
        }
    }

    internal static ManagedBrowserRow BuildRow(
        GoogleChromeManagementVersionsV1ChromeBrowserProfile profile,
        string reportDate, string customerId)
        => new(
            ReportDate:           reportDate,
            CustomerId:           customerId,
            ProfilePermanentId:   profile.ProfilePermanentId   ?? "",
            ProfileId:            profile.ProfileId            ?? "",
            DisplayName:          profile.DisplayName          ?? "",
            UserEmail:            profile.UserEmail            ?? "",
            UserId:               profile.UserId               ?? "",
            AnnotatedUser:        profile.AnnotatedUser        ?? "",
            BrowserVersion:       profile.BrowserVersion       ?? "",
            BrowserChannel:       profile.BrowserChannel       ?? "",
            OsPlatformType:       profile.OsPlatformType       ?? "",
            OsVersion:            profile.OsVersion            ?? "",
            OsPlatformVersion:    profile.OsPlatformVersion    ?? "",
            Hostname:             profile.DeviceInfo?.Hostname           ?? "",
            Machine:              profile.DeviceInfo?.Machine            ?? "",
            DeviceType:           profile.DeviceInfo?.DeviceType         ?? "",
            AffiliatedDeviceId:   profile.DeviceInfo?.AffiliatedDeviceId ?? "",
            AnnotatedLocation:    profile.AnnotatedLocation    ?? "",
            ExtensionCount:       profile.ExtensionCount,
            PolicyCount:          profile.PolicyCount,
            FirstEnrollmentTime:  ToDto(profile.FirstEnrollmentTimeRaw),
            LastActivityTime:     ToDto(profile.LastActivityTimeRaw),
            LastPolicySyncTime:   ToDto(profile.LastPolicySyncTimeRaw),
            LastStatusReportTime: ToDto(profile.LastStatusReportTimeRaw));

    private static DateTimeOffset? ToDto(string? raw)
        => raw is not null && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
               DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
}

public sealed class ManagedBrowserRow
{
    public string          ReportDate           { get; }
    public string          CustomerId           { get; }
    public string          ProfilePermanentId   { get; }
    public string          ProfileId            { get; }
    public string          DisplayName          { get; }
    public string          UserEmail            { get; }
    public string          UserId               { get; }
    public string          AnnotatedUser        { get; }
    public string          BrowserVersion       { get; }
    public string          BrowserChannel       { get; }
    public string          OsPlatformType       { get; }
    public string          OsVersion            { get; }
    public string          OsPlatformVersion    { get; }
    public string          Hostname             { get; }
    public string          Machine              { get; }
    public string          DeviceType           { get; }
    public string          AffiliatedDeviceId   { get; }
    public string          AnnotatedLocation    { get; }
    public long?           ExtensionCount       { get; }
    public long?           PolicyCount          { get; }
    public DateTimeOffset? FirstEnrollmentTime  { get; }
    public DateTimeOffset? LastActivityTime     { get; }
    public DateTimeOffset? LastPolicySyncTime   { get; }
    public DateTimeOffset? LastStatusReportTime { get; }

    public ManagedBrowserRow(
        string ReportDate, string CustomerId, string ProfilePermanentId, string ProfileId,
        string DisplayName, string UserEmail, string UserId, string AnnotatedUser,
        string BrowserVersion, string BrowserChannel, string OsPlatformType, string OsVersion,
        string OsPlatformVersion, string Hostname, string Machine, string DeviceType,
        string AffiliatedDeviceId, string AnnotatedLocation, long? ExtensionCount, long? PolicyCount,
        DateTimeOffset? FirstEnrollmentTime, DateTimeOffset? LastActivityTime,
        DateTimeOffset? LastPolicySyncTime, DateTimeOffset? LastStatusReportTime)
    {
        this.ReportDate           = ReportDate;
        this.CustomerId           = CustomerId;
        this.ProfilePermanentId   = ProfilePermanentId;
        this.ProfileId            = ProfileId;
        this.DisplayName          = DisplayName;
        this.UserEmail            = UserEmail;
        this.UserId               = UserId;
        this.AnnotatedUser        = AnnotatedUser;
        this.BrowserVersion       = BrowserVersion;
        this.BrowserChannel       = BrowserChannel;
        this.OsPlatformType       = OsPlatformType;
        this.OsVersion            = OsVersion;
        this.OsPlatformVersion    = OsPlatformVersion;
        this.Hostname             = Hostname;
        this.Machine              = Machine;
        this.DeviceType           = DeviceType;
        this.AffiliatedDeviceId   = AffiliatedDeviceId;
        this.AnnotatedLocation    = AnnotatedLocation;
        this.ExtensionCount       = ExtensionCount;
        this.PolicyCount          = PolicyCount;
        this.FirstEnrollmentTime  = FirstEnrollmentTime;
        this.LastActivityTime     = LastActivityTime;
        this.LastPolicySyncTime   = LastPolicySyncTime;
        this.LastStatusReportTime = LastStatusReportTime;
    }
}
