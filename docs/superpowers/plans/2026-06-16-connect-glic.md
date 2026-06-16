# Connect-Glic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Connect-Glic` cmdlet that prompts for admin email and service-account.json, derives the Google customer ID via the Directory API, stores credentials with Windows DPAPI, and gives all existing cmdlets silent auto-connect.

**Architecture:** A static `GlicSession` holds live `ApiClients` + `GlicConfig` for the process lifetime. `GlicCmdletBase.ProcessRecord` calls a new `TryAutoConnectAsync` before `RunAsync` — probing config dirs for a DPAPI blob or legacy `service-account.json` — so existing cmdlets gain auto-connect with no changes to their own files. `Connect-Glic` is a standalone `PSCmdlet` that drives the first-time setup flow.

**Tech Stack:** C# / .NET 4.7.2 + .NET 8 (dual-target), xunit, Google.Apis.Auth (`GoogleCredential.FromStreamAsync`), `System.Security.Cryptography.ProtectedData` (DPAPI), `System.Text.Json`.

---

## File Map

**New files:**
- `Auth/DpapiStore.cs` — `Protect(byte[])` / `Unprotect(byte[])` wrapper around `ProtectedData`
- `Auth/GlicSession.cs` — static session holder: `Clients`, `Config`, `IsConnected`, `Set`, `Clear`
- `Cmdlets/ConnectGlicCmdlet.cs` — `Connect-Glic` cmdlet (interactive + scriptable)
- `GLic.Tests/Auth/DpapiStoreTests.cs`
- `GLic.Tests/Auth/GlicSessionTests.cs`

**Modified files:**
- `GLic.csproj` — add DPAPI NuGet (net8.0) + framework ref (net472); remove `install.ps1`/`uninstall.ps1` from StageModule
- `Auth/ConfigLocator.cs` — add `DpapiCredentialFileName` constant + `ResolveDpapiPath(string dir)`
- `Auth/ChromeServiceFactory.cs` — add `BuildAsync(string adminEmail, byte[] serviceAccountJson)` overload
- `Cmdlets/GlicCmdletBase.cs` — add `TryAutoConnectAsync`; modify `ProcessRecord`, `LoadConfig`, `BuildClientsAsync`
- `GLic.Tests/Cmdlets/GlicCmdletBaseTests.cs` — add `TryAutoConnectAsync` tests + expose via `FakeCmdlet`
- `GLic.Tests/Auth/ConfigLocatorTests.cs` — add `ResolveDpapiPath` test
- `GLic.psd1` — add `'Connect-Glic'` to `CmdletsToExport`

**Deleted files:**
- `install.ps1`
- `uninstall.ps1`
- `module/GLic/install.ps1` (removed by next build after csproj update)
- `module/GLic/uninstall.ps1` (same)

---

## Task 1: Add DPAPI Assembly References to `GLic.csproj`

`ProtectedData` lives in `System.Security.dll` on .NET Framework and in the `System.Security.Cryptography.ProtectedData` NuGet on .NET 8. Both are Windows-only; the module already targets Windows-only APIs so no platform guard needed.

**Files:**
- Modify: `GLic.csproj`

- [ ] **Step 1: Add DPAPI references**

In `GLic.csproj`, add a `<Reference>` for net472 and a `<PackageReference>` for net8.0. Insert after the existing `net8.0`-conditional `ItemGroup`:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net472'">
  <Reference Include="System.Security" />
</ItemGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <PackageReference Include="System.Security.Cryptography.ProtectedData" Version="8.0.0" />
</ItemGroup>
```

- [ ] **Step 2: Verify build still passes**

```
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add GLic.csproj
git commit -m "build: add DPAPI assembly references for net472 and net8.0"
```

---

## Task 2: `DpapiStore` — Protect / Unprotect Wrapper

**Files:**
- Create: `Auth/DpapiStore.cs`
- Create: `GLic.Tests/Auth/DpapiStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `GLic.Tests/Auth/DpapiStoreTests.cs`:

