using MediatR;
using IhsanAI.Application.Features.Yetkiler.Queries;
using IhsanAI.Application.Features.Yetkiler.Commands;

namespace IhsanAI.Api.Endpoints;

public static class YetkilerEndpoints
{
    public static IEndpointRouteBuilder MapYetkilerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/permissions")
            .WithTags("Permissions")
            .RequireAuthorization();

        // GET - Yetkileri listele (herkes görebilir)
        group.MapGet("/", async (int? firmaId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYetkilerQuery(firmaId));
            return Results.Ok(result);
        })
        .WithName("GetYetkiler")
        .WithDescription("Yetkileri listeler");

        // GET - ID'ye göre yetki getir (herkes görebilir)
        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYetkiByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetYetkiById")
        .WithDescription("ID'ye göre yetki getirir");

        // GET - Yetki adlarını listele (herkes görebilir)
        group.MapGet("/adlar", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetYetkiAdlariQuery());
            return Results.Ok(result);
        })
        .WithName("GetYetkiAdlari")
        .WithDescription("Yetki adlarını listeler");

        // POST - Yeni yetki oluştur (sadece yetkili kullanıcılar)
        group.MapPost("/", async (CreateYetkiCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/api/permissions/{result.Id}", result);
        })
        .WithName("CreateYetki")
        .WithDescription("Yeni yetki oluşturur")
        .RequireAuthorization("CanManagePermissions");

        // PUT - Yetkiyi güncelle (sadece yetkili kullanıcılar)
        group.MapPut("/{id:int}", async (int id, UpdateYetkiCommand command, IMediator mediator) =>
        {
            var updatedCommand = command with { Id = id };
            var result = await mediator.Send(updatedCommand);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("UpdateYetki")
        .WithDescription("Yetkiyi günceller")
        .RequireAuthorization("CanManagePermissions");

        // DELETE - Yetkiyi sil (sadece yetkili kullanıcılar)
        group.MapDelete("/{id:int}", async (int id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteYetkiCommand(id));
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteYetki")
        .WithDescription("Yetkiyi siler")
        .RequireAuthorization("CanManagePermissions");

        return app;
    }
}
