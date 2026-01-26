using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Quick Sigorta Excel parser
/// Kolon mapping: PoliceNo, BrutPrimTL, BaslamaTarihi
/// Not: Ana veri "PoliceListesi" sheet'inde
/// </summary>
public class QuickExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 2; // Quick Sigorta ID'si
    public override string SirketAdi => "Quick Sigorta";
    public override string[] FileNamePatterns => new[] { "quick", "qck" };

    protected override string[] RequiredColumns => new[]
    {
        "PoliceNo", "BrutPrimTL", "BaslamaTarihi"
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 1;

        foreach (var row in rows)
        {
            rowNumber++;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,
                PoliceNo = GetStringValue(row, "PoliceNo", "Poliçe No", "Police No"),
                ZeyilNo = GetZeyilNo(GetStringValue(row, "ZeyilNo", "Zeyil No")).ToString(),
                YenilemeNo = GetStringValue(row, "YenilemeNo", "Yenileme No"),
                Plaka = GetStringValue(row, "Plaka", "AracPlaka"),
                TanzimTarihi = GetDateValue(row, "TanzimTarihi", "Tanzim Tarihi", "DuzenlenmeTarihi"),
                BaslangicTarihi = GetDateValue(row, "BaslamaTarihi", "Başlama Tarihi", "BaslangicTarihi"),
                BitisTarihi = GetDateValue(row, "BitisTarihi", "Bitiş Tarihi", "SonlanmaTarihi"),
                BrutPrim = GetDecimalValue(row, "BrutPrimTL", "BrutPrim", "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "NetPrimTL", "NetPrim", "Net Prim"),
                Komisyon = GetDecimalValue(row, "KomisyonTL", "Komisyon", "Komisyon Tutarı"),
                Vergi = GetDecimalValue(row, "VergiTL", "Vergi", "Vergi Tutarı"),
                SigortaliAdi = GetStringValue(row, "SigortaliAdi", "Sigortalı Adı", "MusteriAdi"),
                TcVkn = GetStringValue(row, "TcKimlikNo", "TcNo", "TC", "VKN", "VergiNo"),
                PoliceTipi = DetectPoliceTipi(row),
                UrunAdi = DetectUrunAdi(row) ?? GetStringValue(row, "UrunAdi", "Ürün", "Brans", "BransAdi"),
                AcenteAdi = GetStringValue(row, "AcenteAdi", "Acente"),
                Sube = GetStringValue(row, "SubeAdi", "Sube", "Şube"),
                PoliceKesenPersonel = GetStringValue(row, "PersonelAdi", "Personel", "Uretici")
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
}