```csharp
// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Text;
using GLic.Auth;

namespace GLic.Tests.Auth;

public class DpapiStoreTests
{
    [Fact]
    public void RoundTrip_ProtectThenUnprotect_ReturnsSameBytes()
    {
        var original = Encoding.UTF8.GetBytes("{ \"type\": \"service_account\", \"project_id\": \"test\" }");

        var encrypted = DpapiStore.Protect(original);
        var decrypted = DpapiStore.Unprotect(encrypted);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Protect_ProducesOutput_DifferentFromInput()
    {
        var original = Encoding.UTF8.GetBytes("test plaintext");
        var encrypted = DpapiStore.Protect(original);
        Assert.NotEqual(original, encrypted);
    }
}
```

- [ ] **Step 2: Run — verify tests fail**

```
dotnet test GLic.Tests --filter "FullyQualifiedName~DpapiStoreTests"
```

Expected: FAIL — `DpapiStore` does not exist yet.

- [ ] **Step 3: Implement `DpapiStore`**

Create `Auth/DpapiStore.cs`:

```csharp
// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Security.Cryptography;

namespace GLic.Auth;

internal static class DpapiStore
{
    internal static byte[] Protect(byte[] data) =>
        ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    internal static byte[] Unprotect(byte[] data) =>
        ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
```

- [ ] **Step 4: Run — verify tests pass**

```
dotnet test GLic.Tests --filter "FullyQualifiedName~DpapiStoreTests"
```

Expected: PASS, 2 tests.

- [ ] **Step 5: Commit**

```
git add Auth/DpapiStore.cs GLic.Tests/Auth/DpapiStoreTests.cs
git commit -m "feat: add DpapiStore for CurrentUser-scoped DPAPI protect/unprotect"
```

---

## Task 3: `GlicSession` — Static Session Holder

**Files:**
- Create: `Auth/GlicSession.cs`
- Create: `GLic.Tests/Auth/GlicSessionTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `GLic.Tests/Auth/GlicSessionTests.cs`:

```csharp
// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.ChromeManagement.v1;
using Google.Apis.Licensing.v1;
using Google.Apis.Services;
using GLic.Auth;

namespace GLic.Tests.Auth;

public class GlicSessionTests : IDisposable
{
    // Clear static state between tests
    public void Dispose() => GlicSession.Clear();

    private static ApiClients MakeClients()
    {
        // Unauthenticated initializer — enough to construct service objects for testing
        var init = new BaseClientService.Initializer();
        return new ApiClients(
            new ChromeManagementService(init),
            new LicensingService(init),
            new DirectoryService(init));
    }

    [Fact]
    public void IsConnected_Initially_IsFalse()
    {
        GlicSession.Clear();
        Assert.False(GlicSession.IsConnected);
        Assert.Null(GlicSession.Clients);
        Assert.Null(GlicSession.Config);
    }

    [Fact]
    public void Set_WithClientsAndConfig_IsConnectedIsTrue()
    {
        var config = new GlicConfig("C03test", "admin@example.com");
        GlicSession.Set(MakeClients(), config);

        Assert.True(GlicSession.IsConnected);
        Assert.NotNull(GlicSession.Clients);
        Assert.Equal("C03test",           GlicSession.Config!.CustomerId);
        Assert.Equal("admin@example.com", GlicSession.Config.AdminEmail);
    }

    [Fact]
    public void Clear_AfterSet_ResetsAllState()
    {
        GlicSession.Set(MakeClients(), new GlicConfig("C03test", "admin@example.com"));
        GlicSession.Clear();

        Assert.False(GlicSession.IsConnected);
        Assert.Null(GlicSession.Clients);
        Assert.Null(GlicSession.Config);
    }
}
```

- [ ] **Step 2: Run — verify tests fail**

```
dotnet test GLic.Tests --filter "FullyQualifiedName~GlicSessionTests"
```

Expected: FAIL — `GlicSession` does not exist yet.

- [ ] **Step 3: Implement `GlicSession`**

Create `Auth/GlicSession.cs`:

```csharp
// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

