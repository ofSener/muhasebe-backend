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
        .WithDescription("Müşterileri listeler")
        .RequireAuthorization("CanViewCustomers");

        group.MapGet("/stats", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCustomerStatsQuery());
            return Results.Ok(result);
        })
        .WithName("GetCustomerStats")
        .WithDescription("Müşteri istatistiklerini getirir")
        .RequireAuthorization("CanViewCustomers");

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMusteriByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetMusteriById")
        .WithDescription("ID'ye göre müşteri getirir")
        .RequireAuthorization("CanViewCustomerDetail");

        group.MapGet("/search", async (string name, int? ekleyenFirmaId, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new SearchCustomersQuery(name, ekleyenFirmaId, limit ?? 20));
            return Results.Ok(result);
        })
        .WithName("SearchCustomers")
        .WithDescription("Müşteri arama")
        .RequireAuthorization("CanViewCustomers");

        group.MapPost("/", async (CreateCustomerCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Success ? Results.Created($"/api/customers/{result.CustomerId}", result) : Results.BadRequest(result);
        })
        .WithName("CreateCustomer")
        .WithDescription("Yeni müşteri oluşturur");

        group.MapPut("/{id:int}", async (int id, UpdateCustomerCommand command, IMediator mediator) =>
        {
            // Ensure the ID from route matches the command
            var commandWithId = command with { Id = id };
            var result = await mediator.Send(commandWithId);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("UpdateCustomer")
        .WithDescription("Müşteri bilgilerini günceller");

        group.MapDelete("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteCustomerCommand(id));
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("DeleteCustomer")
        .WithDescription("Müşteriyi siler");

        // --- Müşteri Eşleştirme & Birleştirme Endpoints ---

        group.MapGet("/candidates", async (
            string? tc,
            string? vkn,
            string? name,
            string? plaka,
            int? limit,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new FindCustomerCandidatesQuery
            {
                TcKimlikNo = tc,
                VergiNo = vkn,
                Name = name,
                Plaka = plaka,
                Limit = limit
            });
            return Results.Ok(result);
        })
        .WithName("FindCustomerCandidates")
        .WithDescription("Müşteri eşleştirme adaylarını arar")
        .RequireAuthorization("CanViewCustomers");

        group.MapPost("/merge", async (MergeCustomersCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("MergeCustomers")
        .WithDescription("İki müşteri kaydını birleştirir");

        // --- Müşteri Detay & Notlar ---

        group.MapGet("/{id:int}/details", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMusteriDetailsQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetMusteriDetails")
        .WithDescription("Müşteri detay bilgilerini getirir")
        .RequireAuthorization("CanViewCustomerDetail");

        group.MapPost("/{id:int}/notes", async (int id, AddMusteriNotCommand command, IMediator mediator) =>
        {
            var commandWithId = command with { MusteriId = id };
            var result = await mediator.Send(commandWithId);
            return result.Success ? Results.Created($"/api/customers/{id}/notes/{result.NotId}", result) : Results.BadRequest(result);
        })
        .WithName("AddMusteriNot")
        .WithDescription("Müşteriye not ekler")
        .RequireAuthorization("CanViewCustomerDetail");

        group.MapDelete("/{id:int}/notes/{noteId:int}", async (int id, int noteId, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteMusteriNotCommand(id, noteId));
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("DeleteMusteriNot")
        .WithDescription("Müşteri notunu siler")
        .RequireAuthorization("CanViewCustomerDetail");

        group.MapGet("/{id:int}/pool-policies", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMusteriHavuzPoliceleriQuery(id));
            return Results.Ok(result);
        })
        .WithName("GetMusteriHavuzPoliceleri")
        .WithDescription("Müşterinin havuz poliçelerini getirir")
        .RequireAuthorization("CanViewCustomerDetail");

        return app;
    }
}
