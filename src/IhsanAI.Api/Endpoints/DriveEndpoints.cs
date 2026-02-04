using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.Drive.Commands;
using IhsanAI.Application.Features.Drive.Queries;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Api.Endpoints;

public static class DriveEndpoints
{
    public static IEndpointRouteBuilder MapDriveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/drive")
            .WithTags("Drive")
            .RequireAuthorization();

        // GET - Connection status
        group.MapGet("/status", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDriveConnectionStatusQuery());
            return Results.Ok(result);
        })
        .WithName("GetDriveStatus")
        .WithDescription("Drive baglanti durumunu getirir");

        // GET - Upload history
        group.MapGet("/history", async (int? page, int? pageSize, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDriveUploadHistoryQuery(page ?? 1, pageSize ?? 20));
            return Results.Ok(result);
        })
        .WithName("GetDriveHistory")
        .WithDescription("Yukleme gecmisini getirir");

        // GET - Get drive link by policeId (CanViewCapturedPolicies yetkisi olan herkes erişebilir)
        app.MapGet("/api/drive/by-police/{policeId}", async (string policeId, IApplicationDbContext context, ICurrentUserService currentUserService) =>
        {
            var firmaId = currentUserService.FirmaId;
            if (firmaId == null)
            {
                return Results.BadRequest(new { Success = false, Error = "Firma bilgisi bulunamadi." });
            }

            var uploadLog = await context.DriveUploadLogs
                .Where(l => l.PoliceId == policeId
                         && l.FirmaId == firmaId
                         && l.UploadStatus == UploadStatus.Success)
                .OrderByDescending(l => l.UploadedAt)
                .FirstOrDefaultAsync();

            if (uploadLog == null)
            {
                return Results.Ok(new { Success = false, Found = false });
            }

            return Results.Ok(new {
                Success = true,
                Found = true,
                FileId = uploadLog.DriveFileId,
                WebViewLink = uploadLog.DriveWebViewLink,
                FileName = uploadLog.FileName,
                UploadedAt = uploadLog.UploadedAt
            });
        })
        .WithName("GetDriveLinkByPoliceId")
        .WithDescription("PoliceId ile Drive linkini getirir")
        .WithTags("Drive")
        .RequireAuthorization();

        // POST - Initiate OAuth connection
        group.MapPost("/connect", async (HttpContext httpContext, IMediator mediator) =>
        {
            // Dinamik RedirectUri olustur (Omer: 36100, Musti: 36200, Live: production URL)
            var request = httpContext.Request;
            var redirectUri = $"{request.Scheme}://{request.Host}/api/drive/oauth/callback";

            var result = await mediator.Send(new InitiateDriveConnectionCommand(redirectUri));
            return Results.Ok(result);
        })
        .WithName("InitiateDriveConnection")
        .WithDescription("Google Drive baglantisi baslatir");

        // POST - Upload file
        group.MapPost("/upload", async (IFormFile file, IMediator mediator) =>
        {
            var result = await mediator.Send(new UploadFileToDriveCommand(file));
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("UploadToDrive")
        .WithDescription("PDF dosyasini Drive'a yukler")
        .DisableAntiforgery();

        // DELETE - Disconnect
        group.MapDelete("/disconnect", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new DisconnectDriveCommand());
            return Results.Ok(new { Success = result });
        })
        .WithName("DisconnectDrive")
        .WithDescription("Drive baglantisini keser");

        // Simple upload endpoint for external systems (uses firmaId from query)
        app.MapPost("/api/drive/upload-external", async (int firmaId, string? policeId, IFormFile file, IMediator mediator, IApplicationDbContext context, IGoogleDriveService driveService, IDateTimeService dateTimeService) =>
        {
            // Validate PDF only
            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { Success = false, Error = "Sadece PDF dosyalari yuklenebilir." });
            }

            // Validate file size (10MB max)
            if (file.Length > 10 * 1024 * 1024)
            {
                return Results.BadRequest(new { Success = false, Error = "Dosya boyutu 10MB'i asamaz." });
            }

            // Aynı policeId + firmaId ile daha önce başarılı yükleme yapılmış mı kontrol et
            if (!string.IsNullOrEmpty(policeId))
            {
                var existingUpload = await context.DriveUploadLogs
                    .FirstOrDefaultAsync(l => l.PoliceId == policeId
                                           && l.FirmaId == firmaId
                                           && l.UploadStatus == UploadStatus.Success);

                if (existingUpload != null)
                {
                    return Results.Ok(new {
                        Success = true,
                        FileId = existingUpload.DriveFileId,
                        WebViewLink = existingUpload.DriveWebViewLink,
                        DrivePath = existingUpload.DriveFolderPath,
                        PoliceId = policeId,
                        AlreadyExists = true
                    });
                }
            }

            // Check if firm has active Drive connection
            var driveToken = await context.FirmaDriveTokens
                .FirstOrDefaultAsync(t => t.FirmaId == firmaId && t.IsActive);

            if (driveToken == null)
            {
                return Results.BadRequest(new { Success = false, Error = "Bu firma icin Google Drive baglantisi bulunamadi." });
            }

            var now = dateTimeService.Now;
            var drivePath = $"/IHSAN AI/{now.Year}/{now.Month:D2}/{file.FileName}";

            // Create log entry
            var uploadLog = new DriveUploadLog
            {
                FirmaId = firmaId,
                FileName = file.FileName,
                OriginalFileName = file.FileName,
                FileSizeBytes = file.Length,
                DriveFolderPath = drivePath,
                UploadStatus = UploadStatus.Pending,
                UploadedAt = now,
                PoliceId = policeId
            };

            context.DriveUploadLogs.Add(uploadLog);
            await context.SaveChangesAsync(CancellationToken.None);

            try
            {
                using var stream = file.OpenReadStream();
                var result = await driveService.UploadFileAsync(firmaId, stream, file.FileName, "application/pdf");

                uploadLog.UploadStatus = result.Success ? UploadStatus.Success : UploadStatus.Failed;
                uploadLog.DriveFileId = result.FileId;
                uploadLog.DriveWebViewLink = result.WebViewLink;
                uploadLog.ErrorMessage = result.ErrorMessage;
                await context.SaveChangesAsync(CancellationToken.None);

                if (result.Success)
                {
                    return Results.Ok(new {
                        Success = true,
                        FileId = result.FileId,
                        WebViewLink = result.WebViewLink,
                        DrivePath = drivePath,
                        PoliceId = policeId,
                        AlreadyExists = false
                    });
                }
                return Results.BadRequest(new { Success = false, Error = result.ErrorMessage });
            }
            catch (Exception ex)
            {
                uploadLog.UploadStatus = UploadStatus.Failed;
                uploadLog.ErrorMessage = ex.Message;
                await context.SaveChangesAsync(CancellationToken.None);
                return Results.BadRequest(new { Success = false, Error = ex.Message });
            }
        })
        .WithName("UploadToDriveExternal")
        .WithDescription("Harici sistemlerden PDF yuklemek icin (firmaId query parametresi ile)")
        .DisableAntiforgery()
        .RequireAuthorization();

        // OAuth callback - Anonymous (no auth required for Google redirect)
        app.MapGet("/api/drive/oauth/callback", async (HttpContext httpContext, string code, string state, IMediator mediator, IConfiguration configuration) =>
        {
            try
            {
                // Dinamik RedirectUri olustur (token exchange icin ayni URI kullanilmali)
                var request = httpContext.Request;
                var redirectUri = $"{request.Scheme}://{request.Host}/api/drive/oauth/callback";

                await mediator.Send(new CompleteDriveConnectionCommand(code, state, redirectUri));

                // Frontend URL'ini de dinamik belirle
                var frontendUrl = configuration["Frontend:BaseUrl"];
                if (string.IsNullOrEmpty(frontendUrl))
                {
                    // Eger yapilandirma yoksa, ayni host'u kullan (port farki olabilir)
                    frontendUrl = $"{request.Scheme}://{request.Host.Host}:3000";
                }
                return Results.Redirect($"{frontendUrl}/pages/settings/drive-integration.html?connected=true");
            }
            catch (Exception ex)
            {
                var frontendUrl = configuration["Frontend:BaseUrl"] ?? "";
                return Results.Redirect($"{frontendUrl}/pages/settings/drive-integration.html?error={Uri.EscapeDataString(ex.Message)}");
            }
        })
        .WithName("DriveOAuthCallback")
        .WithDescription("Google OAuth callback")
        .AllowAnonymous();

        return app;
    }
}
