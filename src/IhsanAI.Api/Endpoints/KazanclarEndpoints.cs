using MediatR;
using IhsanAI.Application.Features.Kazanclar.Queries;

namespace IhsanAI.Api.Endpoints;

public static class KazanclarEndpoints
{
    public static IEndpointRouteBuilder MapKazanclarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/earnings")
            .WithTags("Earnings")
            .RequireAuthorization("CanViewMyEarnings");

        // GET - Çalışanın kendi kazançlarını getir
        group.MapGet("/my", async (
            DateTime? startDate,
            DateTime? endDate,
            int? bransId,
            string? odemeDurumu,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMyEarningsQuery(
                startDate, endDate, bransId, odemeDurumu));
            return Results.Ok(result);
        })
        .WithName("GetMyEarnings")
        .WithDescription("Giriş yapan çalışanın kazanç bilgilerini getirir");

        return app;
    }
}
