using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Application.Features.ExcelImport.Queries;

public record GetSupportedFormatsQuery : IRequest<SupportedFormatsResponseDto>;

public class GetSupportedFormatsQueryHandler : IRequestHandler<GetSupportedFormatsQuery, SupportedFormatsResponseDto>
{
    private readonly IExcelImportService _excelImportService;

    public GetSupportedFormatsQueryHandler(IExcelImportService excelImportService)
    {
        _excelImportService = excelImportService;
    }

    public async Task<SupportedFormatsResponseDto> Handle(GetSupportedFormatsQuery request, CancellationToken cancellationToken)
    {
        return await _excelImportService.GetSupportedFormatsAsync();
    }
}
