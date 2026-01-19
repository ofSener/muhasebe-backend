using MediatR;
using IhsanAI.Application.Features.SigortaSirketleri.Queries;

namespace IhsanAI.Api.Endpoints;

public static class SigortaSirketleriEndpoints
{
    public static IEndpointRouteBuilder MapSigortaSirketleriEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sigorta-sirketleri")
            .WithTags("Sigorta Sirketleri")
            .RequireAuthorization();

        group.MapGet("/", async (bool? sadeceFaal, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetSigortaSirketleriQuery(sadeceFaal));
            return Results.Ok(result);
        })
        .WithName("GetSigortaSirketleri")
        .WithDescription("Sigorta şirketlerini listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetSigortaSirketiByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetSigortaSirketiById")
        .WithDescription("ID'ye göre sigorta şirketi getirir");

        return app;
    }
}
