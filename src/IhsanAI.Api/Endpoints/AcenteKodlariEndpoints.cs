using MediatR;
using IhsanAI.Application.Features.AcenteKodlari.Queries;

namespace IhsanAI.Api.Endpoints;

public static class AcenteKodlariEndpoints
{
    public static IEndpointRouteBuilder MapAcenteKodlariEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/acente-kodlari")
            .WithTags("Acente Kodlari")
            .RequireAuthorization();

        group.MapGet("/", async (int? firmaId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAcenteKodlariQuery(firmaId));
            return Results.Ok(result);
        })
        .WithName("GetAcenteKodlari")
        .WithDescription("Acente kodlarını listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAcenteKoduByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetAcenteKoduById")
        .WithDescription("ID'ye göre acente kodu getirir");

        return app;
    }
}
