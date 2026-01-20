using MediatR;
using IhsanAI.Application.Features.Subeler.Queries;

namespace IhsanAI.Api.Endpoints;

public static class SubelerEndpoints
{
    public static IEndpointRouteBuilder MapSubelerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/branches")
            .WithTags("Branches")
            .RequireAuthorization();

        group.MapGet("/", async (int? firmaId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetSubelerQuery(firmaId));
            return Results.Ok(result);
        })
        .WithName("GetSubeler")
        .WithDescription("Şubeleri listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetSubeByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetSubeById")
        .WithDescription("ID'ye göre şube getirir");

        return app;
    }
}
