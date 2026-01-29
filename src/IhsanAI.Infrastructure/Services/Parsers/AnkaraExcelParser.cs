using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Ankara Sigorta Excel parser
///
/// MAPPING:
/// *PoliceNo       = "Poliçe No"                   [OK]
/// *YenilemeNo     = "Yenileme No"                 [OK]
/// *ZeyilNo        = "Zeyil No"                    [OK]
/// *ZeyilTipKodu   = "Zeyil Türü"                  [OK]
/// *Brans          = "Branş"                       [OK]
/// *PoliceTipi     = "Tahakkuk / İptal"            [OK]
/// *TanzimTarihi   = "Poliçe Onay Tarihi"          [OK]
/// *BaslangicTarihi= "Poliçe Başlangıç Tarihi"     [OK]
/// *BitisTarihi    = "Poliçe Bitiş Tarihi"         [OK]
/// *ZeyilOnayTarihi= "Zeyil Onay Tarihi"           [OK]
/// *ZeyilBaslangicTarihi = "Zeyil Başlangıç Tarihi"[OK]
/// *BrutPrim       = "Brüt Prim ₺"                 [OK]
/// *NetPrim        = "Net Prim ₺"                  [OK]
/// *Komisyon       = "Komisyon ₺"                  [OK]
/// *SigortaliAdi   = "Sigortalı Adı / Ünvanı"      [OK]
/// *SigortaliSoyadi= YOK (birleşik)                [NO]
/// *Plaka          = "Plaka"                       [OK]
/// *AcenteNo       = "Partaj"                      [WARN]
/// </summary>
public class AnkaraExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 1;
    public override string SirketAdi => "Ankara Sigorta";
    public override string[] FileNamePatterns => new[] { "ankara", "ank" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim", "Başlangıç"
    };

    // Ankara'ya özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "Tahakkuk / İptal", "Branş"  // Bu kombinasyon sadece Ankara'da var
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "Poliçe No");

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No"),
                ZeyilNo = GetStringValue(row, "Zeyil No"),
                ZeyilTipKodu = GetStringValue(row, "Zeyil Türü"),
                Brans = GetStringValue(row, "Branş"),
                PoliceTipi = GetPoliceTipi(row),

                // Tarihler
                TanzimTarihi = GetDateValue(row, "Poliçe Onay Tarihi"),
                BaslangicTarihi = GetDateValue(row, "Poliçe Başlangıç Tarihi"),
                BitisTarihi = GetDateValue(row, "Poliçe Bitiş Tarihi"),
                ZeyilOnayTarihi = GetDateValue(row, "Zeyil Onay Tarihi"),
                ZeyilBaslangicTarihi = GetDateValue(row, "Zeyil Başlangıç Tarihi"),

                // Primler
                BrutPrim = GetDecimalValue(row, "Brüt Prim ₺", "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "Net Prim ₺", "Net Prim"),
                Komisyon = GetDecimalValue(row, "Komisyon ₺", "Komisyon"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "Sigortalı Adı / Ünvanı")?.Trim(),
                SigortaliSoyadi = null,  // Ankara'da birleşik

                // Araç Bilgileri
                Plaka = GetStringValue(row, "Plaka"),

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "Partaj")  // Partaj kodu
            };

            // Tanzim tarihi yoksa poliçe onay tarihini kullan
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

    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        var tahakkukIptal = GetStringValue(row, "Tahakkuk / İptal", "Tahakkuk/İptal");

        if (string.IsNullOrEmpty(tahakkukIptal))
        {
            var brutPrim = GetDecimalValue(row, "Brüt Prim ₺", "Brüt Prim");
            return brutPrim < 0 ? "İPTAL" : "TAHAKKUK";
        }

        return tahakkukIptal.ToUpperInvariant().Contains("İPTAL") ||
               tahakkukIptal.ToUpperInvariant().Contains("IPTAL")
            ? "İPTAL"
            : "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue)
            errors.Add("Poliçe Başlangıç Tarihi geçersiz");

        // Zeyil kontrolü - robust parsing ile (zeyillerde 0 veya negatif prim olabilir)
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil && (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            errors.Add("Brüt Prim boş veya sıfır");
        // Zeyil için prim 0 veya negatif olabilir

        return errors;
    }
}
