using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Ankara Sigorta Excel parser
/// Kolonlar: Poliçe No, Yenileme No, Zeyil No, Tahakkuk / İptal, Brüt Prim ₺, Net Prim ₺,
/// Komisyon ₺, Vergiler ₺, Poliçe Onay Tarihi, Poliçe Başlangıç Tarihi, Poliçe Bitiş Tarihi,
/// Sigortalı Adı / Ünvanı, Plaka, Ürün, Partaj Adı
/// NOT: Ankara formatında TC/VKN kolonu YOKTUR!
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
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // Poliçe No'yu al
            var policeNo = GetStringValue(row, "Poliçe No");

            // Boş satırları atla
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Sigortalı adını birleştir
            var sigortaliAdi = GetStringValue(row, "Sigortalı Adı / Ünvanı", "Sigortalı Adı", "Sigortalı Ünvanı");

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No"),
                ZeyilNo = GetStringValue(row, "Zeyil No"),

                // Tarihler - Ankara'da 3 farklı tarih var
                TanzimTarihi = GetDateValue(row, "Poliçe Onay Tarihi", "Zeyil Onay Tarihi"),
                BaslangicTarihi = GetDateValue(row, "Poliçe Başlangıç Tarihi", "Zeyil Başlangıç Tarihi"),
                BitisTarihi = GetDateValue(row, "Poliçe Bitiş Tarihi"),

                // Prim ve komisyon - Ankara'da TL sembolü var
                BrutPrim = GetDecimalValue(row, "Brüt Prim ₺", "Brüt Prim"),
                NetPrim = GetDecimalValue(row, "Net Prim ₺", "Net Prim"),
                Komisyon = GetDecimalValue(row, "Komisyon ₺", "Komisyon"),
                Vergi = GetDecimalValue(row, "Vergiler ₺", "Vergi"),

                // Sigortalı bilgileri
                SigortaliAdi = sigortaliAdi?.Trim(),
                Plaka = GetStringValue(row, "Plaka"),

                // Ankara'da TC/VKN kolonu YOK
                TcVkn = null,

                // Poliçe tipi - Tahakkuk / İptal kolonu
                PoliceTipi = GetPoliceTipi(row),

                // Ürün adı - Ankara'da "Ürün" kolonu var
                UrunAdi = GetStringValue(row, "Ürün", "Branş"),

                // Acente bilgisi
                AcenteAdi = GetStringValue(row, "Partaj Adı"),
                Sube = null,
                PoliceKesenPersonel = null
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
        var tahakkukIptal = GetStringValue(row, "Tahakkuk / İptal", "Tahakkuk/İptal");

        if (string.IsNullOrEmpty(tahakkukIptal))
        {
            // Brüt prim negatifse iptal
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

        if (!row.BrutPrim.HasValue || row.BrutPrim == 0)
            errors.Add("Brüt Prim boş veya sıfır");

        // Ankara'da TC/VKN ve Plaka zorunlu değil

        return errors;
    }
}
