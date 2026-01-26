using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Hepiyi Sigorta Excel parser
/// Kolon mapping: Poliçe No, Brüt Prim, Poliçe Tarih
/// </summary>
public class HepiyiExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 3; // Hepiyi Sigorta ID'si
    public override string SirketAdi => "Hepiyi Sigorta";
    public override string[] FileNamePatterns => new[] { "hepiyi", "hepİyİ", "hepi̇yi̇" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim", "Poliçe Tarih"
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
                PoliceNo = GetStringValue(row, "Poliçe No", "PoliceNo", "Police No"),
                ZeyilNo = GetZeyilNo(GetStringValue(row, "Zeyil No", "ZeyilNo", "Zeyil")).ToString(),
                YenilemeNo = GetStringValue(row, "Yenileme No", "YenilemeNo"),
                Plaka = GetStringValue(row, "Plaka", "Araç Plaka"),
                TanzimTarihi = GetDateValue(row, "Poliçe Tarih", "Tanzim Tarihi", "TanzimTarihi"),
                BaslangicTarihi = GetDateValue(row, "Poliçe Tarih", "Başlangıç Tarihi", "BaslangicTarihi"),
                BitisTarihi = GetDateValue(row, "Bitiş Tarihi", "BitisTarihi", "Son Geçerlilik"),
                BrutPrim = GetDecimalValue(row, "Brüt Prim", "BrutPrim", "Brut Prim"),
                NetPrim = GetDecimalValue(row, "Net Prim", "NetPrim"),
                Komisyon = GetDecimalValue(row, "Komisyon", "Komisyon Tutarı"),
                Vergi = GetDecimalValue(row, "Vergi", "Vergi Tutarı"),
                SigortaliAdi = GetStringValue(row, "Sigortalı Adı", "SigortaliAdi", "Sigortalı", "Müşteri"),
                TcVkn = GetStringValue(row, "TC Kimlik", "TcKimlikNo", "TC", "VKN"),
                PoliceTipi = DetectPoliceTipi(row),
                UrunAdi = DetectUrunAdi(row) ?? GetStringValue(row, "Ürün Adı", "UrunAdi", "Branş"),
                AcenteAdi = GetStringValue(row, "Acente", "AcenteAdi"),
                Sube = GetStringValue(row, "Şube", "Sube"),
                PoliceKesenPersonel = GetStringValue(row, "Personel", "Üretici")
            };

            // Bitiş tarihi yoksa başlangıç tarihinden 1 yıl sonra
            if (!dto.BitisTarihi.HasValue && dto.BaslangicTarihi.HasValue)
            {
                dto = dto with { BitisTarihi = dto.BaslangicTarihi.Value.AddYears(1) };
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
