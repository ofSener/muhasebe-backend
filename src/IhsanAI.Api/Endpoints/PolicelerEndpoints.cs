using MediatR;
using IhsanAI.Application.Features.Policeler.Queries;
using IhsanAI.Application.Features.Policeler.Commands;

namespace IhsanAI.Api.Endpoints;

public static class PolicelerEndpoints
{
    public static IEndpointRouteBuilder MapPolicelerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policies")
            .WithTags("Policies")
            .RequireAuthorization();

        group.MapGet("/", async (
            int? page,
            int? pageSize,
            DateTime? startDate,
            DateTime? endDate,
            int? policeTuruId,
            int? sigortaSirketiId,
            int? uyeId,
            string? search,
            int? onayDurumu,
            IMediator mediator) =>
        {
            var query = new GetPolicelerQuery
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                StartDate = startDate,
                EndDate = endDate,
                PoliceTuruId = policeTuruId,
                SigortaSirketiId = sigortaSirketiId,
                UyeId = uyeId,
                Search = search,
                OnayDurumu = onayDurumu
            };

            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetPoliceler")
        .WithDescription("Poliçe listesini sayfalama ve filtreleme ile getirir");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPoliceByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPoliceById")
        .WithDescription("ID'ye göre poliçe getirir");

        group.MapPut("/batch-update", async (BatchUpdatePoliciesCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .WithName("BatchUpdatePolicies")
        .WithDescription("Toplu poliçe güncelleme");

        group.MapPost("/{id:int}/send-to-pool", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new SendPolicyToPoolCommand(id));
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("SendPolicyToPool")
        .WithDescription("Tek poliçeyi havuza gönderir");

        group.MapPost("/batch-send-to-pool", async (BatchSendPoliciesToPoolCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .WithName("BatchSendPoliciesToPool")
        .WithDescription("Toplu poliçe havuza gönderme");

        group.MapPost("/{id:int}/approve", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ApprovePolicyCommand(id));
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ApprovePolicy")
        .WithDescription("Yakalanan poliçeyi onaylar");

        group.MapPost("/batch-approve", async (BatchApprovePoliciesCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .WithName("BatchApprovePolicies")
        .WithDescription("Toplu poliçe onaylama");

        return app;
    }
}
