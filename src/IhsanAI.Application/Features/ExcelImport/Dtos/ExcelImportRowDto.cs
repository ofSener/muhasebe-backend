namespace IhsanAI.Application.Features.ExcelImport.Dtos;

public record ExcelImportRowDto
{
    public int RowNumber { get; init; }

    // Poliçe Temel Bilgileri
    public string? PoliceNo { get; init; }
    public string? YenilemeNo { get; init; }
    public string? ZeyilNo { get; init; }
    public string? ZeyilTipKodu { get; init; }
    public string? Brans { get; init; }           // TRAFİK, KASKO, DASK vb.
    public string? PoliceTipi { get; init; }      // Tahakkuk/İptal

    // Tarihler
    public DateTime? TanzimTarihi { get; init; }
    public DateTime? BaslangicTarihi { get; init; }
    public DateTime? BitisTarihi { get; init; }
    public DateTime? ZeyilOnayTarihi { get; init; }
    public DateTime? ZeyilBaslangicTarihi { get; init; }

    // Primler
    public decimal? BrutPrim { get; init; }
    public decimal? NetPrim { get; init; }
    public decimal? Komisyon { get; init; }

    // Müşteri Bilgileri
    public string? SigortaliAdi { get; init; }
    public string? SigortaliSoyadi { get; init; }

    // Araç Bilgileri
    public string? Plaka { get; init; }

    // Acente Bilgileri
    public string? AcenteNo { get; init; }

    // Validation
    public bool IsValid { get; init; }
    public List<string> ValidationErrors { get; init; } = new();

    // Mapped IDs (after lookup)
    public int? MusteriId { get; init; }
    public int? BransId { get; init; }
    public int? SigortaSirketiId { get; init; }
}
