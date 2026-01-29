using MediatR;
using IhsanAI.Application.Features.PoliceHavuzlari.Queries;
using IhsanAI.Application.Features.PoliceHavuzlari.Commands;

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

        group.MapPost("/{id:int}/approve", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ApprovePoolPolicyCommand(id));
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ApprovePoolPolicy")
        .WithDescription("Havuzdaki poliçeyi onaylayıp ana poliçe tablosuna kaydeder");

        group.MapPost("/batch-approve", async (BatchApprovePoolPoliciesCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .WithName("BatchApprovePoolPolicies")
        .WithDescription("Birden fazla havuz poliçesini toplu onaylama");

        group.MapPut("/batch-update", async (BatchUpdatePoliceHavuzlariCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Ok(new
            {
                success = result.FailedCount == 0,
                updatedCount = result.UpdatedCount,
                failedCount = result.FailedCount,
                failedIds = result.FailedIds
            });
        })
        .WithName("BatchUpdatePoliceHavuzlari")
        .WithDescription("Havuzdaki poliçeleri toplu güncelle");

        return app;
    }
}
