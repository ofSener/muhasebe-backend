using MediatR;
using IhsanAI.Application.Features.Musteriler.Queries;

namespace IhsanAI.Api.Endpoints;

public static class MusterilerEndpoints
{
    public static IEndpointRouteBuilder MapMusterilerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization();

        group.MapGet("/", async (int? ekleyenFirmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMusterilerQuery(ekleyenFirmaId, limit));
            return Results.Ok(result);
        })
        .WithName("GetMusteriler")
        .WithDescription("Müşterileri listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMusteriByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetMusteriById")
        .WithDescription("ID'ye göre müşteri getirir");

        return app;
    }
}
