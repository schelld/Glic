// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Runtime.CompilerServices;
using GLic.Auth;
using GLic.Helpers;
using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Admin.Directory.directory_v1.Data;

namespace GLic.Cmdlets;

public enum SuspendedFilter { All, Active, Suspended }

[Cmdlet(VerbsCommon.Get, "GlicUsers")]
[OutputType(typeof(UserRow))]
public sealed class GetGlicUsersCmdlet : GlicCmdletBase
{
    [Parameter] public string? OrgUnit { get; set; }
    [Parameter]
    [ValidateSet("Active", "All", "Suspended", IgnoreCase = true)]
    public string Suspended { get; set; } = "Active";

    protected override async Task RunAsync(CancellationToken ct)
    {
        var suspendedFilter = Suspended.ToLowerInvariant() switch
        {
            "active"    => SuspendedFilter.Active,
            "suspended" => SuspendedFilter.Suspended,
            _           => SuspendedFilter.All,
        };

        var cfg = LoadConfig();
        var clients = await BuildClientsAsync(cfg);
        var reportDate = DateTime.Now.ToString("yyyy-MM-dd");
        var rows = GetRowsAsync(clients, cfg.CustomerId, OrgUnit, suspendedFilter, reportDate, ct);
        await EmitRowsAsync(rows, ct);
    }

    private async IAsyncEnumerable<UserRow> GetRowsAsync(
        ApiClients clients, string customer, string? orgUnit, SuspendedFilter suspendedFilter,
        string reportDate, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var user in Paginator.FetchAllAsync<User>(async pageToken =>
        {
            var req = clients.Directory.Users.List();
            req.Customer = customer;
            req.MaxResults = 500;
            req.Projection = UsersResource.ListRequest.ProjectionEnum.Full;
            req.PageToken = pageToken;
            if (orgUnit is not null)
                req.Query = $"orgUnitPath='{orgUnit.Replace("'", "\\'")}'";
            var resp = await req.ExecuteAsync(ct);
            return (resp.UsersValue, resp.NextPageToken);
        }, ct))
        {
            if (suspendedFilter == SuspendedFilter.Active    && user.Suspended == true) continue;
            if (suspendedFilter == SuspendedFilter.Suspended && user.Suspended != true) continue;
            yield return BuildRow(user, reportDate, customer);
        }
    }

    internal static UserRow BuildRow(User user, string reportDate, string customerId)
    {
        var org = user.Organizations?.FirstOrDefault(o => o.Primary == true)
                  ?? user.Organizations?.FirstOrDefault();
        var managerEmail = user.Relations?.FirstOrDefault(r => r.Type == "manager")?.Value ?? "";
        var employeeId   = user.ExternalIds?.FirstOrDefault(e => e.Type == "organization")?.Value ?? "";
        var aliases      = string.Join(";", user.Aliases ?? []);

        return new UserRow(
            ReportDate:       reportDate,
            CustomerId:       customerId,
            PrimaryEmail:     user.PrimaryEmail ?? "",
            FullName:         user.Name?.FullName ?? "",
            GivenName:        user.Name?.GivenName ?? "",
            FamilyName:       user.Name?.FamilyName ?? "",
            CreationTime:     ToDto(user.CreationTimeRaw),
            LastLoginTime:    ToDto(user.LastLoginTimeRaw),
            IsEnrolledIn2Sv:  user.IsEnrolledIn2Sv,
            IsEnforcedIn2Sv:  user.IsEnforcedIn2Sv,
            RecoveryEmail:    user.RecoveryEmail ?? "",
            RecoveryPhone:    user.RecoveryPhone ?? "",
            OrgUnit:          user.OrgUnitPath ?? "",
            IsAdmin:          user.IsAdmin,
            IsDelegatedAdmin: user.IsDelegatedAdmin,
            Suspended:        user.Suspended,
            Archived:         user.Archived,
            Department:       org?.Department ?? "",
            JobTitle:         org?.Title ?? "",
            CostCenter:       org?.CostCenter ?? "",
            EmployeeId:       employeeId,
            ManagerEmail:     managerEmail,
            Aliases:          aliases);
    }

    private static DateTimeOffset? ToDto(string? raw)
        => raw is not null && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
               DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
}

public sealed class UserRow
{
    public string          ReportDate       { get; }
    public string          CustomerId       { get; }
    public string          PrimaryEmail     { get; }
    public string          FullName         { get; }
    public string          GivenName        { get; }
    public string          FamilyName       { get; }
    public DateTimeOffset? CreationTime     { get; }
    public DateTimeOffset? LastLoginTime    { get; }
    public bool?           IsEnrolledIn2Sv  { get; }
    public bool?           IsEnforcedIn2Sv  { get; }
    public string          RecoveryEmail    { get; }
    public string          RecoveryPhone    { get; }
    public string          OrgUnit          { get; }
    public bool?           IsAdmin          { get; }
    public bool?           IsDelegatedAdmin { get; }
    public bool?           Suspended        { get; }
    public bool?           Archived         { get; }
    public string          Department       { get; }
    public string          JobTitle         { get; }
    public string          CostCenter       { get; }
    public string          EmployeeId       { get; }
    public string          ManagerEmail     { get; }
    public string          Aliases          { get; }

    public UserRow(
        string ReportDate, string CustomerId, string PrimaryEmail, string FullName,
        string GivenName, string FamilyName, DateTimeOffset? CreationTime, DateTimeOffset? LastLoginTime,
        bool? IsEnrolledIn2Sv, bool? IsEnforcedIn2Sv, string RecoveryEmail, string RecoveryPhone,
        string OrgUnit, bool? IsAdmin, bool? IsDelegatedAdmin, bool? Suspended, bool? Archived,
        string Department, string JobTitle, string CostCenter, string EmployeeId,
        string ManagerEmail, string Aliases)
    {
        this.ReportDate       = ReportDate;
        this.CustomerId       = CustomerId;
        this.PrimaryEmail     = PrimaryEmail;
        this.FullName         = FullName;
        this.GivenName        = GivenName;
        this.FamilyName       = FamilyName;
        this.CreationTime     = CreationTime;
        this.LastLoginTime    = LastLoginTime;
        this.IsEnrolledIn2Sv  = IsEnrolledIn2Sv;
        this.IsEnforcedIn2Sv  = IsEnforcedIn2Sv;
        this.RecoveryEmail    = RecoveryEmail;
        this.RecoveryPhone    = RecoveryPhone;
        this.OrgUnit          = OrgUnit;
        this.IsAdmin          = IsAdmin;
        this.IsDelegatedAdmin = IsDelegatedAdmin;
        this.Suspended        = Suspended;
        this.Archived         = Archived;
        this.Department       = Department;
        this.JobTitle         = JobTitle;
        this.CostCenter       = CostCenter;
        this.EmployeeId       = EmployeeId;
        this.ManagerEmail     = ManagerEmail;
        this.Aliases          = Aliases;
    }
}
