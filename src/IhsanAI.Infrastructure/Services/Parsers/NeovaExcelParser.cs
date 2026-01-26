using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Neova Sigorta Excel parser
/// Kolonlar: POLİÇE NO, ZEYİL NO, ZEYİL TÜRÜ, TANZİM TARİHİ, BAŞLANGIÇ TARİHİ,
/// BİTİŞ TARİHİ, MÜŞTERİ TCKN / VKN, MÜŞTERİ AD/ÜNVAN, NET PRİM, BRÜT PRİM,
/// KOMİSYON, ACENTE ADI
/// NOT: Neova formatında Plaka ve Yenileme No YOKTUR!
/// </summary>
public class NeovaExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 5; // Neova Sigorta ID'si
    public override string SirketAdi => "Neova Sigorta";
    public override string[] FileNamePatterns => new[] { "neova", "nva" };

    protected override string[] RequiredColumns => new[]
    {
        "POLİÇE NO", "BRÜT PRİM", "BAŞLANGIÇ"
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // Poliçe No'yu al - Neova'da büyük harf
            var policeNo = GetStringValue(row, "POLİÇE NO", "Poliçe No");

            // Boş satırları atla
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,
                PoliceNo = policeNo,
                YenilemeNo = null, // Neova'da yenileme no yok
                ZeyilNo = GetStringValue(row, "ZEYİL NO", "Zeyil No"),

                // Tarihler
                TanzimTarihi = GetDateValue(row, "TANZİM TARİHİ", "Tanzim Tarihi"),
                BaslangicTarihi = GetDateValue(row, "BAŞLANGIÇ TARİHİ", "Başlangıç Tarihi"),
                BitisTarihi = GetDateValue(row, "BİTİŞ TARİHİ", "Bitiş Tarihi"),

                // Prim ve komisyon
                BrutPrim = GetDecimalValue(row, "BRÜT PRİM", "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "NET PRİM", "Net Prim"),
                Komisyon = GetDecimalValue(row, "KOMİSYON", "Komisyon"),
                Vergi = GetDecimalValue(row, "NET VERGİ", "Vergi"),

                // Sigortalı bilgileri
                SigortaliAdi = GetStringValue(row, "MÜŞTERİ AD/ÜNVAN", "Müşteri Ad/Ünvan")?.Trim(),
                TcVkn = GetStringValue(row, "MÜŞTERİ TCKN / VKN", "MÜŞTERİ TCKN/VKN", "TC/VKN"),
                Plaka = null, // Neova'da plaka yok

                // Poliçe tipi
                PoliceTipi = GetPoliceTipi(row),

                // Ürün adı - KOD kolonundan tespit edilebilir
                UrunAdi = DetectUrunFromKod(row),

                // Acente bilgisi
                AcenteAdi = GetStringValue(row, "ACENTE ADI", "Acente Adı"),
                Sube = null,
                PoliceKesenPersonel = GetStringValue(row, "TEMSİLCİ", "Temsilci")
            };

            // Tanzim tarihi yoksa başlangıç tarihini kullan
            if (!dto.TanzimTarihi.HasValue && dto.BaslangicTarihi.HasValue)
            {
                dto = dto with { TanzimTarihi = dto.BaslangicTarihi };
            }

            // Validation
            var errors = ValidateRow(dto);
            dto = dto with
            {
                IsValid = errors.Count == 0,
                ValidationErrors = errors
            };

            result.Add(dto);
        }

        return result;
    }

    private string? DetectUrunFromKod(IDictionary<string, object?> row)
    {
        var kod = GetStringValue(row, "KOD");

        return kod?.ToUpperInvariant() switch
        {
            "TR4" => "TRAFİK",
            "K23" => "KASKO",
            "DSK" => "DASK",
            _ => null
        };
    }

    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        var zeyilTuru = GetStringValue(row, "ZEYİL TÜRÜ", "Zeyil Türü");

        if (!string.IsNullOrEmpty(zeyilTuru) &&
            (zeyilTuru.ToUpperInvariant().Contains("İPTAL") ||
             zeyilTuru.ToUpperInvariant().Contains("IPTAL") ||
             zeyilTuru.ToUpperInvariant().Contains("FESİH") ||
             zeyilTuru.ToUpperInvariant().Contains("FESIH")))
        {
            return "İPTAL";
        }

        // Brüt prim negatifse iptal
        var brutPrim = GetDecimalValue(row, "BRÜT PRİM", "Brüt Prim");
        if (brutPrim < 0)
            return "İPTAL";

        return "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue)
            errors.Add("Başlangıç Tarihi geçersiz");

        if (!row.BrutPrim.HasValue || row.BrutPrim == 0)
            errors.Add("Brüt Prim boş veya sıfır");

        // TC/VKN varsa format kontrolü (ama zorunlu değil)

        return errors;
    }
}
