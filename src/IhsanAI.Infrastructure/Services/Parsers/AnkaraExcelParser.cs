using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Ankara Sigorta Excel parser
/// Kolon mapping: Poliçe No, Brüt Prim ₺, Poliçe Başlangıç Tarihi
/// </summary>
public class AnkaraExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 1; // Ankara Sigorta ID'si
    public override string SirketAdi => "Ankara Sigorta";
    public override string[] FileNamePatterns => new[] { "ankara", "ank" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim", "Poliçe Başlangıç"
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
                Plaka = GetStringValue(row, "Plaka", "Arac Plaka", "AracPlaka"),
                TanzimTarihi = GetDateValue(row, "Tanzim Tarihi", "TanzimTarihi", "Düzenleme Tarihi"),
                BaslangicTarihi = GetDateValue(row, "Poliçe Başlangıç Tarihi", "Başlangıç Tarihi", "BaslangicTarihi", "Baslangic"),
                BitisTarihi = GetDateValue(row, "Poliçe Bitiş Tarihi", "Bitiş Tarihi", "BitisTarihi", "Bitis"),
                BrutPrim = GetDecimalValue(row, "Brüt Prim ₺", "Brüt Prim", "BrutPrim", "Brut Prim"),
                NetPrim = GetDecimalValue(row, "Net Prim ₺", "Net Prim", "NetPrim"),
                Komisyon = GetDecimalValue(row, "Komisyon ₺", "Komisyon", "Komisyon Tutarı"),
                Vergi = GetDecimalValue(row, "Vergi ₺", "Vergi", "Vergi Tutarı"),
                SigortaliAdi = GetStringValue(row, "Sigortalı Adı", "SigortaliAdi", "Sigortalı", "Müşteri Adı"),
                TcVkn = GetStringValue(row, "TC Kimlik No", "TcKimlikNo", "TC", "VKN", "Vergi No"),
                PoliceTipi = DetectPoliceTipi(row),
                UrunAdi = DetectUrunAdi(row) ?? GetStringValue(row, "Ürün Adı", "UrunAdi", "Branş", "Brans"),
                AcenteAdi = GetStringValue(row, "Acente", "AcenteAdi", "Acente Adı"),
                Sube = GetStringValue(row, "Şube", "Sube", "Şube Adı"),
                PoliceKesenPersonel = GetStringValue(row, "Kesen Personel", "Personel", "Üretici")
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
