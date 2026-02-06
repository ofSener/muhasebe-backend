using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Ankara Sigorta Excel parser
///
/// İki farklı format desteklenir:
///
/// FORMAT 1 - Zeyilli Format (27 kolon):
/// Ürün No, Ürün, Branş, Poliçe No, Yenileme No, Zeyil No, Zeyil Türü,
/// Tahakkuk / İptal, Brüt Prim ₺, Net Prim ₺, Brüt Prim, Net Prim, Döviz Cinsi,
/// Komisyon ₺, Vergiler ₺, Zeyil Onay Tarihi, Zeyil Başlangıç Tarihi,
/// Poliçe Onay Tarihi, Poliçe Başlangıç Tarihi, Poliçe Bitiş Tarihi,
/// Müşteri No, Sigortalı No, Sigortalı Adı / Ünvanı, Plaka,
/// Trafik / Kasko Basamak, Partaj, Partaj Adı
///
/// FORMAT 2 - Zeyilsiz Format (27 kolon):
/// Ürün No, Ürün, Branş, Poliçe No, Yenileme No, Tahakkuk / İptal,
/// Brüt Prim ₺, Net Prim ₺, Brüt Prim, Net Prim, Döviz Cinsi, Komisyon ₺,
/// Poliçe Onay Tarihi, Son İşlem Tarihi, Poliçe Başlangıç Tarihi,
/// Poliçe Bitiş Tarihi, İptal Tarihi, Müşteri No, Sigortalı No,
/// Sigortalı Adı / Ünvanı, Plaka, Trafik / Kasko Basamak, Partaj, Partaj Adı,
/// Poliçe İptal Gerekçesi, Komisyon Gizle
///
/// NOT: İptal satırlarında prim ve komisyon değerleri pozitif gelebilir.
/// Bu durumda otomatik olarak negatife çevrilir.
///
/// BRANŞ → BransId:
/// TRAFİK → 0, KASKO → 1, DASK → 2, GENEL KAZA → 3, SAĞLIK → 7,
/// SEYAHAT → 8, IMM → 12, YABANCI SAĞLIK → 15, TAMAMLAYICI SAĞLIK → 16
/// </summary>
public class AnkaraExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 9;
    public override string SirketAdi => "Ankara Sigorta";
    public override string[] FileNamePatterns => new[] { "ankara", "ank" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim", "Başlangıç"
    };

    // Ankara'ya özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "Tahakkuk / İptal", "Partaj Adı"  // Bu kombinasyon sadece Ankara'da var
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "Poliçe No", "Police No");

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Branş tespiti
            var bransRaw = GetStringValue(row, "Branş", "Brans");
            var bransId = GetBransIdFromBrans(bransRaw);
            var bransAdi = GetStandardBransAdi(bransId) ?? bransRaw;

            // Tarihler - Zeyil varsa zeyil tarihlerini, yoksa poliçe tarihlerini kullan
            var zeyilOnayTarihi = GetDateValue(row, "Zeyil Onay Tarihi");
            var policeOnayTarihi = GetDateValue(row, "Poliçe Onay Tarihi", "Police Onay Tarihi");
            var zeyilBaslangicTarihi = GetDateValue(row, "Zeyil Başlangıç Tarihi", "Zeyil Baslangic Tarihi");
            var policeBaslangicTarihi = GetDateValue(row, "Poliçe Başlangıç Tarihi", "Police Baslangic Tarihi");
            var policeBitisTarihi = GetDateValue(row, "Poliçe Bitiş Tarihi", "Police Bitis Tarihi");

            // Tabloda gösterim için: Tanzim = Poliçe Onay, Zeyil Onay ayrı
            // Kayıt sırasında ExcelImportService zeyil varsa onu kullanacak
            var tanzimTarihi = policeOnayTarihi;
            // Başlangıç: Önce Zeyil Başlangıç, yoksa Poliçe Başlangıç
            var baslangicTarihi = zeyilBaslangicTarihi ?? policeBaslangicTarihi;

            // Önce poliçe tipini belirle (İptal mi Tahakkuk mu)
            var policeTipi = GetPoliceTipi(row);

            // Primler - TL cinsinden (₺ kolonları)
            var brutPrim = GetDecimalValue(row, "Brüt Prim ₺", "Brüt Prim ?");
            var netPrim = GetDecimalValue(row, "Net Prim ₺", "Net Prim ?");
            var komisyon = GetDecimalValue(row, "Komisyon ₺", "Komisyon ?");

            // İptal satırlarında pozitif değerleri negatife çevir
            if (policeTipi == "İPTAL")
            {
                if (brutPrim.HasValue && brutPrim.Value > 0)
                    brutPrim = -brutPrim.Value;
                if (netPrim.HasValue && netPrim.Value > 0)
                    netPrim = -netPrim.Value;
                if (komisyon.HasValue && komisyon.Value > 0)
                    komisyon = -komisyon.Value;
            }

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No"),
                ZeyilNo = GetStringValue(row, "Zeyil No"),
                ZeyilTipKodu = GetStringValue(row, "Zeyil Türü", "Zeyil Turu"),
                Brans = bransAdi,
                BransId = bransId,
                PoliceTipi = policeTipi,

                // Tarihler
                TanzimTarihi = tanzimTarihi,
                BaslangicTarihi = baslangicTarihi,
                BitisTarihi = policeBitisTarihi,
                ZeyilOnayTarihi = zeyilOnayTarihi,
                ZeyilBaslangicTarihi = zeyilBaslangicTarihi,

                // Primler (iptal için negatife çevrilmiş olabilir)
                BrutPrim = brutPrim,
                NetPrim = netPrim,
                Komisyon = komisyon,

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "Sigortalı Adı / Ünvanı", "Sigortali Adi / Unvani")?.Trim(),
                SigortaliSoyadi = null,  // Ankara'da birleşik

                // Araç Bilgileri
                Plaka = GetStringValue(row, "Plaka"),

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "Partaj")
            };

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

    /// <summary>
    /// Branş kolonundan BransId çıkarır
    /// </summary>
    private static int? GetBransIdFromBrans(string? brans)
    {
        if (string.IsNullOrWhiteSpace(brans))
            return null;

        var value = brans.ToUpperInvariant()
            .Replace("İ", "I")
            .Replace("Ğ", "G")
            .Replace("Ü", "U")
            .Replace("Ş", "S")
            .Replace("Ö", "O")
            .Replace("Ç", "C");

        // Trafik
        if (value.Contains("TRAFIK"))
            return 0;

        // Kasko
        if (value.Contains("KASKO"))
            return 1;

        // DASK
        if (value.Contains("DASK"))
            return 2;

        // Ferdi Kaza / Genel Kaza
        if (value.Contains("GENEL KAZA") || value.Contains("FERDI KAZA"))
            return 3;

        // Tamamlayıcı Sağlık
        if (value.Contains("TAMAMLAYICI"))
            return 16;

        // Yabancı Sağlık
        if (value.Contains("YABANCI"))
            return 15;

        // Sağlık (genel)
        if (value.Contains("SAGLIK"))
            return 7;

        // IMM
        if (value.Contains("IMM"))
            return 12;

        // Seyahat
        if (value.Contains("SEYAHAT"))
            return 8;

        return 255; // Belli Değil
    }

    /// <summary>
    /// BransId'den standart branş adı döndürür
    /// </summary>
    private static string? GetStandardBransAdi(int? bransId)
    {
        return bransId switch
        {
            0 => "TRAFİK",
            1 => "KASKO",
            2 => "DASK",
            3 => "FERDİ KAZA",
            7 => "SAĞLIK",
            8 => "SEYAHAT SAĞLIK",
            12 => "IMM",
            15 => "YABANCI SAĞLIK",
            16 => "TAMAMLAYICI SAĞLIK",
            255 => "DİĞER",
            _ => null
        };
    }

    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        var tahakkukIptal = GetStringValue(row, "Tahakkuk / İptal", "Tahakkuk/İptal", "Tahakkuk / Iptal");

        if (!string.IsNullOrEmpty(tahakkukIptal))
        {
            var upper = tahakkukIptal.ToUpperInvariant();
            if (upper.Contains("İPTAL") || upper.Contains("IPTAL"))
                return "İPTAL";
        }

        // Brüt prim negatifse iptal
        var brutPrim = GetDecimalValue(row, "Brüt Prim ₺", "Brüt Prim ?");
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
            errors.Add("Poliçe Başlangıç Tarihi geçersiz");

        // İptal veya Zeyil ise negatif/sıfır prim kabul edilir
        var isIptal = row.PoliceTipi == "İPTAL";
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);

        if (!isIptal && !isZeyil && (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            errors.Add("Brüt Prim boş veya sıfır");

        return errors;
    }
}
