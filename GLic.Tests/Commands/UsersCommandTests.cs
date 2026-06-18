// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using Google.Apis.Admin.Directory.directory_v1.Data;

namespace GLic.Tests.Commands;

public class UsersCommandTests
{
    [Fact]
    public void BuildRow_FullyPopulatedUser_MapsAllFields()
    {
        var user = new User
        {
            PrimaryEmail     = "jdoe@example.com",
            Name             = new UserName { FullName = "Jane Doe", GivenName = "Jane", FamilyName = "Doe" },
            CreationTimeRaw  = "2022-01-15T08:30:00.000Z",
            LastLoginTimeRaw = "2026-06-01T10:00:00.000Z",
            IsEnrolledIn2Sv  = true,
            IsEnforcedIn2Sv  = false,
            RecoveryEmail    = "jane@personal.com",
            RecoveryPhone    = "+15555551234",
            OrgUnitPath      = "/Staff",
            IsAdmin          = false,
            IsDelegatedAdmin = false,
            Suspended        = false,
            Archived         = false,
            Organizations    = [new UserOrganization { Department = "Engineering", Title = "Engineer", CostCenter = "CC-100", Primary = true }],
            ExternalIds      = [new UserExternalId { Type = "organization", Value = "EMP001" }],
            Relations        = [new UserRelation { Type = "manager", Value = "boss@example.com" }],
            Aliases          = ["j.doe@example.com", "jane@example.com"],
        };

        var row = GetGlicUsersCmdlet.BuildRow(user, "2026-06-06", "C03fxe4vs");

        Assert.Equal("jdoe@example.com",          row.PrimaryEmail);
        Assert.Equal("Jane Doe",                  row.FullName);
        Assert.Equal("Jane",                      row.GivenName);
        Assert.Equal("Doe",                       row.FamilyName);
        Assert.Equal(DateTimeOffset.Parse("2022-01-15T08:30:00.000Z"), row.CreationTime);
        Assert.Equal(DateTimeOffset.Parse("2026-06-01T10:00:00.000Z"), row.LastLoginTime);
        Assert.Equal(true,  row.IsEnrolledIn2Sv);
        Assert.Equal(false, row.IsEnforcedIn2Sv);
        Assert.Equal("jane@personal.com",         row.RecoveryEmail);
        Assert.Equal("+15555551234",              row.RecoveryPhone);
        Assert.Equal("/Staff",                    row.OrgUnit);
        Assert.Equal(false, row.IsAdmin);
        Assert.Equal(false, row.IsDelegatedAdmin);
        Assert.Equal(false, row.Suspended);
        Assert.Equal(false, row.Archived);
        Assert.Equal("Engineering",               row.Department);
        Assert.Equal("Engineer",                  row.JobTitle);
        Assert.Equal("CC-100",                    row.CostCenter);
        Assert.Equal("EMP001",                    row.EmployeeId);
        Assert.Equal("boss@example.com",          row.ManagerEmail);
        Assert.Equal("j.doe@example.com;jane@example.com", row.Aliases);
    }

    [Fact]
    public void BuildRow_NullCollections_EmitEmptyStrings()
    {
        var user = new User
        {
            PrimaryEmail  = "sparse@example.com",
            Organizations = null,
            ExternalIds   = null,
            Relations     = null,
            Aliases       = null,
        };

        var row = GetGlicUsersCmdlet.BuildRow(user, "2026-06-06", "C03fxe4vs");

        Assert.Equal("", row.Department);
        Assert.Equal("", row.JobTitle);
        Assert.Equal("", row.CostCenter);
        Assert.Equal("", row.EmployeeId);
        Assert.Equal("", row.ManagerEmail);
        Assert.Equal("", row.Aliases);
    }

    [Fact]
    public void BuildRow_MultipleAliases_JoinedWithSemicolon()
    {
        var user = new User
        {
            PrimaryEmail = "jdoe@example.com",
            Aliases      = ["a@example.com", "b@example.com", "c@example.com"],
        };

        var row = GetGlicUsersCmdlet.BuildRow(user, "2026-06-06", "C03fxe4vs");

        Assert.Equal("a@example.com;b@example.com;c@example.com", row.Aliases);
    }

    [Fact]
    public void BuildRow_PrimaryOrgPreferredOverFirst()
    {
        var user = new User
        {
            PrimaryEmail  = "jdoe@example.com",
            Organizations =
            [
                new UserOrganization { Department = "First",   Title = "First Title",   Primary = false },
                new UserOrganization { Department = "Primary", Title = "Primary Title", Primary = true  },
            ],
        };

        var row = GetGlicUsersCmdlet.BuildRow(user, "2026-06-06", "C03fxe4vs");

        Assert.Equal("Primary",       row.Department);
        Assert.Equal("Primary Title", row.JobTitle);
    }

    [Fact]
    public void BuildRow_NoPrimaryOrg_FallsBackToFirst()
    {
        var user = new User
        {
            PrimaryEmail  = "jdoe@example.com",
            Organizations =
            [
                new UserOrganization { Department = "First",  Title = "First Title",  Primary = false },
                new UserOrganization { Department = "Second", Title = "Second Title", Primary = false },
            ],
        };

        var row = GetGlicUsersCmdlet.BuildRow(user, "2026-06-06", "C03fxe4vs");

        Assert.Equal("First",       row.Department);
        Assert.Equal("First Title", row.JobTitle);
    }

    [Fact]
    public void BuildRow_NoManagerRelation_EmitsEmptyManagerEmail()
    {
        var user = new User
        {
            PrimaryEmail = "jdoe@example.com",
            Relations    = [new UserRelation { Type = "assistant", Value = "helper@example.com" }],
        };

        var row = GetGlicUsersCmdlet.BuildRow(user, "2026-06-06", "C03fxe4vs");

        Assert.Equal("", row.ManagerEmail);
    }

    [Fact]
    public void BuildRow_NoOrganizationExternalId_EmitsEmptyEmployeeId()
    {
        var user = new User
        {
            PrimaryEmail = "jdoe@example.com",
            ExternalIds  = [new UserExternalId { Type = "custom", Value = "CUSTOM001" }],
        };

        var row = GetGlicUsersCmdlet.BuildRow(user, "2026-06-06", "C03fxe4vs");

        Assert.Equal("", row.EmployeeId);
    }
}
