using IhsanAI.Application.Features.ExcelImport.Dtos;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IhsanAI.Infrastructure.Services.Parsers;

public abstract class BaseExcelParser : IExcelParser
{
    public abstract int SigortaSirketiId { get; }
    public abstract string SirketAdi { get; }
    public abstract string[] FileNamePatterns { get; }

    /// <summary>
    /// Header satır numarası (1-indexed). Null ise otomatik tespit edilir.
    /// Alt sınıflar bunu override edebilir.
    /// </summary>
    public virtual int? HeaderRowIndex => null;

    /// <summary>
    /// Ana sayfa adı (null ise ilk sayfayı kullanır)
    /// </summary>
    public virtual string? MainSheetName => null;

    /// <summary>
    /// Ek olarak okunması gereken sayfa isimleri
    /// </summary>
    public virtual string[]? AdditionalSheetNames => null;

    protected abstract string[] RequiredColumns { get; }

    /// <summary>
    /// Bu parser'ı diğerlerinden ayıran benzersiz kolon isimleri.
    /// İçerik bazlı tespit için kullanılır. Null ise sadece RequiredColumns kullanılır.
    /// </summary>
    protected virtual string[]? SignatureColumns => null;

    public virtual bool CanParse(string fileName, IEnumerable<string> headerColumns)
    {
        var headers = headerColumns.Select(h => NormalizeColumnName(h)).ToList();

        // 1. Dosya adı kontrolü - eşleşirse direkt true
        var fileNameLower = fileName.ToLowerInvariant();
        var fileNameMatch = FileNamePatterns.Any(pattern =>
            fileNameLower.Contains(pattern.ToLowerInvariant()));

        if (fileNameMatch)
        {
            // Dosya adı eşleşti, kolonları da kontrol et
            var requiredNormalized = RequiredColumns.Select(c => NormalizeColumnName(c)).ToList();
            return requiredNormalized.All(req =>
                headers.Any(h => h.Contains(req) || req.Contains(h)));
        }

        // 2. İçerik bazlı tespit - SignatureColumns varsa kullan
        if (SignatureColumns != null && SignatureColumns.Length > 0)
        {
            var signatureNormalized = SignatureColumns.Select(c => NormalizeColumnName(c)).ToList();
            var allSignaturesFound = signatureNormalized.All(sig =>
                headers.Any(h => h.Contains(sig) || sig.Contains(h)));

            if (allSignaturesFound)
            {
                // Signature eşleşti, required kolonları da kontrol et
                var requiredNormalized = RequiredColumns.Select(c => NormalizeColumnName(c)).ToList();
                return requiredNormalized.All(req =>
                    headers.Any(h => h.Contains(req) || req.Contains(h)));
            }
        }

        return false;
    }

