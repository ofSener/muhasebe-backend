using MediatR;
using IhsanAI.Application.Features.KomisyonOranlari.Queries;

namespace IhsanAI.Api.Endpoints;

public static class KomisyonOranlariEndpoints
{
    public static IEndpointRouteBuilder MapKomisyonOranlariEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/komisyon-oranlari")
            .WithTags("Komisyon Oranlari")
            .RequireAuthorization();

        group.MapGet("/", async (int? firmaId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetKomisyonOranlariQuery(firmaId));
            return Results.Ok(result);
        })
        .WithName("GetKomisyonOranlari")
        .WithDescription("Komisyon oranlarını listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetKomisyonOraniByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetKomisyonOraniById")
        .WithDescription("ID'ye göre komisyon oranı getirir");

        return app;
    }
}
