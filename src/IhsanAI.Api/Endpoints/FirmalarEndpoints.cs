using MediatR;
using IhsanAI.Application.Features.Firmalar.Queries;

namespace IhsanAI.Api.Endpoints;

public static class FirmalarEndpoints
{
    public static IEndpointRouteBuilder MapFirmalarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/firmalar")
            .WithTags("Firmalar");

        group.MapGet("/", async (bool? sadeceOnaylananlar, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFirmalarQuery(sadeceOnaylananlar));
            return Results.Ok(result);
        })
        .WithName("GetFirmalar")
        .WithDescription("Firmaları listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFirmaByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetFirmaById")
        .WithDescription("ID'ye göre firma getirir");

        return app;
    }
}
