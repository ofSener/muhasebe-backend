using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Application.Features.ExcelImport.Commands;

public record ConfirmImportBatchCommand(string SessionId, int Skip, int Take) : IRequest<ExcelImportResultDto>;

public class ConfirmImportBatchCommandHandler : IRequestHandler<ConfirmImportBatchCommand, ExcelImportResultDto>
{
    private readonly IExcelImportService _excelImportService;

    public ConfirmImportBatchCommandHandler(IExcelImportService excelImportService)
    {
        _excelImportService = excelImportService;
    }

    public async Task<ExcelImportResultDto> Handle(ConfirmImportBatchCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.SessionId))
        {
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = "Oturum ID'si gerekli."
            };
        }

        if (request.Take <= 0)
        {
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = "Take değeri 0'dan büyük olmalı."
            };
        }

        return await _excelImportService.ConfirmImportBatchAsync(request.SessionId, request.Skip, request.Take);
    }
}
