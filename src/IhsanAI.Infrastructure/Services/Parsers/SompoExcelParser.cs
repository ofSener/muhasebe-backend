using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Sompo Sigorta Excel parser
///
/// Excel Yapısı:
/// - Row 1: Başlık (Acente bilgisi)
/// - Row 2: Tarih aralığı
/// - Row 3: Headers
/// - Row 4+: Veriler
///
/// KOLONLAR (Row 3):
/// Col 1: Ürün No
/// Col 2: Poliçe No
/// Col 3: Yenileme No
/// Col 4: Zeyl No
/// Col 5: Onay Tarihi
/// Col 6: Sigortalı Ünvanı
/// Col 7: Net Prim
/// Col 8: Brüt Prim
/// Col 9: Döviz Cinsi
/// Col 10: Komisyon
/// Col 11: GDV
/// Col 12: YSV
/// Col 13: THGF
/// Col 14: GF
///
/// ÜRÜN KODU EŞLEŞTİRME (Sompo Kodu → BransId):
/// 311 → 0 (Trafik)
/// 307, 333 → 1 (Kasko)
/// 117 → 2 (Dask)
/// 460 → 3 (Ferdi Kaza)
/// 403 → 4 (Koltuk)
/// 110 → 5 (Konut)
/// 205 → 6 (Nakliyat)
/// 455 → 8 (Seyahat Sağlık)
/// 106, 438 → 9 (İşyeri)
/// 321 → 12 (IMM)
/// 805 → 15 (Yabancı Sağlık)
/// 804 → 16 (Tamamlayıcı Sağlık)
/// 201 → 24 (Hukuksal Koruma)
/// 303 → 27 (Yeşil Kart)
/// 512, 513 → 28 (Mühendislik)
/// 470 → 29 (Sorumluluk)
/// </summary>
public class SompoExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 61;
    public override string SirketAdi => "Sompo Sigorta";
    public override string[] FileNamePatterns => new[] { "sompo", "smp" };

    /// <summary>
    /// Header satırı 3. satırda (1-indexed)
    /// </summary>
    public override int? HeaderRowIndex => 3;

    protected override string[] RequiredColumns => new[]
    {
        "Ürün No", "Poliçe No", "Prim"
    };

    // Sompo'ya özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "Ürün No", "Sigortalı Ünvanı", "Döviz Cinsi"
    };

    /// <summary>
    /// Sompo ürün kodu → BransId eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, int> UrunKoduMapping = new()
    {
        { "311", 0 },   // Trafik
        { "307", 1 },   // Kasko
        { "333", 1 },   // Kasko
        { "117", 2 },   // Dask
        { "460", 3 },   // Ferdi Kaza
        { "403", 4 },   // Koltuk
        { "110", 5 },   // Konut
        { "205", 6 },   // Nakliyat
        { "455", 8 },   // Seyahat Sağlık
        { "106", 9 },   // İşyeri
        { "438", 9 },   // İşyeri
        { "321", 12 },  // IMM
        { "805", 15 },  // Yabancı Sağlık
        { "804", 16 },  // Tamamlayıcı Sağlık
        { "201", 24 },  // Hukuksal Koruma
        { "303", 27 },  // Yeşil Kart
        { "512", 28 },  // Mühendislik
        { "513", 28 },  // Mühendislik
        { "470", 29 },  // Sorumluluk
    };

    /// <summary>
    /// Ürün kodu → Branş adı eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, string> UrunAdiMapping = new()
    {
        { "311", "TRAFİK" },
        { "307", "KASKO" },
        { "333", "KASKO" },
        { "117", "DASK" },
        { "460", "FERDİ KAZA" },
        { "403", "KOLTUK" },
        { "110", "KONUT" },
        { "205", "NAKLİYAT" },
        { "455", "SEYAHAT SAĞLIK" },
        { "106", "İŞYERİ" },
        { "438", "İŞYERİ" },
        { "321", "IMM" },
        { "805", "YABANCI SAĞLIK" },
        { "804", "TAMAMLAYICI SAĞLIK" },
        { "201", "HUKUKSAL KORUMA" },
        { "303", "YEŞİL KART" },
        { "512", "MÜHENDİSLİK" },
        { "513", "MÜHENDİSLİK" },
        { "470", "SORUMLULUK" },
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // Ürün No kolonu
            var urunNo = GetStringValue(row, "Ürün No", "ÜRÜN NO", "Urun No", "URUN NO")?.Trim();

            // Boş satırları, header satırlarını ve toplam satırlarını atla
            if (string.IsNullOrWhiteSpace(urunNo))
                continue;

            if (urunNo.Contains("Ürün", StringComparison.OrdinalIgnoreCase) ||
                urunNo.Contains("Prim", StringComparison.OrdinalIgnoreCase) ||
                urunNo.Contains("Toplam", StringComparison.OrdinalIgnoreCase))
                continue;

            // Poliçe No
            var policeNo = GetStringValue(row, "Poliçe No", "POLİÇE NO", "Police No", "POLICE NO")?.Trim();
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Zeyil kontrolü
            var zeyilNo = GetStringValue(row, "Zeyl No", "ZEYL NO", "Zeyil No", "ZEYİL NO");
            var isZeyil = IsZeyilPolicy(zeyilNo);

            // Ürün kodu eşleştirme
            var bransId = GetBransIdFromUrunNo(urunNo);
            var bransAdi = GetBransAdiFromUrunNo(urunNo);

            // Onay Tarihi
            var onayTarihi = GetDateValue(row, "Onay Tarihi", "ONAY TARİHİ", "Onay Tar");

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No", "YENİLEME NO", "Yenileme"),
                ZeyilNo = zeyilNo,
                ZeyilTipKodu = null,
                Brans = bransAdi,
                BransId = bransId,
                PoliceTipi = GetPoliceTipiFromPrim(row),

                // Tarihler - Sompo Excel'de sadece Onay Tarihi var
                TanzimTarihi = onayTarihi,
                BaslangicTarihi = null,        // Sompo Excel'de başlangıç tarihi yok
                BitisTarihi = null,            // Sompo Excel'de bitiş tarihi yok
                ZeyilOnayTarihi = isZeyil ? onayTarihi : null,
                ZeyilBaslangicTarihi = null,

                // Primler
                BrutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "Brut Prim", "BRUT PRIM"),
                NetPrim = GetDecimalValue(row, "Net Prim", "NET PRİM", "NET PRIM"),
                Komisyon = GetDecimalValue(row, "Komisyon", "KOMİSYON", "KOMISYON"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "Sigortalı Ünvanı", "SİGORTALI ÜNVANI", "Sigortali Unvani", "SIGORTALI UNVANI")?.Trim(),
                SigortaliSoyadi = null,  // Sompo'da ayrı soyad yok
                Tckn = null,             // Sompo Excel'de TC yok
                Vkn = null,              // Sompo Excel'de VKN yok
                Adres = null,            // Sompo Excel'de adres yok

                // Araç/Acente Bilgileri
                Plaka = null,            // Sompo Excel'de plaka yok
                AcenteNo = null          // Sompo Excel'de acente no yok
            };

            // Validasyon
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

    /// <summary>
    /// Sompo ürün kodundan BransId döndürür
    /// </summary>
    private static int? GetBransIdFromUrunNo(string? urunNo)
    {
        if (string.IsNullOrWhiteSpace(urunNo))
            return null;

        var kod = urunNo.Trim();
        return UrunKoduMapping.TryGetValue(kod, out var bransId) ? bransId : 255; // 255 = Belli Değil
    }

    /// <summary>
    /// Sompo ürün kodundan Branş adı döndürür
    /// </summary>
    private static string? GetBransAdiFromUrunNo(string? urunNo)
    {
        if (string.IsNullOrWhiteSpace(urunNo))
            return null;

        var kod = urunNo.Trim();
        return UrunAdiMapping.TryGetValue(kod, out var bransAdi) ? bransAdi : kod; // Eşleşme yoksa kodu döndür
    }

    /// <summary>
    /// Prim değerine göre poliçe tipini belirler
    /// </summary>
    private string GetPoliceTipiFromPrim(IDictionary<string, object?> row)
    {
        var brutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "Brut Prim", "BRUT PRIM");
        return brutPrim < 0 ? "İPTAL" : "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.TanzimTarihi.HasValue && !row.BaslangicTarihi.HasValue)
            errors.Add("Tarih bilgisi geçersiz");

        // Zeyil kontrolü
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil)
        {
            // Yeni poliçelerde prim pozitif olmalı
            if ((!row.BrutPrim.HasValue || row.BrutPrim == 0) &&
                (!row.NetPrim.HasValue || row.NetPrim == 0))
            {
                errors.Add("Prim bilgisi boş veya sıfır");
            }
        }
        // Zeyil için prim 0 veya negatif olabilir

        return errors;
    }
}