namespace GLic.Auth;

internal static class GlicSession
{
    private static ApiClients? _clients;
    private static GlicConfig?  _config;

    internal static bool         IsConnected => _clients != null;
    internal static ApiClients?  Clients     => _clients;
    internal static GlicConfig?  Config      => _config;

    internal static void Set(ApiClients clients, GlicConfig config)
    {
        _clients = clients;
        _config  = config;
    }

    internal static void Clear()
    {
        _clients = null;
        _config  = null;
    }
}
```

- [ ] **Step 4: Run — verify tests pass**

```
dotnet test GLic.Tests --filter "FullyQualifiedName~GlicSessionTests"
```

Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```
git add Auth/GlicSession.cs GLic.Tests/Auth/GlicSessionTests.cs
git commit -m "feat: add GlicSession static session holder"
```

---

## Task 4: `ConfigLocator` — Add DPAPI Path Resolution

**Files:**
- Modify: `Auth/ConfigLocator.cs`
- Modify: `GLic.Tests/Auth/ConfigLocatorTests.cs`

- [ ] **Step 1: Write the failing test**

Append to the `// --- SKU paths ---` section (before the closing brace) in `GLic.Tests/Auth/ConfigLocatorTests.cs`:

```csharp
// --- DPAPI path ---

[Fact]
public void ResolveDpapiPath_ReturnsExpectedFilenameInDir()
{
    var dir = MakeDir("cfg");

    var result = ConfigLocator.ResolveDpapiPath(dir);

    Assert.Equal(Path.Combine(dir, "service-account.dpapi"), result);
}
```

- [ ] **Step 2: Run — verify test fails**

```
dotnet test GLic.Tests --filter "FullyQualifiedName~ResolveDpapiPath"
```

Expected: FAIL — method does not exist.

- [ ] **Step 3: Implement in `ConfigLocator`**

In `Auth/ConfigLocator.cs`, add after the `CredentialFileName` constant:

```csharp
public const string DpapiCredentialFileName = "service-account.dpapi";
```

Add after `ResolveSkuReadPath`:

```csharp
/// <summary>Returns the path where Connect-Glic writes the DPAPI-encrypted key blob.</summary>
public static string ResolveDpapiPath(string configDir) =>
    Path.Combine(configDir, DpapiCredentialFileName);
```

- [ ] **Step 4: Run — verify test passes**

```
dotnet test GLic.Tests --filter "FullyQualifiedName~ResolveDpapiPath"
```

Expected: PASS, 1 test.

- [ ] **Step 5: Run full test suite to confirm no regressions**

```
dotnet test GLic.Tests
```

Expected: All existing tests pass.

- [ ] **Step 6: Commit**

```
git add Auth/ConfigLocator.cs GLic.Tests/Auth/ConfigLocatorTests.cs
git commit -m "feat: add DpapiCredentialFileName and ResolveDpapiPath to ConfigLocator"
```

---

## Task 5: `ChromeServiceFactory` — Byte-Array Overload

Adds a `BuildAsync(string adminEmail, byte[] serviceAccountJson)` overload so the auto-load path can build clients from DPAPI-decrypted bytes without touching disk. Uses `GoogleCredential.FromStreamAsync` over a `MemoryStream`.

No unit test — constructing real `ApiClients` from a valid service-account JSON requires live GCP credentials. The overload is exercised by the integration path.

**Files:**
- Modify: `Auth/ChromeServiceFactory.cs`

- [ ] **Step 1: Add `using` for `MemoryStream` (already available via `System.IO` in `GlobalUsings.cs`) and add the overload**

In `Auth/ChromeServiceFactory.cs`, add this method after the existing `BuildAsync(string, string)`:

