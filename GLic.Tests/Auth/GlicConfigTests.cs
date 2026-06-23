// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Auth;

namespace GLic.Tests.Auth;

public class GlicConfigTests
{
    [Fact]
    public void Load_WithValidFile_ReturnsConfig()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"customer_id":"C03fxe4vs","admin_email":"admin@example.com"}""");

            var config = GlicConfig.Load(path);

            Assert.Equal("C03fxe4vs",        config.CustomerId);
            Assert.Equal("admin@example.com", config.AdminEmail);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_WithMissingFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => GlicConfig.Load("does-not-exist.json"));
    }

    [Fact]
    public void Load_WithMalformedJson_ThrowsInvalidOperationException()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not-json");
            Assert.Throws<InvalidOperationException>(() => GlicConfig.Load(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_WithCredentialPath_PopulatesProperty()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                """{"customer_id":"C03fxe4vs","admin_email":"admin@example.com","credential_path":"keys\\sa.json"}""");

            var config = GlicConfig.Load(path);

            Assert.Equal("keys\\sa.json", config.CredentialPath);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_WithoutCredentialPath_DefaultsToNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"customer_id":"C03fxe4vs","admin_email":"admin@example.com"}""");

            var config = GlicConfig.Load(path);

            Assert.Null(config.CredentialPath);
        }
        finally { File.Delete(path); }
    }
}
