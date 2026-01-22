using MediatR;
using IhsanAI.Application.Features.AcenteKodlari.Queries;
using IhsanAI.Application.Features.AcenteKodlari.Commands;

namespace IhsanAI.Api.Endpoints;

public static class AcenteKodlariEndpoints
{
    public static IEndpointRouteBuilder MapAcenteKodlariEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agency-codes")
            .WithTags("Agency Codes")
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

        group.MapPost("/", async (CreateAcenteKoduCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/api/agency-codes/{result.Id}", result);
        })
        .WithName("CreateAcenteKodu")
        .WithDescription("Yeni acente kodu oluşturur");

        group.MapPut("/{id:int}", async (int id, UpdateAcenteKoduCommand command, IMediator mediator) =>
        {
            var updatedCommand = command with { Id = id };
            var result = await mediator.Send(updatedCommand);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("UpdateAcenteKodu")
        .WithDescription("Acente kodunu günceller");

        group.MapDelete("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteAcenteKoduCommand(id));
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteAcenteKodu")
        .WithDescription("Acente kodunu siler");

        return app;
    }
}
