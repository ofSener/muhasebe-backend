using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Sompo Sigorta Excel parser
/// Header 3. satırda (EPPlus 1-indexed)
///
/// MAPPING:
/// - PoliceNo       <- "Poliçe No"        ✅
/// - YenilemeNo     <- "Yenileme No"      ✅
/// - ZeyilNo        <- "Zeyl No"          ✅
/// - ZeyilTipKodu   <- YOK                ❌
/// - Brans          <- "Ürün No" (kod)    ⚠️
/// - PoliceTipi     <- YOK                ❌
/// - TanzimTarihi   <- "Onay Tarihi"      ✅
/// - BaslangicTarihi<- "Onay Tarihi"      ⚠️ (aynı)
/// - BitisTarihi    <- YOK                ❌
/// - ZeyilOnayTarihi<- YOK                ❌
/// - ZeyilBaslangicTarihi <- YOK          ❌
/// - BrutPrim       <- "Brüt Prim"        ✅
/// - NetPrim        <- "Net Prim"         ✅
/// - Komisyon       <- "Komisyon"         ✅
/// - SigortaliAdi   <- "Sigortalı Ünvanı" ✅
/// - SigortaliSoyadi<- YOK                ❌
/// - Plaka          <- YOK                ❌
/// - AcenteNo       <- YOK                ❌
/// </summary>
public class SompoExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 6;
    public override string SirketAdi => "Sompo Sigorta";
    public override string[] FileNamePatterns => new[] { "sompo", "smp" };

    /// <summary>
    /// Header satırı auto-detect ile bulunur.
    /// Sompo formatında genellikle 3. satırda ama bazı varyantlarda 1. satırda olabilir.
    /// </summary>
    public override int? HeaderRowIndex => null;

    // İçerik bazlı tespit için genel anahtar kelimeler
    protected override string[] RequiredColumns => new[]
    {
        "Poliçe", "Prim", "Onay"
    };

    // CanParse metodu BaseExcelParser'dan inherit edilir - hem dosya adı hem kolon kontrolü yapar

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // Büyük/küçük harf alternatifleri destekleniyor
            var policeNo = GetStringValue(row, "Poliçe No", "POLİÇE NO", "POLICE NO", "Police No");

            // Boş veya header satırlarını atla
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            if (policeNo.ToUpperInvariant().Contains("POLİÇE") ||
                policeNo.ToUpperInvariant().Contains("POLICE"))
                continue;

            // Tarihler - farklı format varyantlarını destekle
            var tanzimTarihi = GetDateValue(row, "Onay Tarihi", "ONAY TARİHİ", "ONAY TARIHI",
                "ONAY TANZİM TARİHİ", "ONAY TANZIM TARIHI", "Tanzim Tarihi");
            var baslangicTarihi = GetDateValue(row, "BAŞLAMA TAR.", "BAŞLAMA TAR", "BASLAMA TAR",
                "Başlangıç Tarihi", "BAŞLANGIÇ TARİHİ");
            var bitisTarihi = GetDateValue(row, "BİTİŞ TAR.", "BİTİŞ TAR", "BITIS TAR",
                "Bitiş Tarihi", "BİTİŞ TARİHİ");

            // Başlangıç tarihi yoksa tanzim tarihini kullan
            if (!baslangicTarihi.HasValue && tanzimTarihi.HasValue)
                baslangicTarihi = tanzimTarihi;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No", "YENİLEME NO", "YENILEME NO"),
                ZeyilNo = GetStringValue(row, "Zeyl No", "ZEYL NO", "ZEYİL NO"),
                ZeyilTipKodu = null,
                Brans = GetBransFromUrunNo(row),
                PoliceTipi = GetPoliceTipiFromPrim(row),

                // Tarihler
                TanzimTarihi = tanzimTarihi,
                BaslangicTarihi = baslangicTarihi,
                BitisTarihi = bitisTarihi,
                ZeyilOnayTarihi = null,
                ZeyilBaslangicTarihi = null,

                // Primler
                BrutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "BRUT PRIM"),
                NetPrim = GetDecimalValue(row, "Net Prim", "NET PRİM", "NET PRIM"),
                Komisyon = GetDecimalValue(row, "Komisyon", "KOMİSYON", "KOMISYON"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "Sigortalı Ünvanı", "SİGORTALI ÜNVANI", "SIGORTALI UNVANI")?.Trim(),
                SigortaliSoyadi = null,

                // Araç Bilgileri
                Plaka = GetStringValue(row, "Plaka", "PLAKA"),

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "Acente No", "ACENTE NO", "Acente Kod")
            };

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

    private string? GetBransFromUrunNo(IDictionary<string, object?> row)
    {
        var urunNo = GetStringValue(row, "Ürün No", "ÜRÜN NO", "URUN NO");

        return urunNo switch
        {
            "117" => "DASK",
            "115" => "KASKO",
            "101" => "TRAFİK",
            "118" => "KONUT",
            "119" => "İŞYERİ",
            _ => urunNo  // Kod olarak döndür
        };
    }

    private string GetPoliceTipiFromPrim(IDictionary<string, object?> row)
    {
        var brutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "BRUT PRIM");
        return brutPrim < 0 ? "İPTAL" : "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.TanzimTarihi.HasValue && !row.BaslangicTarihi.HasValue)
            errors.Add("Tarih bilgisi geçersiz");

        if ((!row.BrutPrim.HasValue || row.BrutPrim == 0) &&
            (!row.NetPrim.HasValue || row.NetPrim == 0))
        {
            errors.Add("Prim bilgisi boş veya sıfır");
        }

        return errors;
    }
}
