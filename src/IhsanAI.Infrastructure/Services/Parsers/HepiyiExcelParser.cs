using System.Globalization;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Hepiyi Sigorta Excel parser
/// Primler Türkçe format ("-7.141,35")
///
/// MAPPING:
/// *PoliceNo       = "Poliçe No"                [OK]
/// *YenilemeNo     = "YENILEME_NO"              [OK]
/// *ZeyilNo        = "Zeyl No"                  [OK]
/// *ZeyilTipKodu   = "ZEYL_TIP_KODU"            [OK]
/// *Brans          = "URUN_ADI"                 [OK]
/// *PoliceTipi     = "ZEYIL_TIPI"               [OK]
/// *TanzimTarihi   = "Tanzim Tarih"             [OK]
/// *BaslangicTarihi= "Poliçe Tarih"             [OK]
/// *BitisTarihi    = "Poliçe Bitiş Tarih"       [OK]
/// *ZeyilOnayTarihi= YOK                        [NO]
/// *ZeyilBaslangicTarihi = YOK                  [NO]
/// *BrutPrim       = "Brüt Prim"                [OK]
/// *NetPrim        = "Net Prim"                 [OK]
/// *Komisyon       = "Komisyon"                 [OK]
/// *SigortaliAdi   = "Müşteri Ad – Soyad"       [OK]
/// *SigortaliSoyadi= YOK (birleşik)             [NO]
/// *Plaka          = "Plaka"                    [OK]
/// *AcenteNo       = "PARTAJ"                   [WARN]
/// </summary>
public class HepiyiExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 4;
    public override string SirketAdi => "Hepiyi Sigorta";
    public override string[] FileNamePatterns => new[] { "hepiyi", "hepıyı", "hepi̇yi̇" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Prim", "Tarih"
    };

    // Hepiyi'ye özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "Müşteri Ad", "Poliçe Tarih", "Police Tür Kod"  // Bu kombinasyon sadece Hepiyi'de var
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

            // Sigortalı adı - önce "Müşteri Ad – Soyad", yoksa "MUSTERI_UNVANI"
            var sigortaliAdi = GetStringValue(row, "Müşteri Ad – Soyad", "Müşteri Ad - Soyad");
            if (string.IsNullOrEmpty(sigortaliAdi))
                sigortaliAdi = GetStringValue(row, "MUSTERI_UNVANI");

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "YENILEME_NO"),
                ZeyilNo = GetStringValue(row, "Zeyl No"),
                ZeyilTipKodu = GetStringValue(row, "ZEYL_TIP_KODU"),
                Brans = GetStringValue(row, "URUN_ADI"),
                PoliceTipi = GetPoliceTipi(row),

                // Tarihler
                TanzimTarihi = GetDateValue(row, "Tanzim Tarih"),
                BaslangicTarihi = GetDateValue(row, "Poliçe Tarih"),
                BitisTarihi = GetDateValue(row, "Poliçe Bitiş Tarih"),
                ZeyilOnayTarihi = null,  // Hepiyi'de yok
                ZeyilBaslangicTarihi = null,  // Hepiyi'de yok

                // Primler - Türkçe format
                BrutPrim = GetTurkishDecimalValue(row, "Brüt Prim"),
                NetPrim = GetTurkishDecimalValue(row, "Net Prim"),
                Komisyon = GetTurkishDecimalValue(row, "Komisyon"),

                // Müşteri Bilgileri
                SigortaliAdi = sigortaliAdi?.Trim(),
                SigortaliSoyadi = null,  // Hepiyi'de birleşik

                // Araç Bilgileri
                Plaka = GetStringValue(row, "Plaka"),

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "PARTAJ")  // Partaj kodu
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
    /// Türkçe formatındaki decimal değeri parse eder ("-7.141,35" gibi)
    /// </summary>
    private decimal? GetTurkishDecimalValue(IDictionary<string, object?> row, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            if (row.TryGetValue(key, out var value) && value != null)
            {
                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrEmpty(strValue))
                    continue;

                // Önce standart decimal dene
                if (value is decimal d)
                    return d;
                if (value is double dbl)
                    return (decimal)dbl;
                if (value is int i)
                    return i;

                // Türkçe format dene ("-7.141,35" -> -7141.35)
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

        if (!string.IsNullOrEmpty(zeyilTipi) &&
            (zeyilTipi.ToUpperInvariant().Contains("İPTAL") ||
             zeyilTipi.ToUpperInvariant().Contains("IPTAL")))
        {
            return "İPTAL";
        }

        // Brüt prim negatifse iptal
        var brutPrim = GetTurkishDecimalValue(row, "Brüt Prim");
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

        // Zeyil kontrolü - robust parsing ile (zeyillerde 0 veya negatif prim olabilir)
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil && (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            errors.Add("Brüt Prim boş veya sıfır");
        // Zeyil için prim 0 veya negatif olabilir

        return errors;
    }
}
