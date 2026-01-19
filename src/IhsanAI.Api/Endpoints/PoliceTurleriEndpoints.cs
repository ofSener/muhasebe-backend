using MediatR;
using IhsanAI.Application.Features.PoliceTurleri.Queries;

namespace IhsanAI.Api.Endpoints;

public static class PoliceTurleriEndpoints
{
    public static IEndpointRouteBuilder MapPoliceTurleriEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/police-turleri")
            .WithTags("Police Turleri");

        group.MapGet("/", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceTurleriQuery());
            return Results.Ok(result);
        })
        .WithName("GetPoliceTurleri")
        .WithDescription("Poliçe türlerini listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceTuruByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPoliceTuruById")
        .WithDescription("ID'ye göre poliçe türü getirir");

        return app;
    }
}
