using MediatR;
using IhsanAI.Application.Features.YakalananPoliceler.Queries;

namespace IhsanAI.Api.Endpoints;

public static class YakalananPolicelerEndpoints
{
    public static IEndpointRouteBuilder MapYakalananPolicelerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policies/captured")
            .WithTags("Captured Policies")
            .RequireAuthorization();

        group.MapGet("/", async (int? firmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYakalananPolicelerQuery(firmaId, limit));
            return Results.Ok(result);
        })
        .WithName("GetYakalananPoliceler")
        .WithDescription("Yakalanan poliçeleri listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYakalananPoliceByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetYakalananPoliceById")
        .WithDescription("ID'ye göre yakalanan poliçe getirir");

        return app;
    }
}
