using MediatR;
using IhsanAI.Application.Features.PoliceHavuzlari.Queries;

namespace IhsanAI.Api.Endpoints;

public static class PoliceHavuzlariEndpoints
{
    public static IEndpointRouteBuilder MapPoliceHavuzlariEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policies/pool")
            .WithTags("Policy Pool")
            .RequireAuthorization("CanViewPool");

        group.MapGet("/", async (
            int? isOrtagiFirmaId,
            int? page,
            int? pageSize,
            string? search,
            string? status,
            int? bransId,
            int? sigortaSirketiId,
            IMediator mediator) =>
        {
            var query = new GetPoliceHavuzlariQuery
            {
                IsOrtagiFirmaId = isOrtagiFirmaId,
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search,
                Status = status,
                BransId = bransId,
                SigortaSirketiId = sigortaSirketiId
            };
            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetPoliceHavuzlari")
        .WithDescription("Havuzdaki poliçeleri yakalananlarla karşılaştırarak listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceHavuzByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPoliceHavuzById")
        .WithDescription("ID'ye göre poliçe havuzu getirir");

        return app;
    }
}
