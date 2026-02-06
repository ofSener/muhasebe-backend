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
/// Col 1: Branş (310=Trafik, 340=Kasko, vb.)
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
/// 310 → 0 (Trafik), 340/346 → 1 (Kasko), 750 → 27 (Yeşil Kart)
/// 251/255/256/258/259/260/262/265/268/269 → 3 (Ferdi Kaza), 263 → 4 (Koltuk)
/// 420 → 6 (Nakliyat), 615 → 7 (Sağlık), 298/299/300/302 → 8 (Seyahat Sağlık)
/// 270 → 9 (İşyeri), 610 → 16 (Tamamlayıcı Sağlık), 600 → 17 (Yabancı Sağlık)
/// 510/520/530/540/544 → 28 (Mühendislik), 280/281/286 → 29 (Sorumluluk)
/// 285 → 31 (Tehlikeli Madde), 277 → 32 (Tıbbi Sorumluluk), 301 → 33 (Eğitim)
/// </summary>
public class DogaExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 104;
    public override string SirketAdi => "Doğa Sigorta";
    public override string[] FileNamePatterns => new[] { "doga", "doğa" };

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
        // Trafik & Kasko
        { "310", 0 },   // Trafik
        { "340", 1 },   // Kasko
        { "346", 1 },   // Kasko
        { "750", 27 },  // Yeşil Kart

        // Ferdi Kaza
        { "251", 3 },   // Ferdi Kaza
        { "255", 3 },   // Ferdi Kaza
        { "256", 3 },   // Ferdi Kaza
        { "258", 3 },   // Ferdi Kaza
        { "259", 3 },   // Ferdi Kaza
        { "260", 3 },   // Ferdi Kaza
        { "262", 3 },   // Ferdi Kaza
        { "265", 3 },   // Ferdi Kaza
        { "268", 3 },   // Ferdi Kaza
        { "269", 3 },   // Ferdi Kaza

        // Koltuk
        { "263", 4 },   // Koltuk

        // Nakliyat
        { "420", 6 },   // Nakliyat

        // Sağlık
        { "615", 7 },   // Sağlık
        { "298", 8 },   // Seyahat Sağlık
        { "299", 8 },   // Seyahat Sağlık
        { "300", 8 },   // Seyahat Sağlık
        { "302", 8 },   // Seyahat Sağlık
        { "610", 16 },  // Tamamlayıcı Sağlık
        { "600", 17 },  // Yabancı Sağlık

        // İşyeri & Sorumluluk
        { "270", 9 },   // İşyeri
        { "280", 29 },  // Sorumluluk
        { "281", 29 },  // Sorumluluk
        { "286", 29 },  // Sorumluluk
        { "285", 31 },  // Tehlikeli Madde
        { "277", 32 },  // Tıbbi Sorumluluk

        // Mühendislik
        { "510", 28 },  // Mühendislik
        { "520", 28 },  // Mühendislik
        { "530", 28 },  // Mühendislik
        { "540", 28 },  // Mühendislik
        { "544", 28 },  // Mühendislik

        // Eğitim
        { "301", 33 },  // Eğitim
    };

    /// <summary>
    /// Branş kodu → Branş adı eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, string> BransAdiMapping = new()
    {
        // Trafik & Kasko
        { "310", "TRAFİK" },
        { "340", "KASKO" },
        { "346", "KASKO" },
        { "750", "YEŞİL KART" },

        // Ferdi Kaza
        { "251", "FERDİ KAZA" },
        { "255", "FERDİ KAZA" },
        { "256", "FERDİ KAZA" },
        { "258", "FERDİ KAZA" },
        { "259", "FERDİ KAZA" },
        { "260", "FERDİ KAZA" },
        { "262", "FERDİ KAZA" },
        { "265", "FERDİ KAZA" },
        { "268", "FERDİ KAZA" },
        { "269", "FERDİ KAZA" },

        // Koltuk
        { "263", "KOLTUK" },

        // Nakliyat
        { "420", "NAKLİYAT" },

        // Sağlık
        { "615", "SAĞLIK" },
        { "298", "SEYAHAT SAĞLIK" },
        { "299", "SEYAHAT SAĞLIK" },
        { "300", "SEYAHAT SAĞLIK" },
        { "302", "SEYAHAT SAĞLIK" },
        { "610", "TAMAMLAYICI SAĞLIK" },
        { "600", "YABANCI SAĞLIK" },

        // İşyeri & Sorumluluk
        { "270", "İŞYERİ" },
        { "280", "SORUMLULUK" },
        { "281", "SORUMLULUK" },
        { "286", "SORUMLULUK" },
        { "285", "TEHLİKELİ MADDE" },
        { "277", "TIBBİ SORUMLULUK" },

        // Mühendislik
        { "510", "MÜHENDİSLİK" },
        { "520", "MÜHENDİSLİK" },
        { "530", "MÜHENDİSLİK" },
        { "540", "MÜHENDİSLİK" },
        { "544", "MÜHENDİSLİK" },

        // Eğitim
        { "301", "EĞİTİM" },
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

            // Branş sayısal olmalı (310, 340 gibi) - özet satırlarını atla
            if (!int.TryParse(bransKodu, out _))
                continue;

            // Poliçe No
            var policeNo = GetStringValue(row, "Poliçe No", "POLİÇE NO", "Police No", "POLICE NO")?.Trim();
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Poliçe No sayısal olmalı - özet satırlarını atla
            if (!long.TryParse(policeNo, out _))
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

            // Poliçe tipi belirleme (İptal kontrolü)
            var isIptal = IsIptalPolicy(iptKay, iptalDurumu);
            var policeTipi = isIptal ? "İPTAL" : "TAHAKKUK";

            // Sigortalı bilgileri - Ad ve Soyadı birleştir
            // NOT: GetStringValue Contains ile eşleştirir, "Sigortalı Adı" substring olarak
            // "Sigortalı Soyadı"nda da bulunur. Bu yüzden önce soyadını alıyoruz,
            // sonra adı alırken soyadı key'ini hariç tutuyoruz.
            var sigortaliSoyadi = GetExactColumnValue(row, "Sigortalı Soyadı")?.Trim();
            var sigortaliAdi = GetExactColumnValue(row, "Sigortalı Adı")?.Trim();
            var sigortaliTam = CombineNames(sigortaliAdi, sigortaliSoyadi);

            // Primler
            var brutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "Brut Prim", "BRUT PRIM");
            var netPrim = GetDecimalValue(row, "Net Prim", "NET PRİM", "NET PRIM");
            var komisyon = GetDecimalValue(row, "Komisyon", "KOMİSYON", "KOMISYON");

            // İptal ise pozitif primleri negatife çevir
            if (isIptal)
            {
                if (brutPrim.HasValue && brutPrim > 0)
                    brutPrim = -brutPrim;
                if (netPrim.HasValue && netPrim > 0)
                    netPrim = -netPrim;
                if (komisyon.HasValue && komisyon > 0)
                    komisyon = -komisyon;
            }

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
                BrutPrim = brutPrim,
                NetPrim = netPrim,
                Komisyon = komisyon,

                // Müşteri Bilgileri
                SigortaliAdi = sigortaliTam,
                SigortaliSoyadi = null,  // Ad ve soyad birleştirildi
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
    /// Kolon adıyla tam eşleşme yaparak değer döndürür (Contains yerine)
    /// "Sigortalı Adı" / "Sigortalı Soyadı" gibi birbirini içeren kolon adları için gerekli
    /// </summary>
    private static string? GetExactColumnValue(IDictionary<string, object?> row, string columnName)
    {
        var normalized = NormalizeColumnName(columnName);
        var key = row.Keys.FirstOrDefault(k => NormalizeColumnName(k) == normalized);

        if (key != null && row.TryGetValue(key, out var value) && value != null)
        {
            var strValue = value.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(strValue))
                return strValue;
        }
        return null;
    }

    /// <summary>
    /// Branş kodundan BransId döndürür
    /// </summary>
    protected virtual int? GetBransIdFromKod(string? bransKodu)
    {
        if (string.IsNullOrWhiteSpace(bransKodu))
            return null;

        var kod = bransKodu.Trim();
        return BransKoduMapping.TryGetValue(kod, out var bransId) ? bransId : 255; // 255 = Belli Değil
    }

    /// <summary>
    /// Branş kodundan Branş adı döndürür
    /// </summary>
    protected virtual string? GetBransAdiFromKod(string? bransKodu)
    {
        if (string.IsNullOrWhiteSpace(bransKodu))
            return null;

        var kod = bransKodu.Trim();
        return BransAdiMapping.TryGetValue(kod, out var bransAdi) ? bransAdi : kod; // Eşleşme yoksa kodu döndür
    }

    /// <summary>
    /// İptal poliçesi mi kontrol eder
    /// </summary>
    private static bool IsIptalPolicy(string? iptKay, string? iptalDurumu)
    {
        // İpt/Kay kolonu "İ" ise iptal
        if (iptKay == "İ" || iptKay == "I")
            return true;

        // İptal kolonu "E" (Evet) ise iptal
        if (iptalDurumu == "E")
            return true;

        return false;
    }

    /// <summary>
    /// Ad ve soyadı birleştirir
    /// </summary>
    private static string? CombineNames(string? adi, string? soyadi)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(adi))
            parts.Add(adi.Trim());

        if (!string.IsNullOrWhiteSpace(soyadi))
            parts.Add(soyadi.Trim());

        return parts.Count > 0 ? string.Join(" ", parts) : null;
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
