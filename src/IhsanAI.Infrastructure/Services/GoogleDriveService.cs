using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace IhsanAI.Infrastructure.Services;

public class GoogleDriveService : IGoogleDriveService
{
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public GoogleDriveService(
        IConfiguration configuration,
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _configuration = configuration;
        _context = context;
        _dateTimeService = dateTimeService;
        _clientId = configuration["GoogleDrive:ClientId"] ?? throw new InvalidOperationException("GoogleDrive:ClientId yapilandirmasi eksik.");
        _clientSecret = configuration["GoogleDrive:ClientSecret"] ?? throw new InvalidOperationException("GoogleDrive:ClientSecret yapilandirmasi eksik.");
    }

    public Task<string> GetAuthorizationUrlAsync(int firmaId, string redirectUri)
    {
        var scopes = string.Join(" ", new[]
        {
            DriveService.Scope.DriveFile,
            "email",
            "profile"
        });

        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
            $"client_id={Uri.EscapeDataString(_clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(scopes)}" +
            $"&access_type=offline" +
            $"&prompt=consent" +
            $"&state={firmaId}";

        return Task.FromResult(authUrl);
    }

    public async Task<GoogleDriveTokenResult> ExchangeCodeForTokensAsync(string code, string redirectUri)
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret
            },
            Scopes = new[] { DriveService.Scope.DriveFile }
        });

        var tokenResponse = await flow.ExchangeCodeForTokenAsync("", code, redirectUri, CancellationToken.None);

        // Get user email
        string? email = null;
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
            var userInfoJson = await httpClient.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            var userInfo = JsonDocument.Parse(userInfoJson);
            if (userInfo.RootElement.TryGetProperty("email", out var emailElement))
            {
                email = emailElement.GetString();
            }
        }
        catch
        {
            // Ignore email fetch errors
        }

        return new GoogleDriveTokenResult(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken ?? string.Empty,
            DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600),
            email
        );
    }

    public async Task<GoogleDriveTokenResult> RefreshAccessTokenAsync(string refreshToken)
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret
            }
        });

        var tokenResponse = await flow.RefreshTokenAsync("", refreshToken, CancellationToken.None);

        return new GoogleDriveTokenResult(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken ?? refreshToken,
            DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600),
            null
        );
    }

    public async Task<DriveUploadResult> UploadFileAsync(int firmaId, Stream fileStream, string fileName, string mimeType)
    {
        var driveService = await GetDriveServiceAsync(firmaId);
        if (driveService == null)
            return new DriveUploadResult(false, null, null, "Drive servisi baglanilamadi.");

        try
        {
            // Get or create folder structure: /IHSAN AI/YYYY/MM/
            var now = _dateTimeService.Now;
            var yearFolder = now.Year.ToString();
            var monthFolder = now.Month.ToString("D2");

            var rootFolderId = await GetOrCreateFolderAsync(driveService, "IHSAN AI", null);
            var yearFolderId = await GetOrCreateFolderAsync(driveService, yearFolder, rootFolderId);
            var monthFolderId = await GetOrCreateFolderAsync(driveService, monthFolder, yearFolderId);

            // Upload file
            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                Parents = new List<string> { monthFolderId }
            };

            var request = driveService.Files.Create(fileMetadata, fileStream, mimeType);
            request.Fields = "id, webViewLink";
            var result = await request.UploadAsync();

            if (result.Status == Google.Apis.Upload.UploadStatus.Completed)
            {
                var file = request.ResponseBody;
                return new DriveUploadResult(true, file.Id, file.WebViewLink, null);
            }

            return new DriveUploadResult(false, null, null, result.Exception?.Message ?? "Yukleme basarisiz.");
        }
        catch (Exception ex)
        {
            return new DriveUploadResult(false, null, null, ex.Message);
        }
    }

    public async Task<bool> CreateFolderStructureAsync(int firmaId, string rootFolderName)
    {
        var driveService = await GetDriveServiceAsync(firmaId);
        if (driveService == null) return false;

        try
        {
            var rootFolderId = await GetOrCreateFolderAsync(driveService, rootFolderName, null);

            // Update token record with root folder ID
            var token = await _context.FirmaDriveTokens
                .FirstOrDefaultAsync(t => t.FirmaId == firmaId);
            if (token != null)
            {
                token.RootFolderId = rootFolderId;
                await _context.SaveChangesAsync(CancellationToken.None);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DriveConnectionStatus> GetConnectionStatusAsync(int firmaId)
    {
        var token = await _context.FirmaDriveTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.FirmaId == firmaId && t.IsActive);

        if (token == null)
            return new DriveConnectionStatus(false, null, null, 0, 0, 0, null);

        var uploadStats = await _context.DriveUploadLogs
            .AsNoTracking()
            .Where(l => l.FirmaId == firmaId && l.UploadStatus == UploadStatus.Success)
            .GroupBy(l => 1)
            .Select(g => new { Count = g.Count(), Size = g.Sum(l => l.FileSizeBytes), Last = g.Max(l => l.UploadedAt) })
            .FirstOrDefaultAsync();

        return new DriveConnectionStatus(
            true,
            token.GoogleEmail,
            token.CreatedAt,
            0,
            uploadStats?.Count ?? 0,
            uploadStats?.Size ?? 0,
            uploadStats?.Last
        );
    }

    public async Task<bool> DisconnectAsync(int firmaId)
    {
        var token = await _context.FirmaDriveTokens
            .FirstOrDefaultAsync(t => t.FirmaId == firmaId);

        if (token != null)
        {
            token.IsActive = false;
            token.UpdatedAt = _dateTimeService.Now;
            await _context.SaveChangesAsync(CancellationToken.None);
        }

        return true;
    }

    private async Task<DriveService?> GetDriveServiceAsync(int firmaId)
    {
        var token = await _context.FirmaDriveTokens
            .FirstOrDefaultAsync(t => t.FirmaId == firmaId && t.IsActive);

        if (token == null) return null;

        // Refresh token if expired or about to expire (within 5 minutes)
        if (token.TokenExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            try
            {
                var newTokens = await RefreshAccessTokenAsync(token.RefreshToken);
                token.AccessToken = newTokens.AccessToken;
                if (!string.IsNullOrEmpty(newTokens.RefreshToken))
                {
                    token.RefreshToken = newTokens.RefreshToken;
                }
                token.TokenExpiresAt = newTokens.ExpiresAt;
                token.UpdatedAt = _dateTimeService.Now;
                await _context.SaveChangesAsync(CancellationToken.None);
            }
            catch
            {
                return null;
            }
        }

        var credential = GoogleCredential.FromAccessToken(token.AccessToken);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "IhsanAI Muhasebe"
        });
    }

    private static async Task<string> GetOrCreateFolderAsync(DriveService service, string folderName, string? parentId)
    {
        // Search for existing folder
        var query = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        if (parentId != null)
            query += $" and '{parentId}' in parents";

        var listRequest = service.Files.List();
        listRequest.Q = query;
        listRequest.Fields = "files(id, name)";

        var result = await listRequest.ExecuteAsync();
        if (result.Files.Any())
            return result.Files[0].Id;

        // Create new folder
        var folderMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = parentId != null ? new List<string> { parentId } : null
        };

        var createRequest = service.Files.Create(folderMetadata);
        createRequest.Fields = "id";
        var folder = await createRequest.ExecuteAsync();

        return folder.Id;
    }
}
