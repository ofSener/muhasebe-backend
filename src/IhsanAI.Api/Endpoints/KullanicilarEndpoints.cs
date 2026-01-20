using MediatR;
using IhsanAI.Application.Features.Kullanicilar.Queries;
using IhsanAI.Application.Features.Kullanicilar.Commands;

namespace IhsanAI.Api.Endpoints;

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

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetKullaniciByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetKullaniciById")
        .WithDescription("ID'ye göre kullanıcı getirir");

        group.MapGet("/search", async (string name, int? firmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new SearchProducersQuery(name, firmaId, limit ?? 20));
            return Results.Ok(result);
        })
        .WithName("SearchProducers")
        .WithDescription("Üretici/çalışan arama");

        group.MapPut("/{id:int}/yetki", async (int id, UpdateYetkiRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateKullaniciYetkiCommand(id, request.MuhasebeYetkiId));
            return result ? Results.Ok(new { success = true }) : Results.NotFound();
        })
        .WithName("UpdateKullaniciYetki")
        .WithDescription("Kullanıcının yetkisini günceller");

        return app;
    }
}

public record UpdateYetkiRequest(int? MuhasebeYetkiId);
