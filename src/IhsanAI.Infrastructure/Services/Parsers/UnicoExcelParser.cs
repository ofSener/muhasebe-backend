using System.Globalization;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Unico (Aviva) Sigorta Excel parser
/// Tarih formatı: "12/04/2025 00:00:00" (MM/DD/YYYY)
///
/// MAPPING:
/// *PoliceNo       = "Poliçe No"             [OK]
/// *YenilemeNo     = "Yenileme No"           [OK]
/// *ZeyilNo        = "Zeyl No"               [OK]
/// *ZeyilTipKodu   = YOK                     [NO]
/// *Brans          = "Tarife Adı"            [OK]
/// *PoliceTipi     = YOK                     [NO]
/// *TanzimTarihi   = "Tanzim Tarihi"         [OK]
/// *BaslangicTarihi= "Başlama Tarihi"        [OK]
/// *BitisTarihi    = "Bitiş Tarihi"          [OK]
/// *ZeyilOnayTarihi= YOK                     [NO]
/// *ZeyilBaslangicTarihi = YOK               [NO]
/// *BrutPrim       = "Brüt Prim"             [OK]
/// *NetPrim        = "Net Prim"              [OK]
/// *Komisyon       = "Komisyon Tutarı"       [OK]
/// *SigortaliAdi   = "Sigortalı Adı"         [OK]
/// *SigortaliSoyadi= "Sigortalı Soyadı"      [OK]
/// *Plaka          = YOK                     [NO]
/// *AcenteNo       = "Acente No"             [OK]
/// </summary>
public class UnicoExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 2;
    public override string SirketAdi => "Unico Sigorta";
    public override string[] FileNamePatterns => new[] { "unico", "aviva", "unc" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Prim", "Tarih"
    };

    // Unico'ya özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "Tarife Adı", "Sigortalı Adı", "Sigortalı Soyadı"  // Bu kombinasyon sadece Unico'da var
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "Poliçe No");

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No"),
                ZeyilNo = GetStringValue(row, "Zeyl No"),
                ZeyilTipKodu = null,  // Unico'da yok
                Brans = GetStringValue(row, "Tarife Adı"),
                PoliceTipi = GetPoliceTipiFromPrim(row),

                // Tarihler - Unico'da özel format
                TanzimTarihi = GetUnicoDateValue(row, "Tanzim Tarihi"),
                BaslangicTarihi = GetUnicoDateValue(row, "Başlama Tarihi"),
                BitisTarihi = GetUnicoDateValue(row, "Bitiş Tarihi"),
                ZeyilOnayTarihi = null,  // Unico'da yok
                ZeyilBaslangicTarihi = null,  // Unico'da yok

                // Primler
                BrutPrim = GetDecimalValue(row, "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "Net Prim"),
                Komisyon = GetUnicoDecimalValue(row, "Komisyon Tutarı"),

                // Müşteri Bilgileri - Unico'da Ad ve Soyad AYRI
                SigortaliAdi = GetStringValue(row, "Sigortalı Adı")?.Trim(),
                SigortaliSoyadi = GetStringValue(row, "Sigortalı Soyadı")?.Trim(),

                // Araç Bilgileri
                Plaka = null,  // Unico'da yok

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "Acente No")
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
    /// Unico komisyon değerini parse eder
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

                strValue = strValue.Replace(",", ".");

                if (decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                    return result;
            }
        }

        return null;
    }

    private string GetPoliceTipiFromPrim(IDictionary<string, object?> row)
    {
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

        // Zeyil kontrolü - robust parsing ile (zeyillerde 0 veya negatif prim olabilir)
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil && (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            errors.Add("Brüt Prim boş veya sıfır");
        // Zeyil için prim 0 veya negatif olabilir

        return errors;
    }
}