```csharp
/// <summary>Builds clients from raw service-account JSON bytes (e.g. from a DPAPI blob).
/// No file is written to disk at any point.</summary>
public static async Task<ApiClients> BuildAsync(string adminEmail, byte[] serviceAccountJson)
{
    using var stream = new MemoryStream(serviceAccountJson);
    var credential = (await Google.Apis.Auth.OAuth2.GoogleCredential
            .FromStreamAsync(stream, CancellationToken.None))
        .CreateScoped(Scopes)
        .CreateWithUser(adminEmail);

    var initializer = new BaseClientService.Initializer
    {
        HttpClientInitializer = credential,
        ApplicationName = "GLic"
    };

    return new ApiClients(
        new ChromeManagementService(initializer),
        new LicensingService(initializer),
        new DirectoryService(initializer));
}
```

- [ ] **Step 2: Build to confirm it compiles**

```
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add Auth/ChromeServiceFactory.cs
git commit -m "feat: add ChromeServiceFactory.BuildAsync(byte[]) for DPAPI auto-load path"
```

---

## Task 6: `GlicCmdletBase` — Auto-Connect Decision Tree

Adds `TryAutoConnectAsync` (internal, probes dirs for DPAPI blob → legacy SA.json → throws) and wires it into `ProcessRecord`. Modifies `LoadConfig` and `BuildClientsAsync` to return session values when already connected.

**Files:**
- Modify: `Cmdlets/GlicCmdletBase.cs`
- Modify: `GLic.Tests/Cmdlets/GlicCmdletBaseTests.cs`

- [ ] **Step 1: Write the failing tests**

Replace the contents of `GLic.Tests/Cmdlets/GlicCmdletBaseTests.cs` with:

```csharp
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
        // glic.json present but no service-account.dpapi or service-account.json
        File.WriteAllText(
            Path.Combine(dir, "glic.json"),
            """{"customer_id":"C03test","admin_email":"admin@example.com"}""");

        var cmdlet = new FakeCmdlet();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cmdlet.CallTryAutoConnectAsync(CancellationToken.None, new[] { dir }));

        Assert.Contains("Connect-Glic", ex.Message);
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
```

- [ ] **Step 2: Run — verify new tests fail**

```
dotnet test GLic.Tests --filter "FullyQualifiedName~TryAutoConnectAsync"
```

Expected: FAIL — method does not exist.

- [ ] **Step 3: Implement changes in `GlicCmdletBase`**

Replace the contents of `Cmdlets/GlicCmdletBase.cs` with:

