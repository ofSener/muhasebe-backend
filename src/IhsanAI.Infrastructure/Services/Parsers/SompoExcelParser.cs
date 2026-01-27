using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Sompo Sigorta Excel parser
/// Header 3. satırda (EPPlus 1-indexed)
///
/// MAPPING:
/// - PoliceNo       <- "Poliçe No"        ✅
/// - YenilemeNo     <- "Yenileme No"      ✅
/// - ZeyilNo        <- "Zeyl No"          ✅
/// - ZeyilTipKodu   <- YOK                ❌
/// - Brans          <- "Ürün No" (kod)    ⚠️
/// - PoliceTipi     <- YOK                ❌
/// - TanzimTarihi   <- "Onay Tarihi"      ✅
/// - BaslangicTarihi<- "Onay Tarihi"      ⚠️ (aynı)
/// - BitisTarihi    <- YOK                ❌
/// - ZeyilOnayTarihi<- YOK                ❌
/// - ZeyilBaslangicTarihi <- YOK          ❌
/// - BrutPrim       <- "Brüt Prim"        ✅
/// - NetPrim        <- "Net Prim"         ✅
/// - Komisyon       <- "Komisyon"         ✅
/// - SigortaliAdi   <- "Sigortalı Ünvanı" ✅
/// - SigortaliSoyadi<- YOK                ❌
/// - Plaka          <- YOK                ❌
/// - AcenteNo       <- YOK                ❌
/// </summary>
public class SompoExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 6;
    public override string SirketAdi => "Sompo Sigorta";
    public override string[] FileNamePatterns => new[] { "sompo", "smp" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim", "Onay Tarihi"
    };

    public override bool CanParse(string fileName, IEnumerable<string> headerColumns)
    {
        var fileNameLower = fileName.ToLowerInvariant();
        return FileNamePatterns.Any(pattern =>
            fileNameLower.Contains(pattern.ToLowerInvariant()));
    }

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "Poliçe No");

            // Boş veya header satırlarını atla
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            if (policeNo.ToUpperInvariant().Contains("POLİÇE") ||
                policeNo.ToUpperInvariant().Contains("POLICE"))
                continue;

            var onayTarihi = GetDateValue(row, "Onay Tarihi");

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No"),
                ZeyilNo = GetStringValue(row, "Zeyl No"),
                ZeyilTipKodu = null,  // SOMPO'da yok
                Brans = GetBransFromUrunNo(row),
                PoliceTipi = GetPoliceTipiFromPrim(row),

                // Tarihler
                TanzimTarihi = onayTarihi,
                BaslangicTarihi = onayTarihi,
                BitisTarihi = null,  // SOMPO'da yok
                ZeyilOnayTarihi = null,  // SOMPO'da yok
                ZeyilBaslangicTarihi = null,  // SOMPO'da yok

                // Primler
                BrutPrim = GetDecimalValue(row, "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "Net Prim"),
                Komisyon = GetDecimalValue(row, "Komisyon"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "Sigortalı Ünvanı")?.Trim(),
                SigortaliSoyadi = null,  // SOMPO'da yok

                // Araç Bilgileri
                Plaka = null,  // SOMPO'da yok

                // Acente Bilgileri
                AcenteNo = null  // SOMPO'da yok
            };

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

    private string? GetBransFromUrunNo(IDictionary<string, object?> row)
    {
        var urunNo = GetStringValue(row, "Ürün No");

        return urunNo switch
        {
            "117" => "DASK",
            "115" => "KASKO",
            "101" => "TRAFİK",
            "118" => "KONUT",
            "119" => "İŞYERİ",
            _ => urunNo  // Kod olarak döndür
        };
    }

    private string GetPoliceTipiFromPrim(IDictionary<string, object?> row)
    {
        var brutPrim = GetDecimalValue(row, "Brüt Prim");
        return brutPrim < 0 ? "İPTAL" : "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.TanzimTarihi.HasValue)
            errors.Add("Onay Tarihi geçersiz");

        if (!row.BrutPrim.HasValue || row.BrutPrim == 0)
            errors.Add("Brüt Prim boş veya sıfır");

        return errors;
    }
}
