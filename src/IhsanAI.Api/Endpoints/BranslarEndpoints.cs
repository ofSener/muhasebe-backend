using MediatR;
using IhsanAI.Application.Features.Branslar.Queries;

namespace IhsanAI.Api.Endpoints;

public static class BranslarEndpoints
{
    public static IEndpointRouteBuilder MapBranslarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/insurance-types")
            .WithTags("Insurance Types")
            .RequireAuthorization();

        group.MapGet("/", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetBranslarQuery());
            return Results.Ok(result);
        })
        .WithName("GetBranslar")
        .WithDescription("Branşları listeler");

        return app;
    }
}
