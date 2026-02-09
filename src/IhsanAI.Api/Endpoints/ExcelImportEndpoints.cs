using MediatR;
using IhsanAI.Application.Features.ExcelImport.Commands;
using IhsanAI.Application.Features.ExcelImport.Queries;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Api.Endpoints;

public static class ExcelImportEndpoints
{
    public static IEndpointRouteBuilder MapExcelImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/excel-import")
            .WithTags("Excel Import")
            .RequireAuthorization("CanImportPolicies");

        // POST - Upload and parse Excel file
        group.MapPost("/upload", async (IFormFile file, int? sigortaSirketiId, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new UploadExcelCommand(file, sigortaSirketiId));
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Success = false, Error = ex.Message });
            }
            catch (Exception)
            {
                return Results.BadRequest(new { Success = false, Error = "Dosya işlenirken bir hata oluştu. Lütfen dosya formatını kontrol edip tekrar deneyin." });
            }
        })
        .WithName("UploadExcelForPreview")
        .WithDescription("Excel dosyasını yükler ve önizleme verisi döner")
        .DisableAntiforgery();

        // POST - Confirm and save to database
        group.MapPost("/confirm", async (ConfirmImportRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new ConfirmImportCommand(request.SessionId));
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ConfirmExcelImport")
        .WithDescription("Onaylanan verileri veritabanına kaydeder");

        // POST - Confirm batch (timeout önleme için)
        group.MapPost("/confirm-batch", async (ConfirmBatchRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new ConfirmImportBatchCommand(request.SessionId, request.Skip, request.Take));
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ConfirmExcelImportBatch")
        .WithDescription("Verileri batch halinde kaydeder (timeout önleme)");

        // GET - Supported formats
        group.MapGet("/formats", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetSupportedFormatsQuery());
            return Results.Ok(result);
        })
        .WithName("GetSupportedFormats")
        .WithDescription("Desteklenen Excel formatlarını listeler");

        // GET - Import history
        group.MapGet("/history", async (int? page, int? pageSize, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetImportHistoryQuery(page ?? 1, pageSize ?? 20));
            return Results.Ok(result);
        })
        .WithName("GetImportHistory")
        .WithDescription("Import geçmişini listeler");

        // POST - Detect format from headers (upload oncesi hizli tespit)
        group.MapPost("/detect-format", async (DetectFormatRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new DetectFormatQuery(request.FileName, request.Headers));
            return Results.Ok(result);
        })
        .WithName("DetectFormat")
        .WithDescription("Dosya adi ve header kolonlarindan format tespit eder");

        return app;
    }
}

public record ConfirmImportRequest(string SessionId);
public record ConfirmBatchRequest(string SessionId, int Skip, int Take);
public record DetectFormatRequest(string FileName, List<string> Headers);
