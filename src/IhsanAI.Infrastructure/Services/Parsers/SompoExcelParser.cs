using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Sompo Sigorta Excel parser
/// SOMPO formatı: Header 2. satırda (ExcelImportService tarafından işlenir)
/// Kolonlar: Poliçe No, Yenileme No, Zeyl No, Onay Tarihi, Sigortalı Ünvanı, Net Prim, Brüt Prim, Komisyon
/// NOT: SOMPO formatında TC/VKN, Plaka, Bitiş Tarihi YOKTUR!
/// </summary>
public class SompoExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 6; // Sompo Sigorta ID'si
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

            // Poliçe No'yu al - SOMPO formatında tam olarak bu kolon adı
            var policeNo = GetStringValue(row, "Poliçe No");

            // Boş veya header satırlarını atla
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // "Poliçe No" yazısını içeren header satırını atla
            if (policeNo.ToUpperInvariant().Contains("POLİÇE") ||
                policeNo.ToUpperInvariant().Contains("POLICE"))
                continue;

            // Onay Tarihi'ni al - bu hem tanzim hem başlangıç tarihi olacak
            var onayTarihi = GetDateValue(row, "Onay Tarihi");

            // Ürün adını dosya adından tespit et (DASK, KASKO vs)
            var urunAdi = DetectUrunFromFileName(row);

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No"),
                ZeyilNo = GetStringValue(row, "Zeyl No", "Zeyil No"),

                // SOMPO'da sadece Onay Tarihi var, başlangıç ve bitiş yok
                TanzimTarihi = onayTarihi,
                BaslangicTarihi = onayTarihi,
                BitisTarihi = onayTarihi?.AddYears(1), // Varsayılan 1 yıl

                // Prim ve komisyon bilgileri
                NetPrim = GetDecimalValue(row, "Net Prim"),
                BrutPrim = GetDecimalValue(row, "Brüt Prim"),
                Komisyon = GetDecimalValue(row, "Komisyon"),

                // Sigortalı bilgileri
                SigortaliAdi = GetStringValue(row, "Sigortalı Ünvanı")?.Trim(),

                // SOMPO formatında TC/VKN ve Plaka yok
                TcVkn = null,
                Plaka = null,

                // Ürün ve diğer bilgiler
                UrunAdi = urunAdi,
                PoliceTipi = DetectPoliceTipiFromRow(row),

                // SOMPO'da acente/şube bilgisi yok
                AcenteAdi = null,
                Sube = null,
                PoliceKesenPersonel = null
            };

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

    private string? DetectUrunFromFileName(IDictionary<string, object?> row)
    {
        // İlk kolondan ürün numarası alınabilir
        var urunNo = GetStringValue(row, "Ürün No", "UrunNo");

        // Ürün numarasına göre mapping
        return urunNo switch
        {
            "117" => "DASK",
            "115" => "KASKO",
            "101" => "TRAFİK",
            _ => null
        };
    }

    private string DetectPoliceTipiFromRow(IDictionary<string, object?> row)
    {
        // SOMPO'da zeyil tipine bakılabilir
        var zeyilNo = GetStringValue(row, "Zeyl No", "Zeyil No");

        // Zeyil no > 0 ise ve prim negatifse iptal olabilir
        var brutPrim = GetDecimalValue(row, "Brüt Prim");

        if (brutPrim < 0)
            return "İPTAL";

        return "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue)
            errors.Add("Onay Tarihi geçersiz");

        if (!row.BrutPrim.HasValue || row.BrutPrim == 0)
            errors.Add("Brüt Prim boş veya sıfır");

        // SOMPO'da TC/VKN zorunlu değil
        // SOMPO'da Plaka zorunlu değil

        return errors;
    }
}
