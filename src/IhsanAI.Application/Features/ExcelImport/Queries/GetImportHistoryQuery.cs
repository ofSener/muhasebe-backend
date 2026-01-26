using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Application.Features.ExcelImport.Queries;

public record GetImportHistoryQuery(int Page = 1, int PageSize = 20) : IRequest<ImportHistoryListDto>;

public class GetImportHistoryQueryHandler : IRequestHandler<GetImportHistoryQuery, ImportHistoryListDto>
{
    private readonly IExcelImportService _excelImportService;
    private readonly ICurrentUserService _currentUserService;

    public GetImportHistoryQueryHandler(
        IExcelImportService excelImportService,
        ICurrentUserService currentUserService)
    {
        _excelImportService = excelImportService;
        _currentUserService = currentUserService;
    }

    public async Task<ImportHistoryListDto> Handle(GetImportHistoryQuery request, CancellationToken cancellationToken)
    {
        return await _excelImportService.GetImportHistoryAsync(
            _currentUserService.FirmaId,
            request.Page,
            request.PageSize);
    }
}
