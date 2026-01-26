namespace IhsanAI.Application.Features.ExcelImport.Dtos;

public record SupportedFormatDto
{
    public int SigortaSirketiId { get; init; }
    public string SigortaSirketiAdi { get; init; } = string.Empty;
    public string FormatDescription { get; init; } = string.Empty;
    public List<string> RequiredColumns { get; init; } = new();
    public string? Notes { get; init; }
}

public record SupportedFormatsResponseDto
{
    public List<SupportedFormatDto> Formats { get; init; } = new();
}
