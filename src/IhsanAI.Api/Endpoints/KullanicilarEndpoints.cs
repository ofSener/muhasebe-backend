using MediatR;
using IhsanAI.Application.Features.Kullanicilar.Queries;

namespace IhsanAI.Api.Endpoints;

public static class KullanicilarEndpoints
{
    public static IEndpointRouteBuilder MapKullanicilarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/producers")
            .WithTags("Producers")
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

        return app;
    }
}
