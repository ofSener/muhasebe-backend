using MediatR;
using IhsanAI.Application.Features.YakalananPoliceler.Queries;
using IhsanAI.Application.Features.YakalananPoliceler.Commands;

namespace IhsanAI.Api.Endpoints;

public static class YakalananPolicelerEndpoints
{
    public static IEndpointRouteBuilder MapYakalananPolicelerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policies/captured")
            .WithTags("Captured Policies")
            .RequireAuthorization();

        group.MapGet("/", async (
            DateTime? startDate,
            DateTime? endDate,
            string? sortBy,
            string? sortDir,
            int? limit,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYakalananPolicelerQuery(startDate, endDate, sortBy, sortDir, limit));
            return Results.Ok(result);
        })
        .WithName("GetYakalananPoliceler")
        .WithDescription("Yakalanan poliçeleri listeler (yetki bazlı filtreleme ile)");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYakalananPoliceByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetYakalananPoliceById")
        .WithDescription("ID'ye göre yakalanan poliçe getirir");

        group.MapGet("/stats", async (DateTime? startDate, DateTime? endDate, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYakalananPoliceStatsQuery(startDate, endDate));
            return Results.Ok(result);
        })
        .WithName("GetYakalananPoliceStats")
        .WithDescription("Yakalanan poliçe istatistiklerini getirir (yetki bazlı filtreleme ile)");

        group.MapGet("/not-in-pool", async (
            int? firmaId,
            int? page,
            int? pageSize,
            string? search,
            int? bransId,
            int? sigortaSirketiId,
            IMediator mediator) =>
        {
            var query = new GetYakalananNotInPoolQuery
            {
                FirmaId = firmaId,
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Search = search,
                BransId = bransId,
                SigortaSirketiId = sigortaSirketiId
            };
            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetYakalananNotInPool")
        .WithDescription("Yakalanan ama havuzda olmayan poliçeleri listeler (Eşleşmeyenler)");

        group.MapPut("/{id:int}", async (int id, UpdateYakalananPoliceCommand command, IMediator mediator) =>
        {
            if (id != command.Id)
            {
                return Results.BadRequest(new { success = false, message = "ID uyuşmazlığı" });
            }

            var result = await mediator.Send(command);
            return Results.Ok(new
            {
                success = result.Success,
                message = result.Message
            });
        })
        .WithName("UpdateYakalananPolice")
        .WithDescription("Yakalanan poliçeyi güncelle (Prodüktör/Şube atama)");

        group.MapPut("/batch-update", async (BatchUpdateYakalananPolicelerCommand command, IMediator mediator) =>
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
        .WithName("BatchUpdateYakalananPoliceler")
        .WithDescription("Yakalanan poliçeleri toplu güncelle");

        group.MapPost("/{id:int}/approve", async (int id, IMediator mediator) =>
        {
            var command = new ApproveCapturedPolicyCommand(id);
            var result = await mediator.Send(command);

            if (result.Success)
            {
                return Results.Ok(new
                {
                    success = true,
                    message = result.Message,
                    policyId = result.PolicyId
                });
            }
            else
            {
                return Results.BadRequest(new
                {
                    success = false,
                    errorMessage = result.Message
                });
            }
        })
        .WithName("ApproveCapturedPolicy")
        .WithDescription("Yakalanan poliçeyi direkt olarak poliçeler tablosuna kaydet (havuzu bypass et)");

        return app;
    }
}
