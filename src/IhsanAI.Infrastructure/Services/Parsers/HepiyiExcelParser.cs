using System.Globalization;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Hepiyi Sigorta Excel parser
/// Kolonlar: Poliçe No, Zeyl No, Poliçe Tarih, Poliçe Bitiş Tarih, Tanzim Tarih,
/// TC, Müşteri Ad – Soyad, Plaka, Net Prim, Brüt Prim, Komisyon, YENILEME_NO,
/// URUN_ADI, ZEYIL_TIPI, VERGI_KIMLIK_NUMARASI, P Kaynak Adı
/// NOT: Hepiyi formatında primler Türkçe format ("-7.141,35")
/// </summary>
public class HepiyiExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 4; // Hepiyi Sigorta ID'si
    public override string SirketAdi => "Hepiyi Sigorta";
    public override string[] FileNamePatterns => new[] { "hepiyi", "hepıyı", "hepi̇yi̇" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim", "Poliçe Tarih"
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

            // TC veya VKN al - Hepiyi'de iki farklı kolon var
            var tc = GetStringValue(row, "TC");
            var vkn = GetStringValue(row, "VERGI_KIMLIK_NUMARASI");
            var tcVkn = !string.IsNullOrEmpty(tc) ? tc : vkn;

            // Sigortalı adı
            var musteriAd = GetStringValue(row, "Müşteri Ad – Soyad", "Müşteri Ad - Soyad");
            var musteriUnvani = GetStringValue(row, "MUSTERI_UNVANI");
            var sigortaliAdi = !string.IsNullOrEmpty(musteriAd) ? musteriAd : musteriUnvani;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "YENILEME_NO"),
                ZeyilNo = GetStringValue(row, "Zeyl No"),

                // Tarihler - Hepiyi'de farklı format olabilir (DD/MM/YYYY)
                TanzimTarihi = GetDateValue(row, "Tanzim Tarih"),
                BaslangicTarihi = GetDateValue(row, "Poliçe Tarih"),
                BitisTarihi = GetDateValue(row, "Poliçe Bitiş Tarih"),

                // Prim ve komisyon - Hepiyi'de Türkçe format ("-7.141,35")
                BrutPrim = GetTurkishDecimalValue(row, "Brüt Prim"),
                NetPrim = GetTurkishDecimalValue(row, "Net Prim"),
                Komisyon = GetTurkishDecimalValue(row, "Komisyon"),
                Vergi = GetTurkishDecimalValue(row, "GDV"),

                // Sigortalı bilgileri
                SigortaliAdi = sigortaliAdi?.Trim(),
                TcVkn = tcVkn,
                Plaka = GetStringValue(row, "Plaka"),

                // Poliçe tipi
                PoliceTipi = GetPoliceTipi(row),

                // Ürün adı
                UrunAdi = GetStringValue(row, "URUN_ADI"),

                // Acente bilgisi
                AcenteAdi = GetStringValue(row, "P Kaynak Adı", "PARTAJ"),
                Sube = null,
                PoliceKesenPersonel = GetStringValue(row, "SYS Kullanici Adi")
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

        if (!row.BrutPrim.HasValue || row.BrutPrim == 0)
            errors.Add("Brüt Prim boş veya sıfır");

        // TC/VKN varsa format kontrolü - ama zorunlu değil

        return errors;
    }
}
