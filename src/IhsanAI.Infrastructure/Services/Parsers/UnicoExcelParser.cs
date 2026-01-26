using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Unico Sigorta (eski Aviva) Excel parser
/// Kolon mapping: Poliçe No, Brüt Prim, Başlama Tarihi
/// </summary>
public class UnicoExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 5; // Unico Sigorta ID'si
    public override string SirketAdi => "Unico Sigorta";
    public override string[] FileNamePatterns => new[] { "unico", "aviva" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim", "Başlama Tarihi"
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
                ZeyilNo = GetZeyilNo(GetStringValue(row, "Zeyil No", "ZeyilNo")).ToString(),
                YenilemeNo = GetStringValue(row, "Yenileme No", "YenilemeNo"),
                Plaka = GetStringValue(row, "Plaka", "Araç Plaka"),
                TanzimTarihi = GetDateValue(row, "Tanzim Tarihi", "TanzimTarihi", "Düzenleme Tarihi"),
                BaslangicTarihi = GetDateValue(row, "Başlama Tarihi", "Başlangıç Tarihi", "BaslangicTarihi"),
                BitisTarihi = GetDateValue(row, "Bitiş Tarihi", "BitisTarihi", "Son Geçerlilik"),
                BrutPrim = GetDecimalValue(row, "Brüt Prim", "BrutPrim", "Brut Prim"),
                NetPrim = GetDecimalValue(row, "Net Prim", "NetPrim"),
                Komisyon = GetDecimalValue(row, "Komisyon", "Komisyon Tutarı"),
                Vergi = GetDecimalValue(row, "Vergi", "Vergi Tutarı"),
                SigortaliAdi = GetStringValue(row, "Sigortalı Adı", "SigortaliAdi", "Sigortalı", "Müşteri Adı"),
                TcVkn = GetStringValue(row, "TC Kimlik No", "TcKimlikNo", "TC", "VKN", "Vergi No"),
                PoliceTipi = DetectPoliceTipi(row),
                UrunAdi = DetectUrunAdi(row) ?? GetStringValue(row, "Ürün Adı", "UrunAdi", "Branş"),
                AcenteAdi = GetStringValue(row, "Acente", "AcenteAdi", "Acente Adı"),
                Sube = GetStringValue(row, "Şube", "Sube", "Şube Adı"),
                PoliceKesenPersonel = GetStringValue(row, "Personel", "Üretici", "Kesen Personel")
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
