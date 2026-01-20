using MediatR;
using IhsanAI.Application.Features.PoliceHavuzlari.Queries;

namespace IhsanAI.Api.Endpoints;

public static class PoliceHavuzlariEndpoints
{
    public static IEndpointRouteBuilder MapPoliceHavuzlariEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policies/pool")
            .WithTags("Policy Pool")
            .RequireAuthorization();

        group.MapGet("/", async (int? isOrtagiFirmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceHavuzlariQuery(isOrtagiFirmaId, limit));
            return Results.Ok(result);
        })
        .WithName("GetPoliceHavuzlari")
        .WithDescription("Poliçe havuzlarını listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceHavuzByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPoliceHavuzById")
        .WithDescription("ID'ye göre poliçe havuzu getirir");

        return app;
    }
}
