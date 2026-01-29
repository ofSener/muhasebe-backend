using System.Security.Claims;
using MediatR;
using IhsanAI.Application.Features.Auth.Commands;
using IhsanAI.Application.Features.Auth.Queries;

namespace IhsanAI.Api.Endpoints;

public static class AuthEndpoints
{
    // Cookie ayarları
    private const string RefreshTokenCookieName = "refresh_token";
    private const int RefreshTokenExpiryDays = 7;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        // POST /api/auth/login
        group.MapPost("/login", async (LoginRequest request, HttpContext httpContext, IMediator mediator) =>
        {
            // HttpContext'ten IP ve Device bilgilerini al
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();

            var result = await mediator.Send(new LoginCommand(
                request.Email,
                request.Password,
                ipAddress,
                deviceInfo
            ));

            if (!result.Success)
            {
                return Results.Json(new { success = false, error = result.Message }, statusCode: 401);
            }

            // GÜVENLİK: Refresh token'ı HttpOnly cookie olarak set et
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                httpContext.Response.Cookies.Append(RefreshTokenCookieName, result.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,      // JavaScript erişemez - XSS koruması
                    Secure = true,        // Sadece HTTPS üzerinden gönderilir
                    SameSite = SameSiteMode.None,  // Cross-origin cookie paylaşımı için gerekli
                    Expires = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
                    Path = "/",           // Tüm path'lerde gönderilir
                    Domain = ".sigorta.teklifi.al"  // Tüm subdomain'lerde geçerli
                });
            }

            // Access token ve refresh token response'da döndürülür
            return Results.Ok(new
            {
                result.Success,
                result.Message,
                result.Token,          // Access token - frontend memory'de tutacak
                result.RefreshToken,   // Refresh token - frontend localStorage'da tutacak
                result.ExpiresIn,
                result.User
            });
        })
        .WithName("Login")
        .WithDescription("Kullanıcı girişi yapar ve JWT token döner")
        .AllowAnonymous();

        // POST /api/auth/refresh - Yeni access token almak için
        group.MapPost("/refresh", async (RefreshRequest? request, HttpContext httpContext, IMediator mediator) =>
        {
            // Önce request body'den, yoksa cookie'den refresh token'ı al
            var refreshToken = request?.RefreshToken;

            if (string.IsNullOrEmpty(refreshToken))
            {
                refreshToken = httpContext.Request.Cookies[RefreshTokenCookieName];
            }

            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.Json(new { success = false, error = "Refresh token bulunamadı" }, statusCode: 401);
            }

            // HttpContext'ten IP ve Device bilgilerini al
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();

            // Refresh token ile yeni access token al
            var result = await mediator.Send(new RefreshTokenCommand(
                refreshToken,
                ipAddress,
                deviceInfo
            ));

            if (!result.Success)
            {
                // Geçersiz refresh token - cookie'yi sil
                httpContext.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
                {
                    Path = "/",
                    Domain = ".sigorta.teklifi.al"
                });
                return Results.Json(new { success = false, error = result.Message }, statusCode: 401);
            }

            // Yeni refresh token varsa cookie'yi güncelle
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                httpContext.Response.Cookies.Append(RefreshTokenCookieName, result.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,  // Cross-origin cookie paylaşımı için gerekli
                    Expires = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
                    Path = "/",
                    Domain = ".sigorta.teklifi.al"  // Tüm subdomain'lerde geçerli
                });
            }

            return Results.Ok(new
            {
                result.Success,
                result.Token,
                result.RefreshToken,  // Yeni refresh token - frontend localStorage'da güncelleyecek
                result.ExpiresIn
            });
        })
        .WithName("RefreshToken")
        .WithDescription("Refresh token ile yeni access token alır")
        .AllowAnonymous();

        // GET /api/auth/me
        group.MapGet("/me", async (HttpContext httpContext, IMediator mediator) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            var user = await mediator.Send(new GetCurrentUserQuery(userId));

            return user != null ? Results.Ok(user) : Results.NotFound();
        })
        .WithName("GetCurrentUser")
        .WithDescription("Mevcut kullanıcı bilgilerini getirir")
        .RequireAuthorization();

        // POST /api/auth/logout
        group.MapPost("/logout", async (HttpContext httpContext, IMediator mediator) =>
        {
            // Kullanıcı ID'sini JWT'den al
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Cookie'den veya body'den refresh token'ı al
            var refreshToken = httpContext.Request.Cookies[RefreshTokenCookieName];

            // Token'ı database'de revoke et
            await mediator.Send(new LogoutCommand(userId, refreshToken));

            // GÜVENLİK: Refresh token cookie'sini sil
            httpContext.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Domain = ".sigorta.teklifi.al"
            });

            return Results.Ok(new { success = true, message = "Başarıyla çıkış yapıldı" });
        })
        .WithName("Logout")
        .WithDescription("Kullanıcı çıkışı yapar ve token'ı revoke eder")
        .RequireAuthorization();

        // GET /api/auth/sessions - Aktif oturumları listele
        group.MapGet("/sessions", async (HttpContext httpContext, IMediator mediator) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            var sessions = await mediator.Send(new GetActiveSessionsQuery(userId));

            return Results.Ok(new { success = true, sessions });
        })
        .WithName("GetActiveSessions")
        .WithDescription("Kullanıcının aktif oturumlarını listeler")
        .RequireAuthorization();

        // DELETE /api/auth/sessions/{sessionId} - Belirli bir oturumu sonlandır
        group.MapDelete("/sessions/{sessionId}", async (int sessionId, HttpContext httpContext, IMediator mediator) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            var result = await mediator.Send(new RevokeSessionCommand(userId, sessionId));

            if (!result.Success)
            {
                return Results.BadRequest(new { result.Success, result.Message });
            }

            return Results.Ok(new { result.Success, result.Message });
        })
        .WithName("RevokeSession")
        .WithDescription("Belirli bir oturumu sonlandırır")
        .RequireAuthorization();

        // POST /api/auth/logout-all - Tüm cihazlardan çıkış yap
        group.MapPost("/logout-all", async (HttpContext httpContext, IMediator mediator) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // Tüm token'ları revoke et (refreshToken parametresi null)
            var result = await mediator.Send(new LogoutCommand(userId, null));

            // Cookie'yi sil
            httpContext.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Domain = ".sigorta.teklifi.al"
            });

            return Results.Ok(new { result.Success, result.Message });
        })
        .WithName("LogoutAll")
        .WithDescription("Tüm cihazlardan çıkış yapar")
        .RequireAuthorization();

        // POST /api/auth/cleanup-tokens - Eski token'ları temizle (Admin only)
        group.MapPost("/cleanup-tokens", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new CleanupExpiredTokensCommand(30));

            return Results.Ok(new
            {
                result.Success,
                result.DeletedCount,
                result.Message
            });
        })
        .WithName("CleanupExpiredTokens")
        .WithDescription("Süresi dolmuş ve iptal edilmiş token'ları temizler")
        .RequireAuthorization(); // TODO: Admin role kontrolü ekle

        return app;
    }
}

// Request DTO for refresh endpoint
public record RefreshRequest(string? RefreshToken);
