using MediatR;
using IhsanAI.Application.Features.PoliceRizikoAdresleri.Queries;

namespace IhsanAI.Api.Endpoints;

public static class PoliceRizikoAdresleriEndpoints
{
    public static IEndpointRouteBuilder MapPoliceRizikoAdresleriEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/police-riziko-adresleri")
            .WithTags("Police Riziko Adresleri");

        group.MapGet("/", async (int? policeId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceRizikoAdresleriQuery(policeId));
            return Results.Ok(result);
        })
        .WithName("GetPoliceRizikoAdresleri")
        .WithDescription("Poliçe riziko adreslerini listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceRizikoAdresByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPoliceRizikoAdresiById")
        .WithDescription("ID'ye göre poliçe riziko adresi getirir");

        return app;
    }
}
