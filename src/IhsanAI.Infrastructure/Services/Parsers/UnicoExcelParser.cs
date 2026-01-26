using System.Globalization;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Unico (Aviva) Sigorta Excel parser
/// Kolonlar: Poliçe No, Zeyl No, Yenileme No, Tanzim Tarihi, Başlama Tarihi,
/// Bitiş Tarihi, Sigortalı Adı, Sigortalı Soyadı, Net Prim, Brüt Prim,
/// Komisyon Tutarı, Tarife Adı, Acente Adı
/// NOT: Unico formatında TC/VKN ve Plaka YOKTUR!
/// Tarih formatı: "12/04/2025 00:00:00" (MM/DD/YYYY)
/// </summary>
public class UnicoExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 2; // Unico/Aviva Sigorta ID'si
    public override string SirketAdi => "Unico Sigorta";
    public override string[] FileNamePatterns => new[] { "unico", "aviva", "unc" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim", "Başlama Tarihi"
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // Poliçe No'yu al
            var policeNo = GetStringValue(row, "Poliçe No");

            // Boş satırları atla
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Sigortalı adı - Unico'da Ad ve Soyad ayrı
            var sigortaliAdi = GetStringValue(row, "Sigortalı Adı");
            var sigortaliSoyadi = GetStringValue(row, "Sigortalı Soyadı");
            var fullName = $"{sigortaliAdi} {sigortaliSoyadi}".Trim();

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No"),
                ZeyilNo = GetStringValue(row, "Zeyl No"),

                // Tarihler - Unico'da özel format
                TanzimTarihi = GetUnicoDateValue(row, "Tanzim Tarihi"),
                BaslangicTarihi = GetUnicoDateValue(row, "Başlama Tarihi"),
                BitisTarihi = GetUnicoDateValue(row, "Bitiş Tarihi"),

                // Prim ve komisyon
                BrutPrim = GetDecimalValue(row, "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "Net Prim"),
                Komisyon = GetUnicoDecimalValue(row, "Komisyon Tutarı"),
                Vergi = GetDecimalValue(row, "GV"),

                // Sigortalı bilgileri
                SigortaliAdi = fullName,
                TcVkn = null, // Unico'da TC/VKN yok
                Plaka = null, // Unico'da plaka yok

                // Poliçe tipi
                PoliceTipi = GetPoliceTipi(row),

                // Ürün adı - Tarife Adı kolonundan
                UrunAdi = GetStringValue(row, "Tarife Adı"),

                // Acente bilgisi
                AcenteAdi = GetStringValue(row, "Acente Adı"),
                Sube = GetStringValue(row, "Bölge Adı"),
                PoliceKesenPersonel = GetStringValue(row, "Onaylayan Kullanıcı")
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

    /// <summary>
    /// Unico tarih formatını parse eder ("12/04/2025 00:00:00" - MM/DD/YYYY formatı)
    /// </summary>
    private DateTime? GetUnicoDateValue(IDictionary<string, object?> row, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            if (row.TryGetValue(key, out var value) && value != null)
            {
                if (value is DateTime dt)
                    return dt;

                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrEmpty(strValue))
                    continue;

                // Unico formatı: "12/04/2025 00:00:00" (MM/DD/YYYY HH:mm:ss)
                // veya "12/04/2025" (MM/DD/YYYY)
                var formats = new[]
                {
                    "MM/dd/yyyy HH:mm:ss",
                    "MM/dd/yyyy",
                    "dd/MM/yyyy HH:mm:ss",
                    "dd/MM/yyyy",
                    "yyyy-MM-dd'T'HH:mm:ss",
                    "dd.MM.yyyy"
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(strValue, format, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var result))
                        return result;
                }

                // Genel parse
                if (DateTime.TryParse(strValue, out var generalResult))
                    return generalResult;
            }
        }

        return null;
    }

    /// <summary>
    /// Unico komisyon değerini parse eder (string olarak gelebilir)
    /// </summary>
    private decimal? GetUnicoDecimalValue(IDictionary<string, object?> row, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            if (row.TryGetValue(key, out var value) && value != null)
            {
                if (value is decimal d)
                    return d;
                if (value is double dbl)
                    return (decimal)dbl;
                if (value is int i)
                    return i;

                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrEmpty(strValue))
                    continue;

                // Türkçe veya İngilizce format
                strValue = strValue.Replace(",", ".");

                if (decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                    return result;
            }
        }

        return null;
    }

    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        // Brüt prim negatifse iptal
        var brutPrim = GetDecimalValue(row, "Brüt Prim");
        if (brutPrim < 0)
            return "İPTAL";

        // Zeyil No > 0 ve prim 0 ise iptal olabilir
        var zeyilNo = GetStringValue(row, "Zeyl No");
        if (zeyilNo != "0" && brutPrim == 0)
            return "İPTAL";

        return "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue)
            errors.Add("Başlama Tarihi geçersiz");

        if (!row.BrutPrim.HasValue || row.BrutPrim == 0)
            errors.Add("Brüt Prim boş veya sıfır");

        // Unico'da TC/VKN ve Plaka zorunlu değil

        return errors;
    }
}
