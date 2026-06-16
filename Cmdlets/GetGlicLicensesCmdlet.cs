// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Net;
using System.Runtime.CompilerServices;
using GLic.Auth;
using GLic.Helpers;
using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Admin.Directory.directory_v1.Data;
using Google.Apis.Licensing.v1.Data;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommon.Get, "GlicLicenses")]
[OutputType(typeof(LicenseRow))]
public sealed class GetGlicLicensesCmdlet : GlicCmdletBase
{
    [Parameter] public string[]? SkuIds { get; set; }

    protected override async Task RunAsync(CancellationToken ct)
    {
        var skuIdsFlag = SkuIds is null ? null : string.Join(",", SkuIds);
        var cfg = LoadConfig();
        var clients = await BuildClientsAsync(cfg);
        var skuFilePath = ConfigLocator.ResolveSkuReadPath(ResolvedConfigPath);
        var skus = SkuCatalog.Resolve(skuIdsFlag, skuFilePath);

        var reportDate = DateTime.Now.ToString("yyyy-MM-dd");
        var rows = GetRowsAsync(clients, cfg.CustomerId, skus, reportDate, ct);
        await EmitRowsAsync(rows, ct);
    }

    private async IAsyncEnumerable<LicenseRow> GetRowsAsync(
        ApiClients clients, string customer, IReadOnlyList<SkuEntry> skus,
        string reportDate, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var users = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        await foreach (var user in Paginator.FetchAllAsync<User>(async pageToken =>
        {
            var req = clients.Directory.Users.List();
            req.Customer = customer;
            req.MaxResults = 500;
            req.Projection = UsersResource.ListRequest.ProjectionEnum.Full;
            req.PageToken = pageToken;
            var resp = await req.ExecuteAsync(ct);
            return (resp.UsersValue, resp.NextPageToken);
        }, ct))
        {
            if (user.PrimaryEmail is not null)
                users[user.PrimaryEmail] = user;
        }

        foreach (var sku in skus)
        {
            List<LicenseAssignment> assignments = [];
            try
            {
                await foreach (var assignment in Paginator.FetchAllAsync<LicenseAssignment>(async pageToken =>
                {
                    var req = clients.Licensing.LicenseAssignments.ListForProductAndSku(
                        sku.ProductId, sku.SkuId, customer);
                    req.MaxResults = 1000;
                    req.PageToken = pageToken;
                    var resp = await req.ExecuteAsync(ct);
                    return (resp.Items, resp.NextPageToken);
                }, ct))
                {
                    assignments.Add(assignment);
                }
            }
            catch (Google.GoogleApiException ex)
                when (ex.HttpStatusCode is HttpStatusCode.NotFound
                                        or HttpStatusCode.Forbidden
                                        or HttpStatusCode.BadRequest)
            {
                continue;
            }
            catch (Google.GoogleApiException ex)
            {
                WriteWarning($"Skipping {sku.SkuId}: {ex.Message}");
                continue;
            }

            if (assignments.Count == 0)
            {
                WriteWarning(
                    $"SKU '{sku.SkuName}' ({sku.SkuId}) returned no individual assignments — skipped. " +
                    "If this is a domain-wide license, record it manually in Flexera.");
                continue;
            }

            foreach (var assignment in assignments)
            {
                users.TryGetValue(assignment.UserId ?? "", out var user);
                yield return BuildRow(assignment.UserId ?? "", reportDate, customer, sku, user);
            }
        }
    }

    internal static LicenseRow BuildRow(
        string userId, string reportDate, string customerId,
        SkuEntry sku, User? user, string assignmentStatus = "ACTIVE") =>
        new(
            ReportDate:       reportDate,
            CustomerId:       customerId,
            UserEmail:        userId,
            FullName:         user?.Name?.FullName ?? "",
            GivenName:        user?.Name?.GivenName ?? "",
            FamilyName:       user?.Name?.FamilyName ?? "",
            OrgUnit:          user?.OrgUnitPath ?? "",
            IsAdmin:          user?.IsAdmin,
            Suspended:        user?.Suspended,
            LastLoginTime:    ToDto(user?.LastLoginTimeRaw),
            ProductId:        sku.ProductId,
            ProductName:      sku.ProductName,
            SkuId:            sku.SkuId,
            SkuName:          sku.SkuName,
            AssignmentStatus: assignmentStatus);

    private static DateTimeOffset? ToDto(string? raw)
        => raw is not null && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
               DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
}

public record LicenseRow(
    string ReportDate, string CustomerId, string UserEmail, string FullName,
    string GivenName, string FamilyName, string OrgUnit, bool? IsAdmin, bool? Suspended,
    DateTimeOffset? LastLoginTime, string ProductId, string ProductName, string SkuId,
    string SkuName, string AssignmentStatus);