    public abstract List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows);

    /// <summary>
    /// Ek sayfa verilerini kullanarak parse eder.
    /// Alt sınıflar bunu override ederek Sigortalilar gibi ek sayfalardan veri alabilir.
    /// </summary>
    public virtual List<ExcelImportRowDto> ParseWithAdditionalSheets(
        IEnumerable<IDictionary<string, object?>> mainRows,
        Dictionary<string, List<IDictionary<string, object?>>> additionalSheets)
    {
        // Default: sadece ana sayfayı parse et
        return Parse(mainRows);
    }

    #region Helper Methods

    protected static string NormalizeColumnName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        // Türkçe karakterleri dönüştür ve küçük harfe çevir
        // Önce büyük Türkçe karakterleri küçük harfe çevir (ToLowerInvariant bazılarını düzgün çevirmiyor)
        return name
            .Replace("İ", "i")  // Büyük İ (U+0130) - ToLowerInvariant düzgün çevirmiyor
            .Replace("I", "i")  // Normal büyük I
            .Replace("Ğ", "g")
            .Replace("Ü", "u")
            .Replace("Ş", "s")
            .Replace("Ö", "o")
            .Replace("Ç", "c")
            .ToLowerInvariant()
            .Replace("ı", "i")  // Küçük ı (U+0131)
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ö", "o")
            .Replace("ç", "c")
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace("/", "")  // Slash'ları da kaldır
            .Trim();
    }

    protected static string? GetStringValue(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        foreach (var col in possibleColumns)
        {
            var key = row.Keys.FirstOrDefault(k =>
                NormalizeColumnName(k).Contains(NormalizeColumnName(col)) ||
                NormalizeColumnName(col).Contains(NormalizeColumnName(k)));

            if (key != null && row.TryGetValue(key, out var value) && value != null)
            {
                var strValue = value.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(strValue))
                    return strValue;
            }
        }
        return null;
    }

    protected static decimal? GetDecimalValue(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        foreach (var col in possibleColumns)
        {
            var key = row.Keys.FirstOrDefault(k =>
                NormalizeColumnName(k).Contains(NormalizeColumnName(col)) ||
                NormalizeColumnName(col).Contains(NormalizeColumnName(k)));

            if (key != null && row.TryGetValue(key, out var value) && value != null)
            {
                // Önce numeric türleri kontrol et - Excel'den direkt sayı gelebilir
                if (value is decimal d)
                    return d;
                if (value is double dbl)
                    return (decimal)dbl;
                if (value is float f)
                    return (decimal)f;
                if (value is int i)
                    return i;
                if (value is long l)
                    return l;

                // String ise parse et
                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrEmpty(strValue)) continue;

                // Para birimi sembollerini temizle
                strValue = strValue
                    .Replace("₺", "")
                    .Replace("TL", "")
                    .Replace("TRY", "")
                    .Replace(" ", "")
                    .Trim();

                // Türkçe format mı İngilizce format mı anla
                // Türkçe: 1.549,22 (binlik nokta, ondalık virgül)
                // İngilizce: 1,549.22 (binlik virgül, ondalık nokta)

                var lastDot = strValue.LastIndexOf('.');
                var lastComma = strValue.LastIndexOf(',');

                if (lastComma > lastDot)
                {
                    // Türkçe format: 1.549,22
                    strValue = strValue.Replace(".", "").Replace(",", ".");
                }
                else if (lastDot > lastComma)
                {
                    // İngilizce format: 1,549.22
                    strValue = strValue.Replace(",", "");
                }
                else if (lastComma >= 0 && lastDot < 0)
                {
                    // Sadece virgül var: 1549,22
                    strValue = strValue.Replace(",", ".");
                }
                // Sadece nokta var veya hiçbiri yok: olduğu gibi bırak

                if (decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                    return result;
            }
        }
        return null;
    }

    protected static DateTime? GetDateValue(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        foreach (var col in possibleColumns)
        {
            var key = row.Keys.FirstOrDefault(k =>
                NormalizeColumnName(k).Contains(NormalizeColumnName(col)) ||
                NormalizeColumnName(col).Contains(NormalizeColumnName(k)));

            if (key != null && row.TryGetValue(key, out var value) && value != null)
            {
                // DateTime olarak gelmiş olabilir (ClosedXML)
                if (value is DateTime dt)
                    return dt;

                // EPPlus tarih değerlerini Double (OLE Automation date) olarak döndürür
                if (value is double d)
                {
                    try
                    {
                        // OLE Automation date: 1 Ocak 1900'den itibaren gün sayısı
                        // Geçerli tarih aralığı kontrolü (1900-2100 arası)
                        if (d > 1 && d < 73050) // 73050 = 2100-01-01
                        {
                            return DateTime.FromOADate(d);
                        }
                    }
                    catch
                    {
                        // Geçersiz OLE date, string olarak dene
                    }
                }

                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(strValue)) continue;

                // Çeşitli formatları dene
                var formats = new[]
                {
                    "dd.MM.yyyy",
                    "dd/MM/yyyy",
                    "yyyy-MM-dd",
                    "dd.MM.yyyy HH:mm:ss",
                    "dd/MM/yyyy HH:mm:ss",
                    "yyyy-MM-dd HH:mm:ss",
                    "d.M.yyyy",
                    "d/M/yyyy",
                    "M/d/yyyy",
                    "M/d/yyyy h:mm:ss tt"
                };

                if (DateTime.TryParseExact(strValue, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                    return parsed;

                // Genel parse
                if (DateTime.TryParse(strValue, new CultureInfo("tr-TR"), DateTimeStyles.None, out parsed))
                    return parsed;
            }
        }
        return null;
    }

    protected static int? GetIntValue(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        var strValue = GetStringValue(row, possibleColumns);
        if (string.IsNullOrEmpty(strValue)) return null;

        if (int.TryParse(strValue, out var result))
            return result;

        return null;
    }

    protected static int GetZeyilNo(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;

        // Trim whitespace
        value = value.Trim();

        // Handle decimal format like "1.0" or "1,0"
        if (value.Contains('.') || value.Contains(','))
        {
            if (decimal.TryParse(value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var decResult))
                return (int)decResult;
        }

        if (int.TryParse(value, out var result))
            return result;
        return 0;
    }

    /// <summary>
    /// Zeyil poliçesi mi kontrol eder (ZeyilNo > 0 ise zeyildir)
    /// </summary>
    protected static bool IsZeyilPolicy(string? zeyilNo)
    {
        if (string.IsNullOrWhiteSpace(zeyilNo)) return false;

        var zeyilValue = GetZeyilNo(zeyilNo);
        return zeyilValue > 0;
    }

    /// <summary>
    /// Ürün adından BransId (PoliceTuruId) çıkarır
    /// sigortapoliceturleri tablosundaki ID'lerle eşleşir
    /// Kullanılmayan ID'ler: 11, 13, 14, 18, 22, 23
    /// </summary>
    protected static int? DetectBransIdFromUrunAdi(string? urunAdi, bool isZeyil = false)
    {
        if (string.IsNullOrWhiteSpace(urunAdi)) return null;

        var value = urunAdi.ToUpperInvariant()
            .Replace("İ", "I")
            .Replace("Ğ", "G")
            .Replace("Ü", "U")
            .Replace("Ş", "S")
            .Replace("Ö", "O")
            .Replace("Ç", "C");

        // Trafik (ID: 0) - zeyil olsa da aynı ID
        if (value.Contains("TRAFIK") || value.Contains("TRAFFIC") || value.Contains("ZMSS") || value.Contains("ZORUNLU MALI"))
            return 0;

        // Kasko (ID: 1) - zeyil olsa da aynı ID
        if (value.Contains("KASKO"))
            return 1;

        // DASK (ID: 2)
        if (value.Contains("DASK") || value.Contains("DEPREM"))
            return 2;

        // Sağlık türleri (spesifikten genele)
        if (value.Contains("SEYAHAT"))
            return 8; // Seyahat Sağlık
        if (value.Contains("YABANCI") && value.Contains("SAGLIK"))
            return 15; // Yabancı Sağlık
        // Ayakta, Yatarak ve Tamamlayıcı -> 16 (Tamamlayıcı Sağlık)
        if (value.Contains("TAMAMLAYICI") || value.Contains("AYAKTA") || value.Contains("YATARAK"))
            return 16;
        if (value.Contains("SAGLIK"))
            return 7; // Genel Sağlık

        // Ferdi Kaza (ID: 3)
        if (value.Contains("FERDI KAZA") || value.Contains("FERDI_KAZA") || value.Contains("FK"))
            return 3;

        // Koltuk (ID: 4)
        if (value.Contains("KOLTUK"))
            return 4;

        // Konut (ID: 5)
        if (value.Contains("KONUT") || value.Contains("MESKEN"))
            return 5;

        // Nakliyat (ID: 6)
        if (value.Contains("NAKLIYAT") || value.Contains("NAKLIYE") || value.Contains("EMTEA"))
            return 6;

        // İşyeri (ID: 9)
        if (value.Contains("ISYERI") || value.Contains("IS YERI") || value.Contains("TIBBI MALP"))
            return 9;

        // ZKTM (ID: 10)
        if (value.Contains("ZKTM") || value.Contains("KARAYOLU TASIMACI"))
            return 10;

        // IMM (ID: 12)
        if (value.Contains("IMM") || value.Contains("IHTIYARI MALI"))
            return 12;

        // Makbuz (ID: 17)
        if (value.Contains("MAKBUZ"))
            return 17;

        // Doğal Koruma (ID: 19)
        if (value.Contains("DOGAL KORUMA") || value.Contains("DOGAL AFET"))
            return 19;

        // Tarım (ID: 20)
        if (value.Contains("TARIM") || value.Contains("ZIRAI") || value.Contains("HAYVAN"))
            return 20;

        // Yangın (ID: 21)
        if (value.Contains("YANGIN"))
            return 21;

        // Hukuksal Koruma (ID: 24)
        if (value.Contains("HUKUKSAL") || value.Contains("HUKUKI"))
            return 24;

        // Tekne (ID: 25)
        if (value.Contains("TEKNE") || value.Contains("YATA") || value.Contains("DENIZ"))
            return 25;

        // Hayat (ID: 26)
        if (value.Contains("HAYAT") || value.Contains("YASAM"))
            return 26;

        // Yeşil Kart (ID: 27)
        if (value.Contains("YESIL KART") || value.Contains("GREEN CARD"))
            return 27;

        // Mühendislik (ID: 28)
        if (value.Contains("MUHENDISLIK") || value.Contains("INSAAT") || value.Contains("MAKINE KIRILMA"))
            return 28;

        // Sorumluluk (ID: 29)
        if (value.Contains("SORUMLULUK") || value.Contains("MESLEK") || value.Contains("URUN SORUMLUL"))
            return 29;

        // Yol Destek (ID: 30)
        if (value.Contains("YOL DESTEK") || value.Contains("YARDIM"))
            return 30;

        // Belli Değil (ID: 255)
        return 255;
    }

    protected virtual List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue)
            errors.Add("Başlangıç Tarihi geçersiz");

        if (!row.BitisTarihi.HasValue)
            errors.Add("Bitiş Tarihi geçersiz");

        // Zeyil poliçelerde negatif veya sıfır prim olabilir
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!row.BrutPrim.HasValue)
            errors.Add("Brüt Prim geçersiz");
        else if (!isZeyil && row.BrutPrim <= 0)
            errors.Add("Brüt Prim geçersiz (yeni poliçe için prim pozitif olmalıdır)");

        // TC/VKN doğrulama kaldırıldı - artık DTO'da bu alan yok

        return errors;
    }

    protected string DetectPoliceTipi(IDictionary<string, object?> row)
    {
        // Çeşitli kolon isimlerinde iptal/tahakkuk bilgisi ara
        var possibleColumns = new[] { "PoliceTipi", "Tipi", "İşlem", "Islem", "Durum", "HareketTipi" };
        var value = GetStringValue(row, possibleColumns)?.ToUpperInvariant();

        if (string.IsNullOrEmpty(value)) return "TAHAKKUK";

        if (value.Contains("İPTAL") || value.Contains("IPTAL") || value.Contains("CANCEL"))
            return "İPTAL";

        return "TAHAKKUK";
    }

    protected string? DetectUrunAdi(IDictionary<string, object?> row)
    {
        var possibleColumns = new[] { "UrunAdi", "Urun", "Brans", "BransAdi", "Branş", "PoliceTuru", "Turu" };
        var value = GetStringValue(row, possibleColumns)?.ToUpperInvariant();

        if (string.IsNullOrEmpty(value)) return null;

        // Normalize common product names
        if (value.Contains("TRAFİK") || value.Contains("TRAFIK"))
            return "TRAFİK";
        if (value.Contains("KASKO"))
            return "KASKO";
        if (value.Contains("DASK") || value.Contains("ZORUNLU DEPREM"))
            return "DASK";
        if (value.Contains("KONUT"))
            return "KONUT";
        if (value.Contains("SAĞLIK") || value.Contains("SAGLIK"))
            return "SAĞLIK";
        if (value.Contains("FERDI KAZA") || value.Contains("FERDİ KAZA"))
            return "FERDİ KAZA";
        if (value.Contains("HAYAT"))
            return "HAYAT";
        if (value.Contains("İŞYERİ") || value.Contains("ISYERI"))
            return "İŞYERİ";
        if (value.Contains("NAKLİYAT") || value.Contains("NAKLIYAT"))
            return "NAKLİYAT";

        return value;
    }

    #endregion
}
