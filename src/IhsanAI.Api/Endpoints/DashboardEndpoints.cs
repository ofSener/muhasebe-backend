using MediatR;
using IhsanAI.Application.Features.Dashboard.Queries;

namespace IhsanAI.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        // Ana istatistikler
        group.MapGet("/stats", async (
            int? firmaId,
            DateTime? startDate,
            DateTime? endDate,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDashboardStatsQuery(firmaId, startDate, endDate));
            return Results.Ok(result);
        })
        .WithName("GetDashboardStats")
        .WithDescription("Dashboard ana istatistiklerini getirir");

        // Branş dağılımı
        group.MapGet("/brans-dagilim", async (
            int? firmaId,
            DateTime? startDate,
            DateTime? endDate,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetBransDagilimQuery(firmaId, startDate, endDate));
            return Results.Ok(result);
        })
        .WithName("GetBransDagilim")
        .WithDescription("Branşlara göre poliçe dağılımını getirir");

        // Aylık trend
        group.MapGet("/aylik-trend", async (
            int? firmaId,
            int? months,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAylikTrendQuery(firmaId, months ?? 12));
            return Results.Ok(result);
        })
        .WithName("GetAylikTrend")
        .WithDescription("Aylık prim ve poliçe trendini getirir");

        // Top performansçılar
        group.MapGet("/top-performers", async (
            int? firmaId,
            DateTime? startDate,
            DateTime? endDate,
            int? limit,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTopPerformersQuery(firmaId, startDate, endDate, limit ?? 10));
            return Results.Ok(result);
        })
        .WithName("GetTopPerformers")
        .WithDescription("En yüksek prim yapan çalışanları getirir");

        // Son aktiviteler
        group.MapGet("/son-aktiviteler", async (
            int? firmaId,
            int? limit,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetSonAktivitelerQuery(firmaId, limit ?? 20));
            return Results.Ok(result);
        })
        .WithName("GetSonAktiviteler")
        .WithDescription("Son eklenen poliçeleri getirir");

        // Sigorta şirketi dağılımı
        group.MapGet("/sirket-dagilim", async (
            int? firmaId,
            DateTime? startDate,
            DateTime? endDate,
            int? limit,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetSirketDagilimQuery(firmaId, startDate, endDate, limit ?? 10));
            return Results.Ok(result);
        })
        .WithName("GetSirketDagilim")
        .WithDescription("Sigorta şirketlerine göre poliçe dağılımını getirir");

        return app;
    }
}
