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

    protected abstract string[] RequiredColumns { get; }

    public virtual bool CanParse(string fileName, IEnumerable<string> headerColumns)
    {
        // Dosya adı kontrolü
        var fileNameLower = fileName.ToLowerInvariant();
        var fileNameMatch = FileNamePatterns.Any(pattern =>
            fileNameLower.Contains(pattern.ToLowerInvariant()));

        if (!fileNameMatch) return false;

        // Header kolonları kontrolü
        var headers = headerColumns.Select(h => NormalizeColumnName(h)).ToList();
        var requiredNormalized = RequiredColumns.Select(c => NormalizeColumnName(c)).ToList();

        return requiredNormalized.All(req =>
            headers.Any(h => h.Contains(req) || req.Contains(h)));
    }

    public abstract List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows);

    #region Helper Methods

    protected static string NormalizeColumnName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        // Türkçe karakterleri dönüştür ve küçük harfe çevir
        return name
            .ToLowerInvariant()
            .Replace("ı", "i")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ö", "o")
            .Replace("ç", "c")
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
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
        var strValue = GetStringValue(row, possibleColumns);
        if (string.IsNullOrEmpty(strValue)) return null;

        // Para birimi sembollerini ve formatları temizle
        strValue = strValue
            .Replace("₺", "")
            .Replace("TL", "")
            .Replace("TRY", "")
            .Replace(" ", "")
            .Replace(".", "")  // Binlik ayırıcı
            .Replace(",", ".")  // Ondalık ayırıcı
            .Trim();

        if (decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

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
                // DateTime olarak gelmiş olabilir
                if (value is DateTime dt)
                    return dt;

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
                    "d/M/yyyy"
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

    protected static sbyte GetZeyilNo(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        if (sbyte.TryParse(value, out var result))
            return result;
        return 0;
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

        if (!row.BrutPrim.HasValue || row.BrutPrim <= 0)
            errors.Add("Brüt Prim geçersiz");

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
