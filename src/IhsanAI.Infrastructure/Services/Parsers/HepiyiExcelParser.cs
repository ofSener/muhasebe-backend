using System.Globalization;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Hepiyi Sigorta Excel parser
/// Primler ve tarihler TEXT formatında geliyor
///
/// KOLONLAR (67 kolon):
/// Col 1:  Poliçe No
/// Col 2:  Zeyl No
/// Col 3:  Poliçe Tarih          (TEXT: dd/MM/yyyy)
/// Col 4:  Poliçe Bitiş Tarih    (TEXT: dd/MM/yyyy)
/// Col 5:  Tanzim Tarih          (TEXT: dd/MM/yyyy)
/// Col 6:  Police Tür Kod        (202=DASK, 301=Kasko, 351=Trafik, 600/602/603=Sağlık)
/// Col 7:  TC                    (TCKN - 11 haneli)
/// Col 8:  Müşteri Ad - Soyad
/// Col 10: Plaka
/// Col 20: Net Prim              (TEXT: "1.504,16" Türkçe format)
/// Col 21: Brüt Prim             (TEXT: "1.504,16" Türkçe format)
/// Col 22: Komisyon              (TEXT: "300,83" Türkçe format)
/// Col 23: VERGI_KIMLIK_NUMARASI (VKN - 10 haneli)
/// Col 24: MUSTERI_UNVANI
/// Col 28: YENILEME_NO
/// Col 29: PARTAJ
/// Col 30: URUN_ADI              (Branş adı)
/// Col 31: ZEYIL_TIPI            (İptal tespiti için)
/// Col 32: ZEYL_TIP_KODU
///
/// POLİCE TÜR KODLARI → BransId:
/// 202 → 2 (DASK)
/// 301 → 1 (Kasko)
/// 351 → 0 (Trafik)
/// 600 → 8 (Seyahat Sağlık)
/// 602 → 16 (Tamamlayıcı Sağlık)
/// 603 → 15 (Yabancı Sağlık)
///
/// URUN_ADI → BransId:
/// ZORUNLU DEPREM SIGORTASI → 2 (DASK)
/// TRAFIK → 0 (Trafik)
/// GENİŞLETİLMİŞ KASKO → 1 (Kasko)
/// SEYAHAT SAGLIK → 8 (Seyahat Sağlık)
/// TAMAMLAYICI SAGLIK → 16 (Tamamlayıcı Sağlık)
/// YABANCILAR İÇİN SAĞLIK → 15 (Yabancı Sağlık)
/// </summary>
public class HepiyiExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 126;
    public override string SirketAdi => "Hepiyi Sigorta";
    public override string[] FileNamePatterns => new[] { "hepiyi", "hepıyı", "hepi̇yi̇", "hepiı", "hepİyİ" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Prim", "Tarih"
    };

    // Hepiyi'ye özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "Müşteri Ad", "Poliçe Tarih", "Police Tür Kod"  // Bu kombinasyon sadece Hepiyi'de var
    };

    /// <summary>
    /// Police Tür Kod → BransId eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, int> PoliceTurKoduMapping = new()
    {
        { "202", 2 },   // DASK
        { "301", 1 },   // Kasko
        { "351", 0 },   // Trafik
        { "600", 8 },   // Seyahat Sağlık
        { "602", 16 },  // Tamamlayıcı Sağlık
        { "603", 15 },  // Yabancı Sağlık
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "Poliçe No", "Police No");

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Sigortalı adı - önce "Müşteri Ad - Soyad", yoksa "MUSTERI_UNVANI"
            var sigortaliAdi = GetStringValue(row, "Müşteri Ad - Soyad", "Müşteri Ad – Soyad", "Musteri Ad - Soyad");
            if (string.IsNullOrEmpty(sigortaliAdi))
                sigortaliAdi = GetStringValue(row, "MUSTERI_UNVANI");

            // TC/VKN ayrıştır
            var tc = GetStringValue(row, "TC");
            var vkn = GetStringValue(row, "VERGI_KIMLIK_NUMARASI");
            string? tckn = null;
            string? vknFinal = null;

            // TC kolonundan TCKN veya VKN belirle
            if (!string.IsNullOrWhiteSpace(tc))
            {
                tc = tc.Trim();
                if (tc.Length == 11 && tc.All(char.IsDigit))
                    tckn = tc;
                else if (tc.Length == 10 && tc.All(char.IsDigit))
                    vknFinal = tc;
            }

            // VKN kolonu varsa ve henüz VKN belirlenmemişse
            if (string.IsNullOrEmpty(vknFinal) && !string.IsNullOrWhiteSpace(vkn))
            {
                vkn = vkn.Trim();
                if (vkn.Length == 10 && vkn.All(char.IsDigit))
                    vknFinal = vkn;
            }

            // Branş tespiti: önce Police Tür Kod, yoksa URUN_ADI
            var policeTurKod = GetStringValue(row, "Police Tür Kod", "Police Tur Kod");
            var urunAdi = GetStringValue(row, "URUN_ADI");
            var bransId = GetBransIdFromPoliceTurKod(policeTurKod) ?? GetBransIdFromUrunAdi(urunAdi);
            var bransAdi = GetBransAdiFromBransId(bransId) ?? urunAdi;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "YENILEME_NO"),
                ZeyilNo = GetStringValue(row, "Zeyl No", "Zeyl_No"),
                ZeyilTipKodu = GetStringValue(row, "ZEYL_TIP_KODU"),
                Brans = bransAdi,
                BransId = bransId,
                PoliceTipi = GetPoliceTipi(row),

                // Tarihler - TEXT formatında geliyor (dd/MM/yyyy)
                TanzimTarihi = GetHepiyiDateValue(row, "Tanzim Tarih"),
                BaslangicTarihi = GetHepiyiDateValue(row, "Poliçe Tarih", "Police Tarih"),
                BitisTarihi = GetHepiyiDateValue(row, "Poliçe Bitiş Tarih", "Police Bitis Tarih"),
                ZeyilOnayTarihi = null,
                ZeyilBaslangicTarihi = null,

                // Primler - TEXT formatında Türkçe format ("1.504,16")
                BrutPrim = GetTurkishDecimalValue(row, "Brüt Prim", "Brut Prim"),
                NetPrim = GetTurkishDecimalValue(row, "Net Prim"),
                Komisyon = GetTurkishDecimalValue(row, "Komisyon"),

                // Müşteri Bilgileri
                SigortaliAdi = sigortaliAdi?.Trim(),
                SigortaliSoyadi = null,  // Hepiyi'de birleşik
                Tckn = tckn,
                Vkn = vknFinal,

                // Araç Bilgileri
                Plaka = GetStringValue(row, "Plaka"),

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "PARTAJ")
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

    /// <summary>
    /// Police Tür Kod'dan BransId çıkarır
    /// </summary>
    private int? GetBransIdFromPoliceTurKod(string? policeTurKod)
    {
        if (string.IsNullOrWhiteSpace(policeTurKod))
            return null;

        if (PoliceTurKoduMapping.TryGetValue(policeTurKod.Trim(), out var bransId))
            return bransId;

        return null;
    }

    /// <summary>
    /// BransId'den standart branş adı döndürür
    /// </summary>
    private static string? GetBransAdiFromBransId(int? bransId)
    {
        return bransId switch
        {
            0 => "TRAFİK",
            1 => "KASKO",
            2 => "DASK",
            3 => "FERDİ KAZA",
            4 => "KOLTUK",
            5 => "KONUT",
            6 => "NAKLİYAT",
            7 => "SAĞLIK",
            8 => "SEYAHAT SAĞLIK",
            9 => "İŞYERİ",
            10 => "ZKTM",
            12 => "IMM",
            15 => "YABANCI SAĞLIK",
            16 => "TAMAMLAYICI SAĞLIK",
            17 => "MAKBUZ",
            19 => "DOĞAL KORUMA",
            20 => "TARIM",
            21 => "YANGIN",
            24 => "HUKUKSAL KORUMA",
            25 => "TEKNE",
            26 => "HAYAT",
            27 => "YEŞİL KART",
            28 => "MÜHENDİSLİK",
            29 => "SORUMLULUK",
            30 => "YOL DESTEK",
            255 => "DİĞER",
            _ => null
        };
    }

    /// <summary>
    /// URUN_ADI'ndan BransId çıkarır
    /// </summary>
    private static int? GetBransIdFromUrunAdi(string? urunAdi)
    {
        if (string.IsNullOrWhiteSpace(urunAdi))
            return null;

        var value = urunAdi.ToUpperInvariant()
            .Replace("İ", "I")
            .Replace("Ğ", "G")
            .Replace("Ü", "U")
            .Replace("Ş", "S")
            .Replace("Ö", "O")
            .Replace("Ç", "C");

        // DASK
        if (value.Contains("DASK") || value.Contains("DEPREM"))
            return 2;

        // Trafik
        if (value.Contains("TRAFIK"))
            return 0;

        // Kasko
        if (value.Contains("KASKO"))
            return 1;

        // Seyahat Sağlık
        if (value.Contains("SEYAHAT"))
            return 8;

        // Tamamlayıcı Sağlık
        if (value.Contains("TAMAMLAYICI"))
            return 16;

        // Yabancı Sağlık
        if (value.Contains("YABANCI") && value.Contains("SAGLIK"))
            return 15;

        // Genel Sağlık
        if (value.Contains("SAGLIK"))
            return 7;

        return 255; // Belli Değil
    }

    /// <summary>
    /// Hepiyi'ye özel tarih parsing - TEXT formatında geliyor (dd/MM/yyyy)
    /// </summary>
    private static DateTime? GetHepiyiDateValue(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        foreach (var col in possibleColumns)
        {
            var key = row.Keys.FirstOrDefault(k =>
                NormalizeColumnName(k).Contains(NormalizeColumnName(col)) ||
                NormalizeColumnName(col).Contains(NormalizeColumnName(k)));

            if (key != null && row.TryGetValue(key, out var value) && value != null)
            {
                // DateTime olarak gelmiş olabilir
                if (value is DateTime dt)
                    return dt;

                // Double olarak gelmiş olabilir (OLE Automation Date)
                if (value is double dblValue && dblValue > 1 && dblValue < 100000)
                {
                    try { return DateTime.FromOADate(dblValue); }
                    catch { }
                }

                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(strValue)) continue;

                // Hepiyi formatları - TEXT olarak geliyor
                var formats = new[]
                {
                    "dd/MM/yyyy",           // 31/12/2025
                    "d/M/yyyy",             // 1/1/2025
                    "dd/MM/yyyy HH:mm:ss",
                    "dd.MM.yyyy",
                    "d.M.yyyy",
                    "yyyy-MM-dd",
                    "yyyy-MM-dd HH:mm:ss",
                };

                if (DateTime.TryParseExact(strValue, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                    return parsed;

                // Türkçe kültür ile dene
                if (DateTime.TryParse(strValue, new CultureInfo("tr-TR"), DateTimeStyles.None, out parsed))
                    return parsed;

                // Genel parse
                if (DateTime.TryParse(strValue, out parsed))
                    return parsed;
            }
        }
        return null;
    }

    /// <summary>
    /// Türkçe formatındaki decimal değeri parse eder ("1.504,16" gibi)
    /// Hepiyi'de primler TEXT olarak geliyor
    /// </summary>
    private decimal? GetTurkishDecimalValue(IDictionary<string, object?> row, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            var actualKey = row.Keys.FirstOrDefault(k =>
                NormalizeColumnName(k).Contains(NormalizeColumnName(key)) ||
                NormalizeColumnName(key).Contains(NormalizeColumnName(k)));

            if (actualKey != null && row.TryGetValue(actualKey, out var value) && value != null)
            {
                // Önce standart decimal dene
                if (value is decimal d)
                    return d;
                if (value is double dbl)
                    return (decimal)dbl;
                if (value is int i)
                    return i;
                if (value is long l)
                    return l;

                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrEmpty(strValue))
                    continue;

                // "0,00" gibi değerleri kontrol et
                if (strValue == "0,00" || strValue == "0.00" || strValue == "0")
                    return 0;

                // Türkçe format: "1.504,16" -> binlik ayracı nokta, ondalık ayracı virgül
                // 1. Noktaları kaldır (binlik ayracı)
                // 2. Virgülü noktaya çevir (ondalık ayracı)
                strValue = strValue.Replace(".", "").Replace(",", ".");

                if (decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                    return result;
            }
        }

        return null;
    }

    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        var zeyilTipi = GetStringValue(row, "ZEYIL_TIPI", "Zeyil Tipi");

        if (!string.IsNullOrEmpty(zeyilTipi))
        {
            var upper = zeyilTipi.ToUpperInvariant();
            // İptal içeren zeyil tipleri
            if (upper.Contains("İPTAL") ||
                upper.Contains("IPTAL") ||
                upper.Contains("SATIŞTAN İPTAL") ||
                upper.Contains("SATI�TAN �PTAL") ||
                upper.Contains("BAŞLANGICINDAN İPTAL") ||
                upper.Contains("BA�LANGICINDAN �PTAL") ||
                upper.Contains("KISMI IPTAL"))
            {
                return "İPTAL";
            }
        }

        // Brüt prim negatifse iptal
        var brutPrim = GetTurkishDecimalValue(row, "Brüt Prim", "Brut Prim");
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
            errors.Add("Poliçe Tarih geçersiz");

        // Zeyil kontrolü - zeyillerde 0 veya negatif prim olabilir
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil && (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            errors.Add("Brüt Prim boş veya sıfır");

        return errors;
    }
}
