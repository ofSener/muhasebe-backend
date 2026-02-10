using MediatR;
using IhsanAI.Application.Features.Kullanicilar.Queries;
using IhsanAI.Application.Features.Kullanicilar.Commands;

namespace IhsanAI.Api.Endpoints;

public record AssignPermissionRequest(int YetkiId);

public static class KullanicilarEndpoints
{
    public static IEndpointRouteBuilder MapKullanicilarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kullanicilar")
            .WithTags("Kullanicilar")
            .RequireAuthorization();

        group.MapGet("/", async (int? firmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetKullanicilarQuery(firmaId, limit));
            return Results.Ok(result);
        })
        .WithName("GetKullanicilar")
        .WithDescription("Kullanıcıları (çalışanları) listeler");

        group.MapGet("/aktif", async (int? firmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAktifKullanicilarQuery(firmaId, limit));
            return Results.Ok(result);
        })
        .WithName("GetAktifKullanicilar")
        .WithDescription("Sadece aktif kullanıcıları (Onay=1) listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetKullaniciByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetKullaniciById")
        .WithDescription("ID'ye göre kullanıcı getirir");

        group.MapGet("/{id:int}/details", async (int id, DateTime? startDate, DateTime? endDate, int? bransId, int? sirketId, string? search, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetKullaniciDetailsQuery(id, startDate, endDate, bransId, sirketId, search));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetKullaniciDetails")
        .WithDescription("Kullanıcının detay bilgilerini, istatistiklerini ve poliçelerini getirir");

        group.MapGet("/search", async (string name, int? firmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new SearchProducersQuery(name, firmaId, limit ?? 20));
            return Results.Ok(result);
        })
        .WithName("SearchProducers")
        .WithDescription("Üretici/çalışan arama");

        group.MapPut("/{id:int}/permission", async (int id, AssignPermissionRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new AssignPermissionCommand(id, request.YetkiId));
            return result ? Results.Ok(new { success = true, message = "Yetki başarıyla atandı" }) : Results.NotFound();
        })
        .WithName("AssignPermission")
        .WithDescription("Kullanıcıya yetki atar");

        group.MapDelete("/{id:int}/permission", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new RemovePermissionCommand(id));
            return result ? Results.Ok(new { success = true, message = "Yetki başarıyla kaldırıldı" }) : Results.NotFound();
        })
        .WithName("RemovePermission")
        .WithDescription("Kullanıcının yetkisini kaldırır");

        return app;
    }
}

public record UpdateYetkiRequest(int? MuhasebeYetkiId);
