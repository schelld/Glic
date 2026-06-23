// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Linq;
using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.ChromeManagement.v1;
using Google.Apis.Licensing.v1;
using Google.Apis.Services;
using GLic.Auth;
using GLic.Cmdlets;
using Xunit;

namespace GLic.Tests.Cmdlets;

[Collection("GlicSession")]
public class GlicCmdletBaseTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "GLicCmdletTests-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        GlicSession.Clear();
        Directory.Delete(_root, recursive: true);
    }

    private string MakeDir(string name)
    {
        var d = Path.Combine(_root, name);
        Directory.CreateDirectory(d);
        return d;
    }

    private static ApiClients MakeClients()
    {
        var init = new BaseClientService.Initializer();
        return new ApiClients(
            new ChromeManagementService(init),
            new LicensingService(init),
            new DirectoryService(init));
    }

    // --- Existing tests (unchanged) ---

    [Fact]
    public void StopProcessing_SetsCancellationRequested()
    {
        var cmdlet = new FakeCmdlet();
        cmdlet.InvokeStopProcessing();
        Assert.True(cmdlet.IsCancelled);
    }

    [Fact]
    public async Task EmitRowsAsync_PipelineMode_BuffersRowsForPipelineThread()
    {
        var cmdlet = new FakeCmdlet
        {
            RowsToEmit = new[] { new[] { "A", "1" }, new[] { "B", "2" } }.ToAsyncEnumerable()
        };

        await cmdlet.CallRunAsync();

        Assert.Equal(2, cmdlet.PendingOutput.Count);
        Assert.Equal(new[] { "A", "1" }, cmdlet.PendingOutput[0]);
        Assert.Equal(new[] { "B", "2" }, cmdlet.PendingOutput[1]);
    }

    // --- TryAutoConnectAsync tests ---

    [Fact]
    public async Task TryAutoConnectAsync_WhenAlreadyConnected_ReturnsWithoutProbing()
    {
        // Set session connected; probe only an empty dir — would throw branch 4 if check failed
        GlicSession.Set(MakeClients(), new GlicConfig("C03test", "admin@example.com"));

        var emptyDir = MakeDir("empty");
        var cmdlet = new FakeCmdlet();

        // No exception means the early-return branch fired
        await cmdlet.CallTryAutoConnectAsync(CancellationToken.None, new[] { emptyDir });

        Assert.True(GlicSession.IsConnected);
    }

    [Fact]
    public async Task TryAutoConnectAsync_WhenNothingFound_ThrowsWithConnectGlicMessage()
    {
        GlicSession.Clear();
        var emptyDir = MakeDir("empty");
        var cmdlet = new FakeCmdlet();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cmdlet.CallTryAutoConnectAsync(CancellationToken.None, new[] { emptyDir }));

        Assert.Contains("Connect-Glic", ex.Message);
    }

    [Fact]
    public async Task TryAutoConnectAsync_WhenConfigExistsButNoCredential_ThrowsWithConnectGlicMessage()
    {
        GlicSession.Clear();
        var dir = MakeDir("cfg");
        // glic.json present but no service-account.json or GlicVault secret
        File.WriteAllText(
            Path.Combine(dir, "glic.json"),
            """{"customer_id":"C03test","admin_email":"admin@example.com"}""");

        var cmdlet = new FakeCmdlet();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cmdlet.CallTryAutoConnectAsync(CancellationToken.None, new[] { dir }));

        Assert.Contains("Connect-Glic", ex.Message);
    }

    // --- ConnectGlicCmdlet.ValidateServiceAccountJson ---

    [Fact]
    public void ValidateServiceAccountJson_WithValidJson_DoesNotThrow()
    {
        var json = System.Text.Encoding.UTF8.GetBytes(
            """{"type":"service_account","project_id":"my-project","client_email":"sa@my-project.iam.gserviceaccount.com"}""");

        // No exception expected
        ConnectGlicCmdlet.ValidateServiceAccountJson(json, "test.json");
    }

    [Fact]
    public void ValidateServiceAccountJson_WithWrongType_Throws()
    {
        var json = System.Text.Encoding.UTF8.GetBytes(
            """{"type":"authorized_user","client_id":"12345"}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => ConnectGlicCmdlet.ValidateServiceAccountJson(json, "bad.json"));

        Assert.Contains("service_account", ex.Message);
        Assert.Contains("bad.json", ex.Message);
    }

    [Fact]
    public void ValidateServiceAccountJson_WithMissingTypeField_Throws()
    {
        var json = System.Text.Encoding.UTF8.GetBytes("""{"project_id":"my-project"}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => ConnectGlicCmdlet.ValidateServiceAccountJson(json, "no-type.json"));

        Assert.Contains("service_account", ex.Message);
    }

    [Fact]
    public void WriteGlicJson_WritesCustomerIdAndAdminEmail()
    {
        var dir = MakeDir("cfg-write");
        ConnectGlicCmdlet.WriteGlicJson(dir, "C03testxyz", "admin@example.com");

        var text = File.ReadAllText(Path.Combine(dir, ConfigLocator.ConfigFileName));
        Assert.Contains("C03testxyz", text);
        Assert.Contains("admin@example.com", text);
    }

    // --- FakeCmdlet ---

    private sealed class FakeCmdlet : GlicCmdletBase
    {
        public IAsyncEnumerable<string[]>? RowsToEmit { get; set; }

        protected override async Task RunAsync(CancellationToken ct)
        {
            if (RowsToEmit != null)
                await EmitRowsAsync(RowsToEmit, ct);
        }

        public async Task CallRunAsync() => await RunAsync(Cts.Token);
        public Task CallTryAutoConnectAsync(CancellationToken ct, IReadOnlyList<string>? probeDirs = null)
            => TryAutoConnectAsync(ct, probeDirs);

        public void InvokeStopProcessing() => StopProcessing();
        public bool IsCancelled => Cts.Token.IsCancellationRequested;
    }
}
