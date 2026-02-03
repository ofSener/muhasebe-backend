namespace IhsanAI.Application.Features.ExcelImport.Dtos;

/// <summary>
/// Header bazlı format tespit sonucu
/// </summary>
public record DetectFormatResultDto
{
    /// <summary>
    /// Format tespit edildi mi?
    /// </summary>
    public bool Detected { get; init; }

    /// <summary>
    /// Tespit edilen sigorta şirketi ID
    /// </summary>
    public int? SigortaSirketiId { get; init; }

    /// <summary>
    /// Tespit edilen sigorta şirketi adı
    /// </summary>
    public string? SigortaSirketiAdi { get; init; }

    /// <summary>
    /// Tespit yöntemi: "filename" veya "headers"
    /// </summary>
    public string? DetectionMethod { get; init; }

    /// <summary>
    /// Hata mesajı (tespit edilemezse)
    /// </summary>
    public string? Message { get; init; }
}
