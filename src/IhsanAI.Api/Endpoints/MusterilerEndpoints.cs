using MediatR;
using IhsanAI.Application.Features.Musteriler.Queries;
using IhsanAI.Application.Features.Musteriler.Commands;

namespace IhsanAI.Api.Endpoints;

public static class MusterilerEndpoints
{
    public static IEndpointRouteBuilder MapMusterilerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization();

        group.MapGet("/", async (int? ekleyenFirmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMusterilerQuery(ekleyenFirmaId, limit));
            return Results.Ok(result);
        })
        .WithName("GetMusteriler")
        .WithDescription("Müşterileri listeler");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMusteriByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetMusteriById")
        .WithDescription("ID'ye göre müşteri getirir");

        group.MapGet("/search", async (string name, int? ekleyenFirmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new SearchCustomersQuery(name, ekleyenFirmaId, limit ?? 20));
            return Results.Ok(result);
        })
        .WithName("SearchCustomers")
        .WithDescription("Müşteri arama");

        group.MapPost("/", async (CreateCustomerCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Success ? Results.Created($"/api/customers/{result.CustomerId}", result) : Results.BadRequest(result);
        })
        .WithName("CreateCustomer")
        .WithDescription("Yeni müşteri oluşturur");

        return app;
    }
}
