using MediatR;
using IhsanAI.Application.Features.Yetkiler.Queries;

namespace IhsanAI.Api.Endpoints;

public static class YetkilerEndpoints
{
    public static IEndpointRouteBuilder MapYetkilerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/yetkiler")
            .WithTags("Yetkiler")
            .RequireAuthorization();

        group.MapGet("/", async (int? firmaId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYetkilerQuery(firmaId));
            return Results.Ok(result);
        })
        .WithName("GetYetkiler")
        .WithDescription("Yetkileri listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYetkiByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetYetkiById")
        .WithDescription("ID'ye göre yetki getirir");

        group.MapGet("/adlar", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYetkiAdlariQuery());
            return Results.Ok(result);
        })
        .WithName("GetYetkiAdlari")
        .WithDescription("Yetki adlarını listeler");

        return app;
    }
}
