using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Quick Sigorta Excel parser
/// Sheet: PoliceListesi
///
/// MAPPING:
/// - PoliceNo       <- "PoliceNo"             ✅
/// - YenilemeNo     <- "YenilemeNo"           ✅
/// - ZeyilNo        <- "ZeyilNo"              ✅
/// - ZeyilTipKodu   <- "ZeyilTipKodu"         ✅
/// - Brans          <- "UrunAd"               ✅
/// - PoliceTipi     <- YOK                    ❌
/// - TanzimTarihi   <- "TanzimTarihi"         ✅
/// - BaslangicTarihi<- "BaslamaTarihi"        ✅
/// - BitisTarihi    <- "BitisTarihi"          ✅
/// - ZeyilOnayTarihi<- YOK                    ❌
/// - ZeyilBaslangicTarihi <- YOK              ❌
/// - BrutPrim       <- "BrutPrimTL"           ✅
/// - NetPrim        <- "NetPrimTL"            ✅
/// - Komisyon       <- "AcenteKomisyonTL"     ✅
/// - SigortaliAdi   <- YOK                    ❌
/// - SigortaliSoyadi<- YOK                    ❌
/// - Plaka          <- YOK                    ❌
/// - AcenteNo       <- "AcenteNo"             ✅
/// </summary>
public class QuickExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 3;
    public override string SirketAdi => "Quick Sigorta";
    public override string[] FileNamePatterns => new[] { "quick", "quıck", "qck" };

    protected override string[] RequiredColumns => new[]
    {
        "PoliceNo", "BrutPrimTL", "BaslamaTarihi"
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "PoliceNo");

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "YenilemeNo"),
                ZeyilNo = GetStringValue(row, "ZeyilNo"),
                ZeyilTipKodu = GetStringValue(row, "ZeyilTipKodu"),
                Brans = GetStringValue(row, "UrunAd"),
                PoliceTipi = GetPoliceTipiFromPrim(row),

                // Tarihler
                TanzimTarihi = GetDateValue(row, "TanzimTarihi"),
                BaslangicTarihi = GetDateValue(row, "BaslamaTarihi"),
                BitisTarihi = GetDateValue(row, "BitisTarihi"),
                ZeyilOnayTarihi = null,  // Quick'te yok
                ZeyilBaslangicTarihi = null,  // Quick'te yok

                // Primler
                BrutPrim = GetDecimalValue(row, "BrutPrimTL", "BrutPrim"),
                NetPrim = GetDecimalValue(row, "NetPrimTL", "NetPrim"),
                Komisyon = GetDecimalValue(row, "AcenteKomisyonTL", "AcenteKomisyon"),

                // Müşteri Bilgileri
                SigortaliAdi = null,  // Quick'te yok
                SigortaliSoyadi = null,  // Quick'te yok

                // Araç Bilgileri
                Plaka = null,  // Quick'te yok

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "AcenteNo")
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

    private string GetPoliceTipiFromPrim(IDictionary<string, object?> row)
    {
        var zeyilAd = GetStringValue(row, "ZeyilAd");

        if (!string.IsNullOrEmpty(zeyilAd) &&
            (zeyilAd.ToUpperInvariant().Contains("İPTAL") ||
             zeyilAd.ToUpperInvariant().Contains("IPTAL")))
        {
            return "İPTAL";
        }

        var brutPrim = GetDecimalValue(row, "BrutPrimTL", "BrutPrim");
        return brutPrim < 0 ? "İPTAL" : "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue)
            errors.Add("Başlangıç Tarihi geçersiz");

        if (!row.BrutPrim.HasValue || row.BrutPrim == 0)
            errors.Add("Brüt Prim boş veya sıfır");

        return errors;
    }
}