```csharp
// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using GLic.Auth;

namespace GLic.Cmdlets;

public abstract class GlicCmdletBase : PSCmdlet, IDisposable
{
    internal static string ModuleDir =>
        Path.GetDirectoryName(typeof(GlicCmdletBase).Assembly.Location) ?? AppContext.BaseDirectory;

    /// <summary>Explicit config path. When omitted, ConfigLocator probes
    /// GLIC_CONFIG → %ProgramData%\GLic → %APPDATA%\GLic → module dir.</summary>
    [Parameter] public string? Config { get; set; }

    /// <summary>Explicit service-account key path. When omitted, resolved via
    /// credential_path in glic.json, then siblings of glic.json, then module dir.</summary>
    [Parameter] public string? ServiceAccountPath { get; set; }

    private string? _resolvedConfigPath;
    protected string ResolvedConfigPath =>
        _resolvedConfigPath ??= ConfigLocator.ResolveConfigPath(NormalizePath(Config));

    protected GlicConfig LoadConfig()
    {
        if (GlicSession.IsConnected && Config == null)
            return GlicSession.Config!;
        return GlicConfig.Load(ResolvedConfigPath);
    }

    protected Task<ApiClients> BuildClientsAsync(GlicConfig cfg)
    {
        if (GlicSession.IsConnected && Config == null && ServiceAccountPath == null)
            return Task.FromResult(GlicSession.Clients!);
        return ChromeServiceFactory.BuildAsync(
            cfg.AdminEmail,
            ConfigLocator.ResolveCredentialPath(
                NormalizePath(ServiceAccountPath), cfg.CredentialPath, ResolvedConfigPath));
    }

    // Relative user input must resolve against the PowerShell location, not the
    // process CWD — they routinely differ in PS 5.1. Requires the pipeline thread;
    // cmdlets call LoadConfig/BuildClientsAsync at the top of RunAsync, before any await.
    private string? NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : GetUnresolvedProviderPathFromPSPath(path);

    protected CancellationTokenSource Cts { get; } = new CancellationTokenSource();

    private readonly List<object> _outputBuffer = new List<object>();
    private readonly List<(bool IsWarning, string Text)> _messageBuffer = new List<(bool, string)>();

    internal IReadOnlyList<object> PendingOutput => _outputBuffer;
    internal IReadOnlyList<(bool IsWarning, string Text)> PendingMessages => _messageBuffer;

    protected new void WriteVerbose(string text) => _messageBuffer.Add((false, text));
    protected new void WriteWarning(string text) => _messageBuffer.Add((true, text));
    protected void BufferObject(object obj) => _outputBuffer.Add(obj);

    protected override void StopProcessing() => Cts.Cancel();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) Cts.Dispose();
    }

    protected abstract Task RunAsync(CancellationToken ct);

    protected override void ProcessRecord()
    {
        try
        {
            if (Config == null && ServiceAccountPath == null)
                TryAutoConnectAsync(Cts.Token).GetAwaiter().GetResult();
            RunAsync(Cts.Token).GetAwaiter().GetResult();
        }
        catch (FileNotFoundException ex)
        {
            WriteError(new ErrorRecord(ex, "ConfigNotFound", ErrorCategory.ObjectNotFound, this));
            return;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(new ErrorRecord(ex, "ConfigError", ErrorCategory.InvalidData, this));
            return;
        }
        catch (Google.GoogleApiException ex)
        {
            WriteError(new ErrorRecord(ex, "ApiError", ErrorCategory.ConnectionError, this));
            return;
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "UnknownError", ErrorCategory.NotSpecified, this));
            return;
        }

        // Now on the pipeline thread — safe to call Write* methods.
        foreach (var (isWarning, text) in _messageBuffer)
            if (isWarning) base.WriteWarning(text);
            else base.WriteVerbose(text);
        foreach (var obj in _outputBuffer)
            WriteObject(obj);
    }

    protected async Task EmitRowsAsync<T>(IAsyncEnumerable<T> rows, CancellationToken ct)
    {
        await foreach (var row in rows.WithCancellation(ct))
            _outputBuffer.Add(row!);
    }

    // internal for testability (InternalsVisibleTo GLic.Tests)
    internal async Task TryAutoConnectAsync(
        CancellationToken ct, IReadOnlyList<string>? probeDirs = null)
    {
        if (GlicSession.IsConnected) return;

        probeDirs ??= ConfigLocator.DefaultConfigDirs();

        foreach (var dir in probeDirs)
        {
            var configPath = Path.Combine(dir, ConfigLocator.ConfigFileName);
            if (!File.Exists(configPath)) continue;

            var cfg = GlicConfig.Load(configPath);

            // Prefer DPAPI blob (written by Connect-Glic)
            var dpapiPath = ConfigLocator.ResolveDpapiPath(dir);
            if (File.Exists(dpapiPath))
            {
                var json = DpapiStore.Unprotect(File.ReadAllBytes(dpapiPath));
                var clients = await ChromeServiceFactory.BuildAsync(cfg.AdminEmail, json);
                GlicSession.Set(clients, cfg);
                return;
            }

            // Fall back to plain service-account.json (AllUsers / manual install)
            var credPath = Path.Combine(dir, ConfigLocator.CredentialFileName);
            if (File.Exists(credPath))
            {
                var clients = await ChromeServiceFactory.BuildAsync(cfg.AdminEmail, credPath);
                GlicSession.Set(clients, cfg);
                return;
            }
        }

        throw new InvalidOperationException(
            "No GLic session. Run Connect-Glic to authenticate.");
    }
}
```

- [ ] **Step 4: Run all tests**

```
dotnet test GLic.Tests
```

Expected: All tests pass (including the 3 new `TryAutoConnectAsync` tests).

