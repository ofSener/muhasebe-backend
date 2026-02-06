using MediatR;
using IhsanAI.Application.Features.Dashboard;
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
            DashboardMode? mode,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDashboardStatsQuery(firmaId, startDate, endDate, mode ?? DashboardMode.Onayli));
            return Results.Ok(result);
        })
        .WithName("GetDashboardStats")
        .WithDescription("Dashboard ana istatistiklerini getirir. mode=0: Onaylı Poliçeler, mode=1: Yakalanan Poliçeler")
        .RequireAuthorization("CanViewFinanceDashboard");

        // Branş dağılımı
        group.MapGet("/brans-dagilim", async (
            int? firmaId,
            DateTime? startDate,
            DateTime? endDate,
            string? bransIds,
            string? kullaniciIds,
            string? subeIds,
            string? sirketIds,
            DashboardMode? mode,
            IMediator mediator) =>
        {
            var filters = new DashboardFilters(bransIds, kullaniciIds, subeIds, sirketIds);
            var result = await mediator.Send(new GetBransDagilimQuery(firmaId, startDate, endDate, mode ?? DashboardMode.Onayli, filters));
            return Results.Ok(result);
        })
        .WithName("GetBransDagilim")
        .WithDescription("Branşlara göre poliçe dağılımını getirir. mode=0: Onaylı, mode=1: Yakalanan")
        .RequireAuthorization("CanViewFinanceDashboard");

        // Aylık trend
        group.MapGet("/aylik-trend", async (
            int? firmaId,
            int? months,
            DashboardMode? mode,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAylikTrendQuery(firmaId, months ?? 12, mode ?? DashboardMode.Onayli));
            return Results.Ok(result);
        })
        .WithName("GetAylikTrend")
        .WithDescription("Aylık prim ve poliçe trendini getirir. mode=0: Onaylı, mode=1: Yakalanan")
        .RequireAuthorization("CanViewFinanceDashboard");

        // Top performansçılar
        group.MapGet("/top-performers", async (
            int? firmaId,
            DateTime? startDate,
            DateTime? endDate,
            int? limit,
            string? bransIds,
            string? kullaniciIds,
            string? subeIds,
            string? sirketIds,
            DashboardMode? mode,
            IMediator mediator) =>
        {
            var filters = new DashboardFilters(bransIds, kullaniciIds, subeIds, sirketIds);
            var result = await mediator.Send(new GetTopPerformersQuery(firmaId, startDate, endDate, limit ?? 10, mode ?? DashboardMode.Onayli, filters));
            return Results.Ok(result);
        })
        .WithName("GetTopPerformers")
        .WithDescription("En yüksek prim yapan çalışanları getirir. mode=0: Onaylı, mode=1: Yakalanan")
        .RequireAuthorization("CanViewFinanceDashboard");

        // Son aktiviteler
        group.MapGet("/son-aktiviteler", async (
            int? firmaId,
            DateTime? startDate,
            DateTime? endDate,
            int? limit,
            string? bransIds,
            string? kullaniciIds,
            string? subeIds,
            string? sirketIds,
            DashboardMode? mode,
            IMediator mediator) =>
        {
            var filters = new DashboardFilters(bransIds, kullaniciIds, subeIds, sirketIds);
            var result = await mediator.Send(new GetSonAktivitelerQuery(firmaId, startDate, endDate, limit ?? 20, mode ?? DashboardMode.Onayli, filters));
            return Results.Ok(result);
        })
        .WithName("GetSonAktiviteler")
        .WithDescription("Son eklenen poliçeleri getirir. mode=0: Onaylı, mode=1: Yakalanan")
        .RequireAuthorization("CanViewFinanceDashboard");

        // Sigorta şirketi dağılımı
        group.MapGet("/sirket-dagilim", async (
            int? firmaId,
            DateTime? startDate,
            DateTime? endDate,
            int? limit,
            string? bransIds,
            string? kullaniciIds,
            string? subeIds,
            string? sirketIds,
            DashboardMode? mode,
            IMediator mediator) =>
        {
            var filters = new DashboardFilters(bransIds, kullaniciIds, subeIds, sirketIds);
            var result = await mediator.Send(new GetSirketDagilimQuery(firmaId, startDate, endDate, limit ?? 10, mode ?? DashboardMode.Onayli, filters));
            return Results.Ok(result);
        })
        .WithName("GetSirketDagilim")
        .WithDescription("Sigorta şirketlerine göre poliçe dağılımını getirir. mode=0: Onaylı, mode=1: Yakalanan")
        .RequireAuthorization("CanViewFinanceDashboard");

        // Şube dağılımı
        group.MapGet("/sube-dagilim", async (
            int? firmaId,
            DateTime? startDate,
            DateTime? endDate,
            string? bransIds,
            string? kullaniciIds,
            string? subeIds,
            string? sirketIds,
            DashboardMode? mode,
            IMediator mediator) =>
        {
            var filters = new DashboardFilters(bransIds, kullaniciIds, subeIds, sirketIds);
            var result = await mediator.Send(new GetSubeDagilimQuery(firmaId, startDate, endDate, mode ?? DashboardMode.Onayli, filters));
            return Results.Ok(result);
        })
        .WithName("GetSubeDagilim")
        .WithDescription("Şubelere göre poliçe dağılımını getirir. mode=0: Onaylı, mode=1: Yakalanan")
        .RequireAuthorization("CanViewFinanceDashboard");

        return app;
    }
}
