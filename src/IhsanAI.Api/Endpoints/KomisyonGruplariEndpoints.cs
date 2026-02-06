using System.Security.Claims;
using MediatR;
using IhsanAI.Application.Features.KomisyonGruplari.Commands;
using IhsanAI.Application.Features.KomisyonGruplari.Dtos;
using IhsanAI.Application.Features.KomisyonGruplari.Queries;

namespace IhsanAI.Api.Endpoints;

public static class KomisyonGruplariEndpoints
{
    public static IEndpointRouteBuilder MapKomisyonGruplariEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/komisyon-gruplari")
            .WithTags("Komisyon Grupları")
            .RequireAuthorization();

        #region Grup Endpoints

        // GET /api/komisyon-gruplari - Tüm grupları listele
        group.MapGet("/", async (ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            if (firmaId == null) return Results.Unauthorized();

            var result = await mediator.Send(new GetKomisyonGruplariQuery(firmaId.Value));
            return Results.Ok(result);
        })
        .WithName("GetKomisyonGruplari")
        .WithDescription("Tüm komisyon gruplarını listeler");

        // GET /api/komisyon-gruplari/{id} - Grup detayı
        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            if (firmaId == null) return Results.Unauthorized();

            var result = await mediator.Send(new GetKomisyonGrubuDetayQuery(id, firmaId.Value));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetKomisyonGrubuDetay")
        .WithDescription("Komisyon grubu detayını getirir (kurallar ve üyelerle birlikte)");

        // POST /api/komisyon-gruplari - Yeni grup oluştur
        group.MapPost("/", async (KomisyonGrubuRequest request, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            var uyeId = GetUyeId(user);
            if (firmaId == null || uyeId == null) return Results.Unauthorized();

            var id = await mediator.Send(new CreateKomisyonGrubuCommand(firmaId.Value, uyeId.Value, request));
            return Results.Created($"/api/komisyon-gruplari/{id}", new { id });
        })
        .WithName("CreateKomisyonGrubu")
        .WithDescription("Yeni komisyon grubu oluşturur");

