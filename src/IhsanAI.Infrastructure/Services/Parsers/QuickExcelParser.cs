using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Quick Sigorta Excel parser
/// Sheet: PoliceListesi
/// Kolonlar: PoliceNo, YenilemeNo, ZeyilNo, ZeyilAd, UrunAd, BaslamaTarihi, BitisTarihi,
/// TanzimTarihi, NetPrimTL, BrutPrimTL, AcenteKomisyonTL, AcenteAd
/// NOT: Quick formatında TC/VKN, Plaka, Sigortalı Adı YOKTUR!
/// </summary>
public class QuickExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 3; // Quick Sigorta ID'si
    public override string SirketAdi => "Quick Sigorta";
    public override string[] FileNamePatterns => new[] { "quick", "quıck", "qck" };

    protected override string[] RequiredColumns => new[]
    {
        "PoliceNo", "BrutPrim", "BaslamaTarihi"
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // Poliçe No'yu al - Quick formatında "PoliceNo"
            var policeNo = GetStringValue(row, "PoliceNo");

            // Boş satırları atla
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "YenilemeNo"),
                ZeyilNo = GetStringValue(row, "ZeyilNo"),

                // Tarihler
                TanzimTarihi = GetDateValue(row, "TanzimTarihi"),
                BaslangicTarihi = GetDateValue(row, "BaslamaTarihi"),
                BitisTarihi = GetDateValue(row, "BitisTarihi"),

                // Prim ve komisyon - Quick'te TL versiyonu var
                BrutPrim = GetDecimalValue(row, "BrutPrimTL", "BrutPrim"),
                NetPrim = GetDecimalValue(row, "NetPrimTL", "NetPrim"),
                Komisyon = GetDecimalValue(row, "AcenteKomisyonTL", "AcenteKomisyon"),
                Vergi = null, // Quick'te vergi yok

                // Quick'te Sigortalı, TC/VKN ve Plaka YOKTUR
                SigortaliAdi = null,
                TcVkn = null,
                Plaka = null,

                // Poliçe tipi - ZeyilAd kolonundan
                PoliceTipi = GetPoliceTipi(row),

                // Ürün adı
                UrunAdi = GetStringValue(row, "UrunAd"),

                // Acente bilgisi
                AcenteAdi = GetStringValue(row, "AcenteAd")?.Trim(),
                Sube = null,
                PoliceKesenPersonel = GetStringValue(row, "Kullanici")
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

    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        var zeyilAd = GetStringValue(row, "ZeyilAd");

        if (string.IsNullOrEmpty(zeyilAd))
        {
            var brutPrim = GetDecimalValue(row, "BrutPrimTL", "BrutPrim");
            return brutPrim < 0 ? "İPTAL" : "TAHAKKUK";
        }

        return zeyilAd.ToUpperInvariant().Contains("İPTAL") ||
               zeyilAd.ToUpperInvariant().Contains("IPTAL")
            ? "İPTAL"
            : "TAHAKKUK";
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

        // Quick'te TC/VKN, Plaka, Sigortalı zorunlu değil

        return errors;
    }
}
