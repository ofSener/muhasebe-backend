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
        "Poliçe", "Prim"
    };

    // Sompo'ya özgü kolonlar - içerik bazlı tespit için
    // Not: Bu kombinasyon sadece Sompo formatında var
    protected override string[] SignatureColumns => new[]
    {
        "ÜRÜN NO", "ONAY TANZİM"
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
            var policeNo = GetStringValue(row, "POLİÇE NO", "Poliçe No", "POLICE NO", "Police No");

            // Boş veya header satırlarını atla
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            if (policeNo.ToUpperInvariant().Contains("POLİÇE") ||
                policeNo.ToUpperInvariant().Contains("POLICE"))
                continue;

            // Tarihler - farklı format varyantlarını destekle
            var tanzimTarihi = GetDateValue(row, "ONAY TANZİM TARİHİ", "Onay Tarihi", "ONAY TARİHİ",
                "ONAY TARIHI", "ONAY TANZIM TARIHI", "Tanzim Tarihi");
            var baslangicTarihi = GetDateValue(row, "BAŞLAMA TAR.", "BAŞLAMA TAR", "BASLAMA TAR",
                "Başlangıç Tarihi", "BAŞLANGIÇ TARİHİ", "BASLANGIC TARIHI");
            var bitisTarihi = GetDateValue(row, "BİTİŞ TAR.", "BİTİŞ TAR", "BITIS TAR",
                "Bitiş Tarihi", "BİTİŞ TARİHİ", "BITIS TARIHI");

            // Başlangıç tarihi yoksa tanzim tarihini kullan
            if (!baslangicTarihi.HasValue && tanzimTarihi.HasValue)
                baslangicTarihi = tanzimTarihi;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "YENİLEME NO", "Yenileme No", "YENILEME NO"),
                ZeyilNo = GetStringValue(row, "ZEYİL NO", "Zeyl No", "ZEYL NO", "ZEYIL NO"),
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
                BrutPrim = GetDecimalValue(row, "BRÜT PRİM", "Brüt Prim", "BRUT PRIM"),
                NetPrim = GetDecimalValue(row, "NET PRİM", "Net Prim", "NET PRIM"),
                Komisyon = GetDecimalValue(row, "KOMİSYON", "Komisyon", "KOMISYON"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "SİGORTALI ÜNVANI", "Sigortalı Ünvanı", "SIGORTALI UNVANI")?.Trim(),
                SigortaliSoyadi = null,

                // Araç Bilgileri
                Plaka = GetStringValue(row, "PLAKA", "Plaka"),

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "ACENTE NO", "Acente No", "ACENTE KOD", "ACENTE ÜNVAN")
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
        var urunNo = GetStringValue(row, "ÜRÜN NO", "Ürün No", "URUN NO");

        // Sompo ürün kodları ve TKP gibi kısaltmalar
        return urunNo?.ToUpperInvariant() switch
        {
            "117" => "DASK",
            "115" => "KASKO",
            "101" => "TRAFİK",
            "118" => "KONUT",
            "119" => "İŞYERİ",
            "TKP" => "KASKO",  // TKP = Ticari Kasko Paketi
            "TRF" => "TRAFİK",
            _ => urunNo  // Kod olarak döndür
        };
    }

    private string GetPoliceTipiFromPrim(IDictionary<string, object?> row)
    {
        // Önce İPTAL / YÜRÜRLÜK kolonuna bak
        var iptalYururluk = GetStringValue(row, "?PTAL / YÜRÜRLÜK", "İPTAL / YÜRÜRLÜK", "IPTAL / YURURLUK");
        if (!string.IsNullOrEmpty(iptalYururluk) &&
            iptalYururluk.ToUpperInvariant().Contains("İPTAL"))
        {
            return "İPTAL";
        }

        var brutPrim = GetDecimalValue(row, "BRÜT PRİM", "Brüt Prim", "BRUT PRIM");
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