- [ ] **Step 5: Commit**

```
git add Cmdlets/GlicCmdletBase.cs GLic.Tests/Cmdlets/GlicCmdletBaseTests.cs
git commit -m "feat: add TryAutoConnectAsync to GlicCmdletBase; wire auto-connect into ProcessRecord"
```

---

## Task 7: `ConnectGlicCmdlet`

Standalone `PSCmdlet` (not `GlicCmdletBase` — it bootstraps the session, not consumes it). Validates the service-account JSON, derives the customer ID via `customers.get("my_customer")`, writes `glic.json` + DPAPI blob to `%APPDATA%\GLic`, then sets `GlicSession`.

**Files:**
- Create: `Cmdlets/ConnectGlicCmdlet.cs`

- [ ] **Step 1: Write the validation helper test first**

Add to `GLic.Tests/Cmdlets/GlicCmdletBaseTests.cs` (before the `FakeCmdlet` inner class):

```csharp
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
```

- [ ] **Step 2: Run — verify validation tests fail**

```
dotnet test GLic.Tests --filter "FullyQualifiedName~ValidateServiceAccountJson"
```

Expected: FAIL — `ConnectGlicCmdlet` does not exist yet.

- [ ] **Step 3: Implement `ConnectGlicCmdlet`**

Create `Cmdlets/ConnectGlicCmdlet.cs`:

```csharp
// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GLic.Auth;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommunications.Connect, "Glic")]
public sealed class ConnectGlicCmdlet : PSCmdlet
{
    [Parameter] public string? AdminEmail       { get; set; }
    [Parameter] public string? ServiceAccountPath { get; set; }
    [Parameter] public SwitchParameter Force    { get; set; }

    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    protected override void StopProcessing() => _cts.Cancel();

    protected override void ProcessRecord()
    {
        if (GlicSession.IsConnected && !Force.IsPresent) return;

        try
        {
            ConnectAsync(_cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // User pressed Ctrl+C — silent exit
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "ConnectGlicFailed", ErrorCategory.AuthenticationError, this));
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        // --- Collect admin email ---
        var email = AdminEmail?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            Host.UI.Write("Admin email (e.g. admin@domain.com): ");
            email = Host.UI.ReadLine()?.Trim();
        }
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Admin email is required.");

        // --- Collect service-account.json path ---
        var saPath = ServiceAccountPath?.Trim();
        while (string.IsNullOrWhiteSpace(saPath) || !File.Exists(saPath))
        {
            if (!string.IsNullOrWhiteSpace(saPath))
                Host.UI.WriteLine($"File not found: {saPath}");
            Host.UI.Write("Path to service-account.json: ");
            saPath = Host.UI.ReadLine()?.Trim();
        }

        // --- Validate JSON ---
        var saBytes = File.ReadAllBytes(saPath!);
        ValidateServiceAccountJson(saBytes, saPath!);

        // --- Authenticate and derive customer ID ---
        Host.UI.WriteLine("Connecting to Google Workspace...");
        var clients = await ChromeServiceFactory.BuildAsync(email!, saBytes);
        var customer = await clients.Directory.Customers.Get("my_customer").ExecuteAsync(ct);
        var customerId = customer.Id
            ?? throw new InvalidOperationException(
                "customers.get returned no customer ID. Verify admin_email has admin rights.");

        // --- Write glic.json ---
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GLic");
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, ConfigLocator.ConfigFileName);
        var cfg = new GlicConfig(customerId, email!);
        var writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        File.WriteAllText(configPath, JsonSerializer.Serialize(cfg, writeOptions), Encoding.UTF8);

        // --- Write DPAPI blob ---
        var dpapiPath = ConfigLocator.ResolveDpapiPath(configDir);
        File.WriteAllBytes(dpapiPath, DpapiStore.Protect(saBytes));

        // --- Set session ---
        GlicSession.Set(clients, cfg);

        Host.UI.WriteLine($"Connected: {email} ({customerId})");
    }

    /// <summary>Validates that <paramref name="json"/> is a service account key file.
    /// Internal so unit tests can call it directly.</summary>
    internal static void ValidateServiceAccountJson(byte[] json, string path)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("type", out var typeEl)
            || typeEl.GetString() != "service_account")
            throw new InvalidOperationException(
                $"\"{path}\" does not look like a service account key — "
                + "expected \"type\": \"service_account\". "
                + "Download a key from Google Cloud Console → IAM → Service Accounts → Keys.");
    }
}
```

