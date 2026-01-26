using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Neova Sigorta Excel parser
/// Kolon mapping: POLİÇE NO, BRÜT PRİM, BAŞLANGIÇ TARİHİ
/// </summary>
public class NeovaExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 4; // Neova Sigorta ID'si
    public override string SirketAdi => "Neova Sigorta";
    public override string[] FileNamePatterns => new[] { "neova", "neo" };

    protected override string[] RequiredColumns => new[]
    {
        "POLİÇE NO", "BRÜT PRİM", "BAŞLANGIÇ TARİHİ"
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
                PoliceNo = GetStringValue(row, "POLİÇE NO", "POLICE NO", "Poliçe No", "PoliceNo"),
                ZeyilNo = GetZeyilNo(GetStringValue(row, "ZEYİL NO", "ZEYIL NO", "Zeyil No")).ToString(),
                YenilemeNo = GetStringValue(row, "YENİLEME NO", "YENILEME NO", "Yenileme No"),
                Plaka = GetStringValue(row, "PLAKA", "ARAÇ PLAKA", "Plaka"),
                TanzimTarihi = GetDateValue(row, "TANZİM TARİHİ", "TANZIM TARIHI", "Tanzim Tarihi"),
                BaslangicTarihi = GetDateValue(row, "BAŞLANGIÇ TARİHİ", "BASLANGIC TARIHI", "Başlangıç Tarihi"),
                BitisTarihi = GetDateValue(row, "BİTİŞ TARİHİ", "BITIS TARIHI", "Bitiş Tarihi"),
                BrutPrim = GetDecimalValue(row, "BRÜT PRİM", "BRUT PRIM", "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "NET PRİM", "NET PRIM", "Net Prim"),
                Komisyon = GetDecimalValue(row, "KOMİSYON", "KOMISYON", "Komisyon"),
                Vergi = GetDecimalValue(row, "VERGİ", "VERGI", "Vergi"),
                SigortaliAdi = GetStringValue(row, "SİGORTALI ADI", "SIGORTALI ADI", "Sigortalı Adı", "MÜŞTERİ"),
                TcVkn = GetStringValue(row, "TC KİMLİK NO", "TC KIMLIK NO", "TC", "VKN", "VERGİ NO"),
                PoliceTipi = DetectPoliceTipi(row),
                UrunAdi = DetectUrunAdi(row) ?? GetStringValue(row, "ÜRÜN ADI", "URUN ADI", "BRANŞ", "BRANS"),
                AcenteAdi = GetStringValue(row, "ACENTE", "ACENTE ADI"),
                Sube = GetStringValue(row, "ŞUBE", "SUBE"),
                PoliceKesenPersonel = GetStringValue(row, "PERSONEL", "ÜRETİCİ", "URETICI")
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
