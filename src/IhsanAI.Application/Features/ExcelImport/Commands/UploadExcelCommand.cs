using MediatR;
using Microsoft.AspNetCore.Http;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Application.Features.ExcelImport.Commands;

public record UploadExcelCommand(
    IFormFile File,
    int? SigortaSirketiId
) : IRequest<ExcelImportPreviewDto>;

public class UploadExcelCommandHandler : IRequestHandler<UploadExcelCommand, ExcelImportPreviewDto>
{
    private readonly IExcelImportService _excelImportService;

    public UploadExcelCommandHandler(IExcelImportService excelImportService)
    {
        _excelImportService = excelImportService;
    }

    public async Task<ExcelImportPreviewDto> Handle(UploadExcelCommand request, CancellationToken cancellationToken)
    {
        // Dosya validasyonu
        if (request.File == null || request.File.Length == 0)
        {
            throw new ArgumentException("Dosya seçilmedi.");
        }

        var allowedExtensions = new[] { ".xlsx", ".xls", ".csv" };
        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Desteklenmeyen dosya formatı. Sadece Excel (.xlsx, .xls) ve CSV dosyaları yüklenebilir.");
        }

        // Dosya boyutu kontrolü (10MB max)
        if (request.File.Length > 10 * 1024 * 1024)
        {
            throw new ArgumentException("Dosya boyutu 10MB'ı aşamaz.");
        }

        using var stream = request.File.OpenReadStream();
        return await _excelImportService.ParseExcelAsync(stream, request.File.FileName, request.SigortaSirketiId);
    }
}
