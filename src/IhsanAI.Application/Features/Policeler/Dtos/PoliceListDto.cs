namespace IhsanAI.Application.Features.Policeler.Dtos;

/// <summary>
/// Poliçe listesi için tek bir poliçe bilgisi
/// </summary>
public record PoliceListItemDto
{
    public int Id { get; init; }
    public int SigortaSirketiId { get; init; }
    public string? SigortaSirketiAdi { get; init; }
    public int PoliceTuruId { get; init; }
    public string? PoliceTuruAdi { get; init; }
    public string PoliceNumarasi { get; init; } = string.Empty;
    public string? Plaka { get; init; }
    public DateTime TanzimTarihi { get; init; }
    public DateTime BaslangicTarihi { get; init; }
    public DateTime BitisTarihi { get; init; }
    public decimal BrutPrim { get; init; }
    public decimal NetPrim { get; init; }
    public string? SigortaliAdi { get; init; }
    public string? CepTelefonu { get; init; }
    public decimal? Komisyon { get; init; }
    public string? AcenteAdi { get; init; }
    public int? GuncelleyenUyeId { get; init; }
    public string? GuncelleyenUyeAdi { get; init; }
    public sbyte Zeyil { get; init; }
    public int? ZeyilNo { get; init; }
    public int YenilemeDurumu { get; init; }
    public int OnayDurumu { get; init; }

    // Prodüktör ve Şube bilgileri
    public int ProduktorId { get; init; }
    public string? ProduktorAdi { get; init; }
    public int ProduktorSubeId { get; init; }
    public string? ProduktorSubeAdi { get; init; }
    public int SubeId { get; init; }
    public string? SubeAdi { get; init; }
    public int UyeId { get; init; }
    public string? UyeAdi { get; init; }
}

/// <summary>
/// Sayfalanmış poliçe listesi response
/// </summary>
public record PoliceListDto
{
    public List<PoliceListItemDto> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
