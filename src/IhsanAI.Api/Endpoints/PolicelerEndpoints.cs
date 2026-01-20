using MediatR;
using IhsanAI.Application.Features.Policeler.Queries;

namespace IhsanAI.Api.Endpoints;

public static class PolicelerEndpoints
{
    public static IEndpointRouteBuilder MapPolicelerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policies")
            .WithTags("Policies")
            .RequireAuthorization();

        group.MapGet("/", async (int? isOrtagiFirmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPolicelerQuery(isOrtagiFirmaId, limit));
            return Results.Ok(result);
        })
        .WithName("GetPoliceler")
        .WithDescription("Poliçeleri listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPoliceById")
        .WithDescription("ID'ye göre poliçe getirir");

        return app;
    }
}