        // PUT /api/komisyon-gruplari/{id} - Grup güncelle
        group.MapPut("/{id:int}", async (int id, KomisyonGrubuRequest request, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            var uyeId = GetUyeId(user);
            if (firmaId == null || uyeId == null) return Results.Unauthorized();

            var success = await mediator.Send(new UpdateKomisyonGrubuCommand(id, firmaId.Value, uyeId.Value, request));
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UpdateKomisyonGrubu")
        .WithDescription("Komisyon grubunu günceller");

        // DELETE /api/komisyon-gruplari/{id} - Grup sil
        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            if (firmaId == null) return Results.Unauthorized();

            var success = await mediator.Send(new DeleteKomisyonGrubuCommand(id, firmaId.Value));
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteKomisyonGrubu")
        .WithDescription("Komisyon grubunu siler");

        #endregion

        #region Kural Endpoints

        // POST /api/komisyon-gruplari/{grupId}/kurallar - Kural ekle
        group.MapPost("/{grupId:int}/kurallar", async (int grupId, KomisyonKuraliRequest request, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            var uyeId = GetUyeId(user);
            if (firmaId == null || uyeId == null) return Results.Unauthorized();

            var id = await mediator.Send(new CreateKomisyonKuraliCommand(grupId, firmaId.Value, uyeId.Value, request));
            return id.HasValue
                ? Results.Created($"/api/komisyon-gruplari/{grupId}/kurallar/{id}", new { id })
                : Results.NotFound();
        })
        .WithName("CreateKomisyonKurali")
        .WithDescription("Gruba yeni kural ekler");

        // PUT /api/komisyon-gruplari/{grupId}/kurallar/{kuralId} - Kural güncelle
        group.MapPut("/{grupId:int}/kurallar/{kuralId:int}", async (int grupId, int kuralId, KomisyonKuraliRequest request, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            var uyeId = GetUyeId(user);
            if (firmaId == null || uyeId == null) return Results.Unauthorized();

            var success = await mediator.Send(new UpdateKomisyonKuraliCommand(kuralId, grupId, firmaId.Value, uyeId.Value, request));
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UpdateKomisyonKurali")
        .WithDescription("Kuralı günceller");

        // DELETE /api/komisyon-gruplari/{grupId}/kurallar/{kuralId} - Kural sil
        group.MapDelete("/{grupId:int}/kurallar/{kuralId:int}", async (int grupId, int kuralId, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            if (firmaId == null) return Results.Unauthorized();

            var success = await mediator.Send(new DeleteKomisyonKuraliCommand(kuralId, grupId, firmaId.Value));
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteKomisyonKurali")
        .WithDescription("Kuralı siler");

        #endregion

        #region Üye Endpoints

        // POST /api/komisyon-gruplari/{grupId}/uyeler - Üye ekle
        group.MapPost("/{grupId:int}/uyeler", async (int grupId, KomisyonGrubuUyeEkleRequest request, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            var uyeId = GetUyeId(user);
            if (firmaId == null || uyeId == null) return Results.Unauthorized();

            var id = await mediator.Send(new AddUyeToGrupCommand(grupId, firmaId.Value, uyeId.Value, request.UyeId));
            return id.HasValue
                ? Results.Created($"/api/komisyon-gruplari/{grupId}/uyeler/{request.UyeId}", new { id })
                : Results.BadRequest(new { error = "Üye eklenemedi. Grup bulunamadı veya üye zaten ekli." });
        })
        .WithName("AddUyeToGrup")
        .WithDescription("Gruba üye ekler");

        // DELETE /api/komisyon-gruplari/{grupId}/uyeler/{uyeId} - Üye çıkar
        group.MapDelete("/{grupId:int}/uyeler/{uyeId:int}", async (int grupId, int uyeId, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            if (firmaId == null) return Results.Unauthorized();

            var success = await mediator.Send(new RemoveUyeFromGrupCommand(grupId, uyeId, firmaId.Value));
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("RemoveUyeFromGrup")
        .WithDescription("Gruptan üye çıkarır");

        #endregion

        #region Şube Endpoints

        // POST /api/komisyon-gruplari/{grupId}/subeler - Şube ekle
        group.MapPost("/{grupId:int}/subeler", async (int grupId, KomisyonGrubuSubeEkleRequest request, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            var uyeId = GetUyeId(user);
            if (firmaId == null || uyeId == null) return Results.Unauthorized();

            var id = await mediator.Send(new AddSubeToGrupCommand(grupId, firmaId.Value, uyeId.Value, request.SubeId));
            return id.HasValue
                ? Results.Created($"/api/komisyon-gruplari/{grupId}/subeler/{request.SubeId}", new { id })
                : Results.BadRequest(new { error = "Şube eklenemedi. Grup bulunamadı veya şube zaten ekli." });
        })
        .WithName("AddSubeToGrup")
        .WithDescription("Gruba şube ekler");

        // DELETE /api/komisyon-gruplari/{grupId}/subeler/{subeId} - Şube çıkar
        group.MapDelete("/{grupId:int}/subeler/{subeId:int}", async (int grupId, int subeId, ClaimsPrincipal user, IMediator mediator) =>
        {
            var firmaId = GetFirmaId(user);
            if (firmaId == null) return Results.Unauthorized();

            var success = await mediator.Send(new RemoveSubeFromGrupCommand(grupId, subeId, firmaId.Value));
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("RemoveSubeFromGrup")
        .WithDescription("Gruptan şube çıkarır");

        #endregion

        return app;
    }

    private static int? GetFirmaId(ClaimsPrincipal user)
    {
        var firmaIdClaim = user.FindFirst("firmaId")?.Value;
        return int.TryParse(firmaIdClaim, out var firmaId) ? firmaId : null;
    }

    private static int? GetUyeId(ClaimsPrincipal user)
    {
        var uyeIdClaim = user.FindFirst("uyeId")?.Value ??
                         user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(uyeIdClaim, out var uyeId) ? uyeId : null;
    }
}
