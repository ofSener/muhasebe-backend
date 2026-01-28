namespace IhsanAI.Application.Features.PoliceHavuzlari.Dtos;

/// <summary>
/// Havuzdaki tek bir poliçe kaydını temsil eder
/// </summary>
public record PoliceHavuzItemDto
{
    public int Id { get; init; }
    public string PoliceNo { get; init; } = string.Empty;
    public string? SigortaliAdi { get; init; }
    public string? Brans { get; init; }
    public int? BransId { get; init; }
    public decimal BrutPrim { get; init; }
    public DateTime? BaslangicTarihi { get; init; }
    public DateTime? BitisTarihi { get; init; }
    public DateTime EklenmeTarihi { get; init; }
    public string? SigortaSirketi { get; init; }
    public int? SigortaSirketiId { get; init; }
    public string? Plaka { get; init; }
    public sbyte ZeyilNo { get; init; }
    public string? PoliceTipi { get; init; }
    public string? PoliceKesenPersonel { get; init; }

    // Eşleşme bilgileri
    public string EslesmeDurumu { get; init; } = "ESLESMEDI"; // ESLESTI, FARK_VAR, ESLESMEDI, AKTARIMDA
    public decimal? YakalananPrim { get; init; }
    public decimal? PrimFarki { get; init; }
    public int? YakalananPoliceId { get; init; }
}

/// <summary>
/// Havuz listesi ve istatistikleri içeren response DTO
/// </summary>
public record PoliceHavuzListDto
{
    public List<PoliceHavuzItemDto> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int MatchedCount { get; init; }
    public int UnmatchedCount { get; init; }
    public int DifferenceCount { get; init; }
    public int OnlyPoolCount { get; init; }
    public decimal TotalBrutPrim { get; init; }
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
