using System.Globalization;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Unico (Aviva) Sigorta Excel parser
/// Tarih formatı: "12/04/2025 00:00:00" (MM/DD/YYYY)
///
/// KOLONLAR:
/// Col 1: Bölge Adı
/// Col 2: Şube Kodu
/// Col 3: Acente No
/// Col 4: Acente Adı
/// Col 5: Onaylayan Kullanıcı
/// Col 6: Poliçe No
/// Col 7: Tarife (ürün kodu)
/// Col 8: Tarife Adı
/// Col 9: Zeyl No
/// Col 10: Yenileme No
/// Col 11: Tanzim Tarihi
/// Col 12: Başlama Tarihi
/// Col 13: Bitiş Tarihi
/// Col 14: Sigortalı No
/// Col 15: Sigortalı Adı
/// Col 16: Sigortalı Soyadı
/// Col 17: Aviva Müşteri
/// Col 18: Döviz
/// Col 19: Net Prim
/// Col 20: Brüt Prim
/// Col 21: Komisyon Oranı
/// Col 22: Komisyon Tutarı
/// Col 23-26: GV, YSV, GF, TF (vergiler)
///
/// TARİFE KODU EŞLEŞTİRME (Unico Kodu → BransId):
/// 408 → 0 (Trafik)
/// 499 → 1 (Kasko/UNIKASKO)
/// 137, 318 → 2 (Dask)
/// 598 → 3 (Ferdi Kaza)
/// 100 → 5 (Konut)
/// 599 → 7 (Sağlık/Kritik Hastalıklar)
/// 517, 521 → 30 (Yol Destek)
/// </summary>
public class UnicoExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 17;
    public override string SirketAdi => "Unico Sigorta";
    public override string[] FileNamePatterns => new[] { "unico", "aviva" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Prim", "Tarih"
    };

    // Unico'ya özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "Tarife Adı", "Sigortalı Adı", "Sigortalı Soyadı"
    };

    /// <summary>
    /// Unico tarife kodu → BransId eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, int> TarifeKoduMapping = new()
    {
        { "408", 0 },   // Trafik
        { "499", 1 },   // Kasko (UNIKASKO)
        { "137", 2 },   // Dask
        { "318", 2 },   // Dask
        { "598", 3 },   // Ferdi Kaza
        { "100", 5 },   // Konut
        { "599", 7 },   // Sağlık (Kritik Hastalıklar)
        { "517", 30 },  // Yol Destek (TR ASSIST)
        { "521", 30 },  // Yol Destek (TUR ASSIST)
    };

    /// <summary>
    /// Tarife adından BransId çıkarır
    /// </summary>
    private static int? GetBransIdFromTarifeAdi(string? tarifeAdi)
    {
        if (string.IsNullOrWhiteSpace(tarifeAdi))
            return null;

        var value = tarifeAdi.ToUpperInvariant()
            .Replace("İ", "I")
            .Replace("Ü", "U")
            .Replace("Ö", "O")
            .Replace("Ş", "S")
            .Replace("Ç", "C")
            .Replace("Ğ", "G");

        // Trafik
        if (value.Contains("TRAFIK"))
            return 0;

        // Kasko
        if (value.Contains("KASKO"))
            return 1;

        // Dask
        if (value.Contains("DASK"))
            return 2;

        // Ferdi Kaza
        if (value.Contains("FERDI KAZA") || value.Contains("FERDIKAZA"))
            return 3;

        // Konut
        if (value.Contains("KONUT"))
            return 5;

        // Sağlık / Kritik Hastalıklar
        if (value.Contains("SAGLIK") || value.Contains("KRITIK") || value.Contains("HASTALIK"))
            return 7;

        // Yol Destek / Yol Yardım
        if (value.Contains("YOL DESTEK") || value.Contains("YOL YARDIM") ||
            value.Contains("ASSIST") || value.Contains("YOLDESTEK"))
            return 30;

        return 255; // Belli Değil
    }

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "Poliçe No", "POLİÇE NO", "POLICE NO");

            // Boş satırları ve toplam satırlarını atla
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // "Toplam" satırlarını atla
            var bolgeAdi = GetStringValue(row, "Bölge Adı", "BÖLGE ADI", "BOLGE ADI");
            if (bolgeAdi != null && bolgeAdi.Contains("Toplam", StringComparison.OrdinalIgnoreCase))
                continue;

            // Tarife kodu ve adı
            var tarifeKodu = GetStringValue(row, "Tarife", "TARİFE", "TARIFE");
            var tarifeAdi = GetStringValue(row, "Tarife Adı", "TARİFE ADI", "TARIFE ADI");

            // BransId belirleme: önce koda bak, yoksa ada bak
            int? bransId = null;
            if (!string.IsNullOrWhiteSpace(tarifeKodu) && TarifeKoduMapping.TryGetValue(tarifeKodu.Trim(), out var mappedId))
            {
                bransId = mappedId;
            }
            else
            {
                bransId = GetBransIdFromTarifeAdi(tarifeAdi);
            }

            var zeyilNo = GetStringValue(row, "Zeyl No", "ZEYL NO", "ZEYİL NO");
            var isZeyil = IsZeyilPolicy(zeyilNo);

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Yenileme No", "YENİLEME NO", "YENILEME NO"),
                ZeyilNo = zeyilNo,
                ZeyilTipKodu = null,
                Brans = tarifeAdi,
                BransId = bransId,
                PoliceTipi = GetPoliceTipiFromPrim(row),

                // Tarihler
                TanzimTarihi = GetUnicoDateValue(row, "Tanzim Tarihi", "TANZİM TARİHİ", "TANZIM TARIHI"),
                BaslangicTarihi = GetUnicoDateValue(row, "Başlama Tarihi", "BAŞLAMA TARİHİ", "BASLAMA TARIHI"),
                BitisTarihi = GetUnicoDateValue(row, "Bitiş Tarihi", "BİTİŞ TARİHİ", "BITIS TARIHI"),
                ZeyilOnayTarihi = null,
                ZeyilBaslangicTarihi = null,

                // Primler
                BrutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "BRUT PRIM"),
                NetPrim = GetDecimalValue(row, "Net Prim", "NET PRİM", "NET PRIM"),
                Komisyon = GetDecimalValue(row, "Komisyon Tutarı", "KOMİSYON TUTARI", "KOMISYON TUTARI"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "Sigortalı Adı", "SİGORTALI ADI", "SIGORTALI ADI")?.Trim(),
                SigortaliSoyadi = GetStringValue(row, "Sigortalı Soyadı", "SİGORTALI SOYADI", "SIGORTALI SOYADI")?.Trim(),
                Tckn = null,
                Vkn = null,
                Adres = null,

                // Araç/Acente Bilgileri
                Plaka = null,
                AcenteNo = GetStringValue(row, "Acente No", "ACENTE NO")
            };

            // Tanzim tarihi yoksa başlangıç tarihini kullan
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

    /// <summary>
    /// Unico tarih formatını parse eder ("12/04/2025 00:00:00" - MM/DD/YYYY formatı)
    /// </summary>
    private DateTime? GetUnicoDateValue(IDictionary<string, object?> row, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            var actualKey = row.Keys.FirstOrDefault(k =>
                NormalizeColumnName(k).Contains(NormalizeColumnName(key)) ||
                NormalizeColumnName(key).Contains(NormalizeColumnName(k)));

            if (actualKey != null && row.TryGetValue(actualKey, out var value) && value != null)
            {
                if (value is DateTime dt)
                    return dt;

                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrEmpty(strValue))
                    continue;

                var formats = new[]
                {
                    "MM/dd/yyyy HH:mm:ss",
                    "MM/dd/yyyy",
                    "dd/MM/yyyy HH:mm:ss",
                    "dd/MM/yyyy",
                    "yyyy-MM-dd'T'HH:mm:ss",
                    "dd.MM.yyyy",
                    "M/d/yyyy H:mm:ss",
                    "M/d/yyyy"
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(strValue, format, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var result))
                        return result;
                }

                // Genel parse
                if (DateTime.TryParse(strValue, out var generalResult))
                    return generalResult;
            }
        }

        return null;
    }

    private string GetPoliceTipiFromPrim(IDictionary<string, object?> row)
    {
        var brutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "BRUT PRIM");
        if (brutPrim < 0)
            return "İPTAL";

        // Zeyil No > 0 ve prim 0 ise iptal olabilir
        var zeyilNo = GetStringValue(row, "Zeyl No", "ZEYL NO", "ZEYİL NO");
        if (zeyilNo != "0" && !string.IsNullOrEmpty(zeyilNo) && brutPrim == 0)
            return "İPTAL";

        return "TAHAKKUK";
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue && !row.TanzimTarihi.HasValue)
            errors.Add("Tarih bilgisi geçersiz");

        // Zeyil kontrolü
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil)
        {
            if ((!row.BrutPrim.HasValue || row.BrutPrim == 0) &&
                (!row.NetPrim.HasValue || row.NetPrim == 0))
            {
                errors.Add("Prim bilgisi boş veya sıfır");
            }
        }

        return errors;
    }
}
