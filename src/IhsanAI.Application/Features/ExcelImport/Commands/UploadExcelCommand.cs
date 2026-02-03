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

        var allowedExtensions = new[] { ".xlsx", ".xls", ".csv", ".xml" };
        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Desteklenmeyen dosya formatı. Sadece Excel (.xlsx, .xls), CSV ve XML dosyaları yüklenebilir.");
        }

        // Dosya boyutu kontrolü (50MB max)
        if (request.File.Length > 50 * 1024 * 1024)
        {
            throw new ArgumentException("Dosya boyutu 50MB'ı aşamaz.");
        }

        // Stream'i MemoryStream'e kopyala (IFormFile stream'i birden fazla kez okunamaz)
        using var memoryStream = new MemoryStream();
        await request.File.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        return await _excelImportService.ParseExcelAsync(memoryStream, request.File.FileName, request.SigortaSirketiId);
    }
}
