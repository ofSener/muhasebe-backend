using MediatR;
using IhsanAI.Application.Features.PoliceSigortalilari.Queries;

namespace IhsanAI.Api.Endpoints;

public static class PoliceSigortalilariEndpoints
{
    public static IEndpointRouteBuilder MapPoliceSigortalilariEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policy-insureds")
            .WithTags("Policy Insureds")
            .RequireAuthorization();

        group.MapGet("/", async (int? policeId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceSigortalilariQuery(policeId));
            return Results.Ok(result);
        })
        .WithName("GetPoliceSigortalilari")
        .WithDescription("Poliçe sigortalılarını listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceSigortaliByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPoliceSigortaliById")
        .WithDescription("ID'ye göre poliçe sigortalısı getirir");

        return app;
    }
}
