namespace IhsanAI.Application.Features.ExcelImport.Dtos;

public record ExcelImportRowDto
{
    public int RowNumber { get; init; }
    public string? PoliceNo { get; init; }
    public string? ZeyilNo { get; init; }
    public string? YenilemeNo { get; init; }
    public string? Plaka { get; init; }
    public DateTime? TanzimTarihi { get; init; }
    public DateTime? BaslangicTarihi { get; init; }
    public DateTime? BitisTarihi { get; init; }
    public decimal? BrutPrim { get; init; }
    public decimal? NetPrim { get; init; }
    public decimal? Komisyon { get; init; }
    public decimal? Vergi { get; init; }
    public string? SigortaliAdi { get; init; }
    public string? TcVkn { get; init; }
    public string? PoliceTipi { get; init; }  // Tahakkuk/İptal
    public string? UrunAdi { get; init; }     // TRAFİK, KASKO, DASK vb.
    public string? AcenteAdi { get; init; }   // AKAY/OİKİ vb.
    public string? Sube { get; init; }
    public string? PoliceKesenPersonel { get; init; }

    // Validation
    public bool IsValid { get; init; }
    public List<string> ValidationErrors { get; init; } = new();

    // Mapped IDs (after lookup)
    public int? MusteriId { get; init; }
    public int? BransId { get; init; }
    public int? IsOrtagiFirmaId { get; init; }
    public int? IsOrtagiSubeId { get; init; }
    public int? IsOrtagiUyeId { get; init; }
}
