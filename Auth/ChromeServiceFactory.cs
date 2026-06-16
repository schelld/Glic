// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using Google.Apis.Auth.OAuth2;
using Google.Apis.ChromeManagement.v1;
using Google.Apis.Licensing.v1;
using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Services;

namespace GLic.Auth;

public record ApiClients(
    ChromeManagementService ChromeManagement,
    LicensingService Licensing,
    DirectoryService Directory);

public static class ChromeServiceFactory
{
    private static readonly string[] Scopes =
    [
        ChromeManagementService.Scope.ChromeManagementReportsReadonly,
        ChromeManagementService.Scope.ChromeManagementTelemetryReadonly,
        ChromeManagementService.Scope.ChromeManagementProfilesReadonly,
        DirectoryService.Scope.AdminDirectoryDeviceChromeosReadonly,
        DirectoryService.Scope.AdminDirectoryUserReadonly,
        DirectoryService.Scope.AdminDirectoryOrgunitReadonly,
        LicensingService.Scope.AppsLicensing,
    ];

    public static async Task<ApiClients> BuildAsync(
        string adminEmail,
        string credentialPath)
    {
        if (!File.Exists(credentialPath))
            throw new FileNotFoundException(
                $"Service account file not found: {credentialPath}", credentialPath);

        var serviceAccount = await CredentialFactory.FromFileAsync<ServiceAccountCredential>(
            credentialPath, CancellationToken.None);
        var credential = serviceAccount.ToGoogleCredential()
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

    /// <summary>Builds clients from raw service-account JSON bytes (e.g. from a DPAPI blob).
    /// No file is written to disk at any point.</summary>
    public static async Task<ApiClients> BuildAsync(
        string adminEmail,
        byte[] serviceAccountJson)
    {
        using var stream = new MemoryStream(serviceAccountJson);
        var credential = (await GoogleCredential
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
}
