using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Application.Features.ExcelImport.Queries;

/// <summary>
/// Dosya adı ve header kolonlarından format tespit sorgusu
/// </summary>
public record DetectFormatQuery(string FileName, List<string> Headers) : IRequest<DetectFormatResultDto>;

public class DetectFormatQueryHandler : IRequestHandler<DetectFormatQuery, DetectFormatResultDto>
{
    private readonly IExcelImportService _excelImportService;

    public DetectFormatQueryHandler(IExcelImportService excelImportService)
    {
        _excelImportService = excelImportService;
    }

    public async Task<DetectFormatResultDto> Handle(DetectFormatQuery request, CancellationToken cancellationToken)
    {
        return await _excelImportService.DetectFormatFromHeadersAsync(request.FileName, request.Headers);
    }
}
