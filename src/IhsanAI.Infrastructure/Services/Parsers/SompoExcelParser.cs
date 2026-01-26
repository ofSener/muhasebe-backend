using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Sompo Sigorta Excel parser
/// Özel format: Header 2. satırda, ilk satır firma bilgisi
/// Kolon mapping: Column_1 → PoliceNo, Column_7 → BrutPrim, Column_4 → TanzimTarihi
/// </summary>
public class SompoExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 6; // Sompo Sigorta ID'si
    public override string SirketAdi => "Sompo Sigorta";
    public override string[] FileNamePatterns => new[] { "sompo", "smp" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe", "Prim", "Tarih"  // Genel arama için
    };

    public override bool CanParse(string fileName, IEnumerable<string> headerColumns)
    {
        // Dosya adı kontrolü
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

            // Sompo formatında ilk satır header değil firma bilgisi olabilir
            // Boş veya anlamsız satırları atla
            var policeNo = GetStringValue(row,
                "Column_1", "Column_0", "Poliçe No", "PoliceNo", "Police No",
                "POLİÇE NO", "POLICE NO");

            if (string.IsNullOrWhiteSpace(policeNo) ||
                policeNo.ToUpperInvariant().Contains("POLİÇE") ||
                policeNo.ToUpperInvariant().Contains("POLICE"))
            {
                continue; // Header satırını atla
            }

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,
                PoliceNo = policeNo,
                ZeyilNo = GetZeyilNo(GetStringValue(row,
                    "Column_2", "Zeyil No", "ZeyilNo", "ZEYİL NO")).ToString(),
                YenilemeNo = GetStringValue(row,
                    "Column_3", "Yenileme No", "YenilemeNo"),
                Plaka = GetStringValue(row,
                    "Column_5", "Plaka", "PLAKA", "Araç Plaka"),
                TanzimTarihi = GetDateValue(row,
                    "Column_4", "Tanzim Tarihi", "TanzimTarihi", "TANZİM TARİHİ"),
                BaslangicTarihi = GetDateValue(row,
                    "Column_6", "Column_4", "Başlangıç Tarihi", "BaslangicTarihi", "BAŞLANGIÇ"),
                BitisTarihi = GetDateValue(row,
                    "Column_8", "Column_5", "Bitiş Tarihi", "BitisTarihi", "BİTİŞ"),
                BrutPrim = GetDecimalValue(row,
                    "Column_7", "Column_9", "Brüt Prim", "BrutPrim", "BRÜT PRİM", "PRİM"),
                NetPrim = GetDecimalValue(row,
                    "Column_10", "Net Prim", "NetPrim", "NET PRİM"),
                Komisyon = GetDecimalValue(row,
                    "Column_11", "Komisyon", "KOMİSYON"),
                Vergi = GetDecimalValue(row,
                    "Column_12", "Vergi", "VERGİ"),
                SigortaliAdi = GetStringValue(row,
                    "Column_13", "Column_3", "Sigortalı Adı", "SigortaliAdi", "SİGORTALI"),
                TcVkn = GetStringValue(row,
                    "Column_14", "Column_4", "TC Kimlik", "TcKimlikNo", "TC", "VKN"),
                PoliceTipi = DetectPoliceTipi(row),
                UrunAdi = DetectUrunAdi(row) ?? GetStringValue(row,
                    "Column_15", "Ürün", "UrunAdi", "BRANŞ"),
                AcenteAdi = GetStringValue(row,
                    "Column_16", "Acente", "AcenteAdi", "ACENTE"),
                Sube = GetStringValue(row,
                    "Column_17", "Şube", "Sube", "ŞUBE"),
                PoliceKesenPersonel = GetStringValue(row,
                    "Column_18", "Personel", "ÜRETİCİ")
            };

            // Başlangıç tarihi yoksa tanzim tarihini kullan
            if (!dto.BaslangicTarihi.HasValue && dto.TanzimTarihi.HasValue)
            {
                dto = dto with { BaslangicTarihi = dto.TanzimTarihi };
            }

            // Tanzim tarihi yoksa başlangıç tarihini kullan
            if (!dto.TanzimTarihi.HasValue && dto.BaslangicTarihi.HasValue)
            {
                dto = dto with { TanzimTarihi = dto.BaslangicTarihi };
            }

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
