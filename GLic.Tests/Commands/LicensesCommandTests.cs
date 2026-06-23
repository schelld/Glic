// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using GLic.Helpers;
using Google.Apis.Admin.Directory.directory_v1.Data;

namespace GLic.Tests.Commands;

public class LicensesCommandTests
{
    [Fact]
    public void BuildRow_WithMatchingUser_MapsDirectoryFields()
    {
        var sku = new SkuEntry("Google-Apps", "Google Workspace (Legacy)", "Google-Apps-Unlimited", "Business Starter");
        var user = new User
        {
            Name = new UserName { FullName = "Jane Doe", GivenName = "Jane", FamilyName = "Doe" },
            OrgUnitPath = "/Sales",
            IsAdmin = false,
            Suspended = false,
            LastLoginTimeRaw = "2026-06-01T10:00:00.000Z"
        };

        var row = GetGlicLicensesCmdlet.BuildRow("jdoe@example.com", "2026-06-05", "C03fxe4vs", sku, user);

        Assert.Equal("jdoe@example.com", row.UserEmail);
        Assert.Equal("Jane Doe",  row.FullName);
        Assert.Equal("Jane",      row.GivenName);
        Assert.Equal("Doe",       row.FamilyName);
        Assert.Equal("/Sales",    row.OrgUnit);
        Assert.Equal(false,                                                   row.IsAdmin);
        Assert.Equal(false,                                                   row.Suspended);
        Assert.Equal(DateTimeOffset.Parse("2026-06-01T10:00:00.000Z"),       row.LastLoginTime);
        Assert.Equal("Google-Apps",              row.ProductId);
        Assert.Equal("Google Workspace (Legacy)", row.ProductName);
        Assert.Equal("Google-Apps-Unlimited",    row.SkuId);
        Assert.Equal("Business Starter",         row.SkuName);
        Assert.Equal("ACTIVE",                   row.AssignmentStatus);
    }

    [Fact]
    public void BuildRow_WithNullUser_EmitsEmptyDirectoryFields()
    {
        var sku = new SkuEntry("Google-Apps", "Google Workspace (Legacy)", "Google-Apps-Unlimited", "Business Starter");

        var row = GetGlicLicensesCmdlet.BuildRow("deleted@example.com", "2026-06-05", "C03fxe4vs", sku, null);

        Assert.Equal("deleted@example.com", row.UserEmail);
        Assert.Equal("", row.FullName);
        Assert.Equal("", row.GivenName);
        Assert.Equal("", row.FamilyName);
        Assert.Equal("", row.OrgUnit);
        Assert.Null(row.IsAdmin);
        Assert.Null(row.Suspended);
        Assert.Null(row.LastLoginTime);
        Assert.Equal("ACTIVE", row.AssignmentStatus);
    }

    [Fact]
    public void BuildRow_DefaultAssignmentStatus_IsActive()
    {
        var sku = new SkuEntry("Google-Apps", "Google Workspace (Legacy)", "Google-Apps-Unlimited", "Business Starter");

        var row = GetGlicLicensesCmdlet.BuildRow("user@example.com", "2026-06-10", "C03fxe4vs", sku, null);

        Assert.Equal("ACTIVE", row.AssignmentStatus);
    }
}
