using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Application.Features.ExcelImport.Commands;

public record ConfirmImportCommand(string SessionId) : IRequest<ExcelImportResultDto>;

public class ConfirmImportCommandHandler : IRequestHandler<ConfirmImportCommand, ExcelImportResultDto>
{
    private readonly IExcelImportService _excelImportService;

    public ConfirmImportCommandHandler(IExcelImportService excelImportService)
    {
        _excelImportService = excelImportService;
    }

    public async Task<ExcelImportResultDto> Handle(ConfirmImportCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.SessionId))
        {
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = "Oturum ID'si gerekli."
            };
        }

        return await _excelImportService.ConfirmImportAsync(request.SessionId);
    }
}
