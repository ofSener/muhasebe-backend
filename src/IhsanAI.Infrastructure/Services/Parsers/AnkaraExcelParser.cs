using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Ankara Sigorta Excel parser
///
/// MAPPING:
/// - PoliceNo       <- "Poliçe No"                   ✅
/// - YenilemeNo     <- "Yenileme No"                 ✅
/// - ZeyilNo        <- "Zeyil No"                    ✅
/// - ZeyilTipKodu   <- "Zeyil Türü"                  ✅
/// - Brans          <- "Branş"                       ✅
/// - PoliceTipi     <- "Tahakkuk / İptal"            ✅
/// - TanzimTarihi   <- "Poliçe Onay Tarihi"          ✅
/// - BaslangicTarihi<- "Poliçe Başlangıç Tarihi"     ✅
/// - BitisTarihi    <- "Poliçe Bitiş Tarihi"         ✅
/// - ZeyilOnayTarihi<- "Zeyil Onay Tarihi"           ✅
/// - ZeyilBaslangicTarihi <- "Zeyil Başlangıç Tarihi"✅
/// - BrutPrim       <- "Brüt Prim ₺"                 ✅
/// - NetPrim        <- "Net Prim ₺"                  ✅
/// - Komisyon       <- "Komisyon ₺"                  ✅
/// - SigortaliAdi   <- "Sigortalı Adı / Ünvanı"      ✅
/// - SigortaliSoyadi<- YOK (birleşik)                ❌
/// - Plaka          <- "Plaka"                       ✅
/// - AcenteNo       <- "Partaj"                      ⚠️
/// </summary>
public class AnkaraExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 1;
    public override string SirketAdi => "Ankara Sigorta";
    public override string[] FileNamePatterns => new[] { "ankara", "ank" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim", "Poliçe Başlangıç"
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
                ZeyilNo = GetStringValue(row, "Zeyil No"),
                ZeyilTipKodu = GetStringValue(row, "Zeyil Türü"),
                Brans = GetStringValue(row, "Branş"),
                PoliceTipi = GetPoliceTipi(row),

                // Tarihler
                TanzimTarihi = GetDateValue(row, "Poliçe Onay Tarihi"),
                BaslangicTarihi = GetDateValue(row, "Poliçe Başlangıç Tarihi"),
                BitisTarihi = GetDateValue(row, "Poliçe Bitiş Tarihi"),
                ZeyilOnayTarihi = GetDateValue(row, "Zeyil Onay Tarihi"),
                ZeyilBaslangicTarihi = GetDateValue(row, "Zeyil Başlangıç Tarihi"),

                // Primler
                BrutPrim = GetDecimalValue(row, "Brüt Prim ₺", "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "Net Prim ₺", "Net Prim"),
                Komisyon = GetDecimalValue(row, "Komisyon ₺", "Komisyon"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "Sigortalı Adı / Ünvanı")?.Trim(),
                SigortaliSoyadi = null,  // Ankara'da birleşik

                // Araç Bilgileri
                Plaka = GetStringValue(row, "Plaka"),

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "Partaj")  // Partaj kodu
            };

            // Tanzim tarihi yoksa poliçe onay tarihini kullan
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

    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        var tahakkukIptal = GetStringValue(row, "Tahakkuk / İptal", "Tahakkuk/İptal");

        if (string.IsNullOrEmpty(tahakkukIptal))
        {
            var brutPrim = GetDecimalValue(row, "Brüt Prim ₺", "Brüt Prim");
            return brutPrim < 0 ? "İPTAL" : "TAHAKKUK";
        }

        return tahakkukIptal.ToUpperInvariant().Contains("İPTAL") ||
               tahakkukIptal.ToUpperInvariant().Contains("IPTAL")
            ? "İPTAL"
            : "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue)
            errors.Add("Poliçe Başlangıç Tarihi geçersiz");

        if (!row.BrutPrim.HasValue || row.BrutPrim == 0)
            errors.Add("Brüt Prim boş veya sıfır");

        return errors;
    }
}
