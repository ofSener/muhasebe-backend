namespace IhsanAI.Application.Common.Interfaces;

public interface IGoogleDriveService
{
    Task<string> GetAuthorizationUrlAsync(int firmaId, string redirectUri);
    Task<GoogleDriveTokenResult> ExchangeCodeForTokensAsync(string code, string redirectUri);
    Task<GoogleDriveTokenResult> RefreshAccessTokenAsync(string refreshToken);
    Task<DriveUploadResult> UploadFileAsync(int firmaId, Stream fileStream, string fileName, string mimeType);
    Task<bool> CreateFolderStructureAsync(int firmaId, string rootFolderName);
    Task<DriveConnectionStatus> GetConnectionStatusAsync(int firmaId);
    Task<bool> DisconnectAsync(int firmaId);
}

public record GoogleDriveTokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string? Email
);

public record DriveUploadResult(
    bool Success,
    string? FileId,
    string? WebViewLink,
    string? ErrorMessage
);

public record DriveConnectionStatus(
    bool IsConnected,
    string? Email,
    DateTime? ConnectedAt,
    int SyncedFolderCount,
    int UploadedFileCount,
    long UsedStorageBytes,
    DateTime? LastSyncAt
);
