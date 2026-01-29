using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Neova Sigorta Excel parser
/// Kolonlar büyük harf
///
/// MAPPING:
/// *PoliceNo       = "POLİÇE NO"                [OK]
/// *YenilemeNo     = YOK                        [NO]
/// *ZeyilNo        = "ZEYİL NO"                 [OK]
/// *ZeyilTipKodu   = "ZEYİL TÜRÜ"               [OK]
/// *Brans          = "FAALİYET KODU"            [WARN]
/// *PoliceTipi     = "G/T"                      [WARN]
/// *TanzimTarihi   = "TANZİM TARİHİ"            [OK]
/// *BaslangicTarihi= "BAŞLANGIÇ TARİHİ"         [OK]
/// *BitisTarihi    = "BİTİŞ TARİHİ"             [OK]
/// *ZeyilOnayTarihi= YOK                        [NO]
/// *ZeyilBaslangicTarihi = YOK                  [NO]
/// *BrutPrim       = "BRÜT PRİM"                [OK]
/// *NetPrim        = "NET PRİM"                 [OK]
/// *Komisyon       = "KOMİSYON"                 [OK]
/// *SigortaliAdi   = "MÜŞTERİ AD/ÜNVAN"         [OK]
/// *SigortaliSoyadi= YOK (birleşik)             [NO]
/// *Plaka          = YOK                        [NO]
/// *AcenteNo       = "ACENTE KOD"               [OK]
/// </summary>
public class NeovaExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 5;
    public override string SirketAdi => "Neova Sigorta";
    public override string[] FileNamePatterns => new[] { "neova", "nva" };

    protected override string[] RequiredColumns => new[]
    {
        "POLİÇE NO", "PRİM", "TARİH"
    };

    // Neova'ya özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "KOD", "G/T", "MÜŞTERİ AD/ÜNVAN"  // Bu kombinasyon sadece Neova'da var
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "POLİÇE NO", "Poliçe No");

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = null,  // Neova'da yok
                ZeyilNo = GetStringValue(row, "ZEYİL NO", "Zeyil No"),
                ZeyilTipKodu = GetStringValue(row, "ZEYİL TÜRÜ", "Zeyil Türü"),
                Brans = GetBransFromKod(row),
                PoliceTipi = GetPoliceTipi(row),

                // Tarihler
                TanzimTarihi = GetDateValue(row, "TANZİM TARİHİ", "Tanzim Tarihi"),
                BaslangicTarihi = GetDateValue(row, "BAŞLANGIÇ TARİHİ", "Başlangıç Tarihi"),
                BitisTarihi = GetDateValue(row, "BİTİŞ TARİHİ", "Bitiş Tarihi"),
                ZeyilOnayTarihi = null,  // Neova'da yok
                ZeyilBaslangicTarihi = null,  // Neova'da yok

                // Primler
                BrutPrim = GetDecimalValue(row, "BRÜT PRİM", "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "NET PRİM", "Net Prim"),
                Komisyon = GetDecimalValue(row, "KOMİSYON", "Komisyon"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "MÜŞTERİ AD/ÜNVAN", "Müşteri Ad/Ünvan")?.Trim(),
                SigortaliSoyadi = null,  // Neova'da birleşik

                // Araç Bilgileri
                Plaka = null,  // Neova'da yok

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "ACENTE KOD", "Acente Kod")
            };

            // Tanzim tarihi yoksa başlangıç tarihini kullan
            if (!dto.TanzimTarihi.HasValue && dto.BaslangicTarihi.HasValue)
            {
                dto = dto with { TanzimTarihi = dto.BaslangicTarihi };
            }

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

    private string? GetBransFromKod(IDictionary<string, object?> row)
    {
        var kod = GetStringValue(row, "KOD");
        var faaliyetKodu = GetStringValue(row, "FAALİYET KODU");

        // Önce KOD'a bak - Neova branş kodları
        var brans = kod?.ToUpperInvariant() switch
        {
            "TR4" => "TRAFİK",
            "TS2" => "KASKO",  // Ticari kasko
            "K23" => "KASKO",
            "DSK" => "DASK",
            "KNT" => "KONUT",
            var x when x?.StartsWith("TR") == true => "TRAFİK",
            var x when x?.StartsWith("TS") == true => "KASKO",
            var x when x?.StartsWith("K") == true => "KASKO",
            _ => null
        };

        // KOD'dan bulunamazsa faaliyet kodunu döndür
        return brans ?? faaliyetKodu;
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

        // Zeyil kontrolü - robust parsing ile (zeyillerde 0 veya negatif prim olabilir)
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil && (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            errors.Add("Brüt Prim boş veya sıfır");
        // Zeyil için prim 0 veya negatif olabilir

        return errors;
    }
}
