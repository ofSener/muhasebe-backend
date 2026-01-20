using System.Security.Claims;
using MediatR;
using IhsanAI.Application.Features.Auth.Commands;
using IhsanAI.Application.Features.Auth.Queries;

namespace IhsanAI.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        // POST /api/auth/login
        group.MapPost("/login", async (LoginRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new LoginCommand(request.Email, request.Password));

            if (!result.Success)
            {
                // 401 ile birlikte hata mesajını da döndür
                return Results.Json(new { success = false, error = result.Message }, statusCode: 401);
            }

            return Results.Ok(result);
        })
        .WithName("Login")
        .WithDescription("Kullanıcı girişi yapar ve JWT token döner")
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
            // JWT token stateless olduğu için server-side logout yok
            // Client token'ı silmeli
            return Results.Ok(new { success = true, message = "Başarıyla çıkış yapıldı" });
        })
        .WithName("Logout")
        .WithDescription("Kullanıcı çıkışı yapar")
        .RequireAuthorization();

        return app;
    }
}
