using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Doğa Sigorta Excel parser
///
/// Excel Yapısı:
/// - Row 1: Headers
/// - Row 2+: Veriler
///
/// KOLONLAR:
/// Col 1: Branş (310=Kasko, 340=Trafik, vb.)
/// Col 2: Acente
/// Col 3: Acente Açık/Kapalı
/// Col 4: Tali
/// Col 5: Poliçe No
/// Col 6: Dask Poliçe No
/// Col 7: Tecdit No (Yenileme)
/// Col 8: Zeyil No
/// Col 9: Zeyil Kod
/// Col 10: Zeyil Ad
/// Col 11: İpt/Kay (K=Kayıt, İ=İptal)
/// Col 12: Bölge Kodu
/// Col 13: Tanzim Tarihi
/// Col 14: Vade Başlangıç
/// Col 15: Vade Bitiş
/// Col 16: Tarife Kodu
/// Col 17: Sbm Havuz
/// Col 18: Sbm Havuz Primi
/// Col 19: Sigortalı Adı
/// Col 20: Sigortalı Soyadı
/// Col 21: Onay Veren
/// Col 22: Net Prim
/// Col 23: Brüt Prim
/// Col 24: Komisyon
/// Col 25: GV
/// Col 26: THGF
/// Col 27: GHP
/// Col 28: YSV
/// Col 29: Ödeme Tipi
/// Col 30: İptal
/// Col 31: Önceki Poliçe Key
/// Col 32: Acente Temsilcisi
/// Col 33: Üretim Kanalı
/// Col 34: Poliçe Zeyil Key
///
/// BRANŞ KODU EŞLEŞTİRME (Doğa Kodu → BransId):
/// 310 → 1 (Kasko)
/// 340 → 0 (Trafik)
/// 318 → 3 (DASK)
/// </summary>
public class DogaExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 104;
    public override string SirketAdi => "Doğa Sigorta";
    public override string[] FileNamePatterns => new[] { "doga", "doğa", "raporsonuc" };

    protected override string[] RequiredColumns => new[]
    {
        "Branş", "Poliçe No", "Brüt Prim"
    };

    // Doğa'ya özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "İpt/Kay", "Vade Başlangıç", "Vade Bitiş", "Sbm Havuz"
    };

    /// <summary>
    /// Doğa branş kodu → BransId eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, int> BransKoduMapping = new()
    {
        { "310", 1 },   // Kasko
        { "340", 0 },   // Trafik
        { "318", 3 },   // DASK
        { "320", 2 },   // Konut
        { "350", 8 },   // Ferdi Kaza
        { "360", 9 },   // Sorumluluk
        { "370", 5 },   // Nakliyat
        { "380", 4 },   // İşyeri
        { "390", 7 },   // Hayat
        { "410", 6 },   // Sağlık
        { "420", 16 },  // Tamamlayıcı Sağlık
    };

    /// <summary>
    /// Branş kodu → Branş adı eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, string> BransAdiMapping = new()
    {
        { "310", "KASKO" },
        { "340", "TRAFİK" },
        { "318", "DASK" },
        { "320", "KONUT" },
        { "350", "FERDİ KAZA" },
        { "360", "SORUMLULUK" },
        { "370", "NAKLİYAT" },
        { "380", "İŞYERİ" },
        { "390", "HAYAT" },
        { "410", "SAĞLIK" },
        { "420", "TAMAMLAYICI SAĞLIK" },
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // Branş kolonu
            var bransKodu = GetStringValue(row, "Branş", "BRANŞ", "Brans", "BRANS")?.Trim();

            // Boş satırları ve header satırlarını atla
            if (string.IsNullOrWhiteSpace(bransKodu))
                continue;

            if (bransKodu.Contains("Branş", StringComparison.OrdinalIgnoreCase))
                continue;

            // Poliçe No
            var policeNo = GetStringValue(row, "Poliçe No", "POLİÇE NO", "Police No", "POLICE NO")?.Trim();
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Zeyil kontrolü
            var zeyilNo = GetStringValue(row, "Zeyil No", "ZEYİL NO", "Zeyl No", "ZEYL NO");
            var zeyilKodu = GetStringValue(row, "Zeyil Kod", "ZEYİL KOD", "Zeyl Kod");
            var zeyilAdi = GetStringValue(row, "Zeyil Ad", "ZEYİL AD", "Zeyl Ad");
            var isZeyil = IsZeyilPolicy(zeyilNo);

            // Branş eşleştirme
            var bransId = GetBransIdFromKod(bransKodu);
            var bransAdi = GetBransAdiFromKod(bransKodu);

            // Tarihler
            var tanzimTarihi = GetDateValue(row, "Tanzim Tarihi", "TANZİM TARİHİ", "Tanzim Tar");
            var baslangicTarihi = GetDateValue(row, "Vade Başlangıç", "VADE BAŞLANGIÇ", "Vade Baslangic");
            var bitisTarihi = GetDateValue(row, "Vade Bitiş", "VADE BİTİŞ", "Vade Bitis");

            // İptal/Kayıt durumu
            var iptKay = GetStringValue(row, "İpt/Kay", "IPT/KAY", "Ipt/Kay")?.Trim().ToUpperInvariant();
            var iptalDurumu = GetStringValue(row, "İptal", "IPTAL", "Iptal")?.Trim().ToUpperInvariant();

            // Poliçe tipi belirleme
            var policeTipi = GetPoliceTipi(row, iptKay, iptalDurumu);

            // Sigortalı bilgileri
            var sigortaliAdi = GetStringValue(row, "Sigortalı Adı", "SİGORTALI ADI", "Sigortali Adi")?.Trim();
            var sigortaliSoyadi = GetStringValue(row, "Sigortalı Soyadı", "SİGORTALI SOYADI", "Sigortali Soyadi")?.Trim();

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Tecdit No", "TECDİT NO", "Tecdit", "Yenileme No"),
                ZeyilNo = zeyilNo,
                ZeyilTipKodu = zeyilKodu,
                Brans = bransAdi,
                BransId = bransId,
                PoliceTipi = policeTipi,

                // Tarihler
                TanzimTarihi = tanzimTarihi,
                BaslangicTarihi = baslangicTarihi,
                BitisTarihi = bitisTarihi,
                ZeyilOnayTarihi = isZeyil ? tanzimTarihi : null,
                ZeyilBaslangicTarihi = isZeyil ? baslangicTarihi : null,

                // Primler
                BrutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "Brut Prim", "BRUT PRIM"),
                NetPrim = GetDecimalValue(row, "Net Prim", "NET PRİM", "NET PRIM"),
                Komisyon = GetDecimalValue(row, "Komisyon", "KOMİSYON", "KOMISYON"),

                // Müşteri Bilgileri
                SigortaliAdi = sigortaliAdi,
                SigortaliSoyadi = sigortaliSoyadi,
                Tckn = null,             // Doğa Excel'de TC yok
                Vkn = null,              // Doğa Excel'de VKN yok
                Adres = null,            // Doğa Excel'de adres yok

                // Araç/Acente Bilgileri
                Plaka = null,            // Doğa Excel'de plaka yok
                AcenteNo = GetStringValue(row, "Acente", "ACENTE")?.Trim()
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
    /// Branş kodundan BransId döndürür
    /// </summary>
    private static int? GetBransIdFromKod(string? bransKodu)
    {
        if (string.IsNullOrWhiteSpace(bransKodu))
            return null;

        var kod = bransKodu.Trim();
        return BransKoduMapping.TryGetValue(kod, out var bransId) ? bransId : 255; // 255 = Belli Değil
    }

    /// <summary>
    /// Branş kodundan Branş adı döndürür
    /// </summary>
    private static string? GetBransAdiFromKod(string? bransKodu)
    {
        if (string.IsNullOrWhiteSpace(bransKodu))
            return null;

        var kod = bransKodu.Trim();
        return BransAdiMapping.TryGetValue(kod, out var bransAdi) ? bransAdi : kod; // Eşleşme yoksa kodu döndür
    }

    /// <summary>
    /// Poliçe tipini belirler (TAHAKKUK/İPTAL)
    /// </summary>
    private string GetPoliceTipi(IDictionary<string, object?> row, string? iptKay, string? iptalDurumu)
    {
        // İpt/Kay kolonu "İ" ise iptal
        if (iptKay == "İ" || iptKay == "I")
            return "İPTAL";

        // İptal kolonu "E" (Evet) ise iptal
        if (iptalDurumu == "E")
            return "İPTAL";

        // Brüt prim negatifse iptal
        var brutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "Brut Prim", "BRUT PRIM");
        if (brutPrim < 0)
            return "İPTAL";

        return "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.TanzimTarihi.HasValue && !row.BaslangicTarihi.HasValue)
            errors.Add("Tarih bilgisi geçersiz");

        // Zeyil değilse prim kontrolü
        var isZeyil = !string.IsNullOrWhiteSpace(row.ZeyilNo) &&
                      int.TryParse(row.ZeyilNo, out var zeyilNum) &&
                      zeyilNum > 0;

        if (!isZeyil)
        {
            if ((!row.BrutPrim.HasValue || row.BrutPrim == 0) &&
                (!row.NetPrim.HasValue || row.NetPrim == 0))
            {
                errors.Add("Prim bilgisi boş veya sıfır");
            }
        }

        return errors;
    }
}