- [ ] **Step 4: Run — verify validation tests pass**

```
dotnet test GLic.Tests --filter "FullyQualifiedName~ValidateServiceAccountJson"
```

Expected: PASS, 3 tests.

- [ ] **Step 5: Run full test suite**

```
dotnet test GLic.Tests
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```
git add Cmdlets/ConnectGlicCmdlet.cs GLic.Tests/Cmdlets/GlicCmdletBaseTests.cs
git commit -m "feat: add Connect-Glic cmdlet with DPAPI storage and customer ID derivation"
```

---

## Task 8: Manifest, Csproj Cleanup, Remove Install Scripts

**Files:**
- Modify: `GLic.psd1`
- Modify: `GLic.csproj`
- Delete: `install.ps1`
- Delete: `uninstall.ps1`

- [ ] **Step 1: Add `Connect-Glic` to `GLic.psd1`**

In `GLic.psd1`, add `'Connect-Glic'` as the first entry in `CmdletsToExport`:

```powershell
CmdletsToExport = @(
    'Connect-Glic',
    'Get-GlicApps',
    'Get-GlicDevices',
    'Get-GlicTelemetry',
    'Get-GlicDeviceApps',
    'Get-GlicBrowserExtensions',
    'Get-GlicManagedBrowsers',
    'Get-GlicHardware',
    'Get-GlicLicenses',
    'Get-GlicUsers',
    'Invoke-GlicDiscover'
)
```

- [ ] **Step 2: Remove `install.ps1` and `uninstall.ps1` from the StageModule target**

In `GLic.csproj`, find the `_SharedRootFiles` line (inside the `StageModule` target) and remove `install.ps1;uninstall.ps1;` from it:

```xml
<_SharedRootFiles Include="GLic.psd1;GLic.format.ps1xml;GLic.psm1;skus.json;glic.sample.json;LICENSE" />
```

- [ ] **Step 3: Delete the installer scripts**

```
git rm install.ps1 uninstall.ps1
```

Also delete the staged copies if they exist (they will be excluded from future builds):

```
git rm --ignore-unmatch "module/GLic/install.ps1" "module/GLic/uninstall.ps1"
```

- [ ] **Step 4: Build to confirm everything compiles and stages correctly**

```
dotnet build
```

Expected: Build succeeded, 0 errors. `module/GLic/` should no longer contain `install.ps1` or `uninstall.ps1` after the build (they won't be copied from root since they're deleted).

- [ ] **Step 5: Run full test suite**

```
dotnet test GLic.Tests
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```
git add GLic.psd1 GLic.csproj
git commit -m "feat: register Connect-Glic in manifest; remove install.ps1 and uninstall.ps1"
```

---

## Task 9: Build and Smoke-Check

- [ ] **Step 1: Full build**

```
dotnet build
```

Expected: Build succeeded, 0 errors, 0 warnings (or only pre-existing warnings).

- [ ] **Step 2: Full test suite**

```
dotnet test GLic.Tests
```

Expected: All tests pass.

- [ ] **Step 3: Verify staged module contains expected files**

```powershell
Get-ChildItem module\GLic -Recurse | Select-Object FullName
```

Expected:
- `module\GLic\GLic.psd1` — includes `Connect-Glic` in `CmdletsToExport`
- `module\GLic\net472\GLic.dll` and `module\GLic\net8.0\GLic.dll`
- No `install.ps1` or `uninstall.ps1` anywhere under `module\GLic\`

- [ ] **Step 4: Final commit tag**

```
git tag v1.1.0-connect-glic
```
