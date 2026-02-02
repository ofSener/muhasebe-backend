using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Ankara Sigorta Excel parser
///
/// KOLONLAR (27 kolon):
/// Col 1:  Ürün No
/// Col 2:  Ürün                    (Ürün adı - detaylı)
/// Col 3:  Branş                   (DASK, KASKO, TRAFİK, SAĞLIK, vb.)
/// Col 4:  Poliçe No
/// Col 5:  Yenileme No
/// Col 6:  Zeyil No
/// Col 7:  Zeyil Türü
/// Col 8:  Tahakkuk / İptal
/// Col 9:  Brüt Prim ₺             (TL cinsinden - dövizli değil)
/// Col 10: Net Prim ₺
/// Col 11: Brüt Prim               (Döviz cinsinden)
/// Col 12: Net Prim
/// Col 13: Döviz Cinsi
/// Col 14: Komisyon ₺
/// Col 15: Vergiler ₺
/// Col 16: Zeyil Onay Tarihi       (DateTime)
/// Col 17: Zeyil Başlangıç Tarihi  (DateTime)
/// Col 18: Poliçe Onay Tarihi      (DateTime)
/// Col 19: Poliçe Başlangıç Tarihi (DateTime)
/// Col 20: Poliçe Bitiş Tarihi     (DateTime)
/// Col 21: Müşteri No
/// Col 22: Sigortalı No
/// Col 23: Sigortalı Adı / Ünvanı
/// Col 24: Plaka
/// Col 25: Trafik / Kasko Basamak
/// Col 26: Partaj
/// Col 27: Partaj Adı
///
/// BRANŞ → BransId:
/// TRAFİK → 0 (Trafik)
/// KASKO → 1 (Kasko)
/// DASK → 2 (DASK)
/// GENEL KAZA → 3 (Ferdi Kaza)
/// SAĞLIK → 7 (Sağlık)
/// TAMAMLAYICI SAĞLIK → 16 (Tamamlayıcı Sağlık)
/// YABANCI SAĞLIK → 15 (Yabancı Sağlık)
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
        "Tahakkuk / İptal", "Branş"  // Bu kombinasyon sadece Ankara'da var
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
                PoliceTipi = GetPoliceTipi(row),

                // Tarihler
                TanzimTarihi = tanzimTarihi,
                BaslangicTarihi = baslangicTarihi,
                BitisTarihi = policeBitisTarihi,
                ZeyilOnayTarihi = zeyilOnayTarihi,
                ZeyilBaslangicTarihi = zeyilBaslangicTarihi,

                // Primler - TL cinsinden (₺ kolonları)
                BrutPrim = GetDecimalValue(row, "Brüt Prim ₺", "Brüt Prim ?"),
                NetPrim = GetDecimalValue(row, "Net Prim ₺", "Net Prim ?"),
                Komisyon = GetDecimalValue(row, "Komisyon ₺", "Komisyon ?"),

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

        // Zeyil kontrolü - zeyillerde 0 veya negatif prim olabilir
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil && (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            errors.Add("Brüt Prim boş veya sıfır");

        return errors;
    }
}
