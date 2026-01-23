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
            var result = await mediator.Send(new LoginCommand(request.Email, request.Password));

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
                    SameSite = SameSiteMode.Strict,  // CSRF koruması
                    Expires = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
                    Path = "/api/auth"    // Sadece auth endpoint'lerinde gönderilir
                });
            }

            // Access token response'da döndürülür (kısa ömürlü, memory'de tutulacak)
            return Results.Ok(new
            {
                result.Success,
                result.Message,
                result.Token,          // Access token - frontend memory'de tutacak
                result.ExpiresIn,
                result.User
                // RefreshToken response'da YOK - cookie'de
            });
        })
        .WithName("Login")
        .WithDescription("Kullanıcı girişi yapar ve JWT token döner")
        .AllowAnonymous();

        // POST /api/auth/refresh - Yeni access token almak için
        group.MapPost("/refresh", async (HttpContext httpContext, IMediator mediator) =>
        {
            // HttpOnly cookie'den refresh token'ı al
            var refreshToken = httpContext.Request.Cookies[RefreshTokenCookieName];

            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.Json(new { success = false, error = "Refresh token bulunamadı" }, statusCode: 401);
            }

            // Refresh token ile yeni access token al
            var result = await mediator.Send(new RefreshTokenCommand(refreshToken));

            if (!result.Success)
            {
                // Geçersiz refresh token - cookie'yi sil
                httpContext.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
                {
                    Path = "/api/auth"
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
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
                    Path = "/api/auth"
                });
            }

            return Results.Ok(new
            {
                result.Success,
                result.Token,
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
        group.MapPost("/logout", (HttpContext httpContext) =>
        {
            // GÜVENLİK: Refresh token cookie'sini sil
            httpContext.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth"
            });

            return Results.Ok(new { success = true, message = "Başarıyla çıkış yapıldı" });
        })
        .WithName("Logout")
        .WithDescription("Kullanıcı çıkışı yapar")
        .RequireAuthorization();

        return app;
    }
}
