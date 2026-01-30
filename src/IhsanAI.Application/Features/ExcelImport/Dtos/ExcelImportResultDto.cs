namespace IhsanAI.Application.Features.ExcelImport.Dtos;

public record ExcelImportResultDto
{
    public bool Success { get; init; }
    public int TotalProcessed { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public int DuplicateCount { get; init; }
    public int NewCustomersCreated { get; init; }
    public List<ExcelImportErrorDto> Errors { get; init; } = new();
    public string? ErrorMessage { get; init; }

    // Batch progress bilgileri
    public int TotalValidRows { get; init; }
    public int ProcessedSoFar { get; init; }
    public bool IsCompleted { get; init; }
    public bool HasMoreBatches { get; init; }
}

public record ExcelImportErrorDto
{
    public int RowNumber { get; init; }
    public string? PoliceNo { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}
