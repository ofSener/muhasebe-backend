using System.Globalization;
using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Neova Sigorta - Akay Acente Excel formatı parser
///
/// KOLONLAR:
/// Col 1:  KOD                  - Branş kodu (K23=Kasko, TR4=Trafik, DSK=DASK vb.)
/// Col 2:  ACENTE KOD
/// Col 3:  ACENTE ADI
/// Col 4:  TEMSİLCİ
/// Col 5:  G/T                  - Gerçek/Tüzel kişi
/// Col 6:  MÜŞTERİ TCKN / VKN   - TC Kimlik No veya Vergi Kimlik No
/// Col 7:  MÜŞTERİ AD/ÜNVAN     - Müşteri adı
/// Col 8:  MÜŞTERİ NO
/// Col 9:  MÜŞTERİ MBB NO
/// Col 10: POLİÇE NO
/// Col 11: ZEYİL NO
/// Col 12: ZEYİL TÜRÜ           - İptal/değişiklik türü
/// Col 13: ESKİ POLİÇE NO
/// Col 14: TANZİM TARİHİ
/// Col 15: BAŞLANGIÇ TARİHİ
/// Col 16: BİTİŞ TARİHİ
/// Col 17: NET PRİM
/// Col 18: NET VERGİ
/// Col 19: BRÜT PRİM
/// Col 20: KOMİSYON
/// Col 21: DAİNİ MÜRTEHİN       - Banka/Finans bilgisi
/// Col 22: P/T/R                - P=Poliçe, T=Teklif, R=Rapor
/// Col 23: FAALİYET KODU
/// Col 24-27: BİLGİ1-4          - Ek bilgiler (plaka vb.)
///
/// BRANŞ KODLARI:
/// K11, K18, K23    = Kasko
/// TR4, TR6         = Trafik
/// DSK              = DASK
/// TS1, TS3, TSS    = Ticari Sigorta
/// S47, S50, SG1    = Sağlık
/// Y02, Y06         = Yangın
/// YI1, YI2         = İşyeri
/// YK1, YK2         = Konut
/// </summary>
public class NeovaExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 93;
    public override string SirketAdi => "Neova Sigorta";
    public override string[] FileNamePatterns => new[] { "neova", "nva" };

    protected override string[] RequiredColumns => new[]
    {
        "POLİÇE NO", "BRÜT PRİM", "BAŞLANGIÇ TARİHİ"
    };

    // Bu parser'ı diğer Neova'dan ayıran benzersiz kolonlar
    protected override string[] SignatureColumns => new[]
    {
        "KOD", "G/T", "MÜŞTERİ TCKN / VKN", "P/T/R"
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // P/T/R kontrolü - sadece "P" (Poliçe) olanları al
            var ptr = GetStringValue(row, "P/T/R", "PTR")?.ToUpperInvariant();
            if (ptr != "P")
                continue;

            var policeNo = GetStringValue(row, "POLİÇE NO", "POLICE NO", "PoliceNo");

            // Toplam satırlarını atla
            if (string.IsNullOrWhiteSpace(policeNo) ||
                policeNo.Contains("TOPLAM") ||
                policeNo.Contains("GENEL"))
                continue;

            // TC/VKN ayrıştır
            var tcVkn = GetStringValue(row, "MÜŞTERİ TCKN / VKN", "MÜŞTERİ TCKN", "TCKN / VKN", "TCKN/VKN");
            string? tckn = null;
            string? vkn = null;

            if (!string.IsNullOrWhiteSpace(tcVkn))
            {
                tcVkn = tcVkn.Trim();
                if (tcVkn.Length == 11 && tcVkn.All(char.IsDigit))
                    tckn = tcVkn;
                else if (tcVkn.Length == 10 && tcVkn.All(char.IsDigit))
                    vkn = tcVkn;
                else if (tcVkn.All(char.IsDigit))
                {
                    // 11 haneli ise TC, değilse VKN olarak kabul et
                    if (tcVkn.Length == 11)
                        tckn = tcVkn;
                    else
                        vkn = tcVkn;
                }
            }

            // Plaka BİLGİ1-4 kolonlarından çıkar
            var plaka = ExtractPlaka(row);

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = null,  // Bu formatta yok
                ZeyilNo = GetStringValue(row, "ZEYİL NO", "ZEYIL NO", "ZeyilNo"),
                ZeyilTipKodu = GetStringValue(row, "ZEYİL TÜRÜ", "ZEYIL TURU", "ZeyilTuru"),
                Brans = GetBransFromKod(row),
                BransId = GetBransIdFromKod(row),
                PoliceTipi = GetPoliceTipi(row),

                // Tarihler (Neova Amerikan formatı kullanıyor: M/d/yyyy h:mm:ss tt)
                TanzimTarihi = GetNeovaDateValue(row, "TANZİM TARİHİ", "TANZIM TARIHI", "TanzimTarihi"),
                BaslangicTarihi = GetNeovaDateValue(row, "BAŞLANGIÇ TARİHİ", "BASLANGIC TARIHI", "BaslangicTarihi"),
                BitisTarihi = GetNeovaDateValue(row, "BİTİŞ TARİHİ", "BITIS TARIHI", "BitisTarihi"),
                ZeyilOnayTarihi = null,
                ZeyilBaslangicTarihi = null,

                // Primler
                BrutPrim = GetDecimalValue(row, "BRÜT PRİM", "BRUT PRIM", "BrutPrim"),
                NetPrim = GetDecimalValue(row, "NET PRİM", "NET PRIM", "NetPrim"),
                Komisyon = GetDecimalValue(row, "KOMİSYON", "KOMISYON", "Komisyon"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "MÜŞTERİ AD/ÜNVAN", "MUSTERI AD/UNVAN", "MusteriAdUnvan")?.Trim(),
                SigortaliSoyadi = null,  // Bu formatta birleşik
                Tckn = tckn,
                Vkn = vkn,

                // Araç/Diğer Bilgiler
                Plaka = plaka,
                Adres = null,

                // Acente Bilgileri
                AcenteNo = GetExactColumnValue(row, "ACENTE KOD", "ACENTE NO", "ACENTEKOD")
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
    /// KOD kolonundan branş adı çıkarır
    /// </summary>
    private string? GetBransFromKod(IDictionary<string, object?> row)
    {
        var kod = GetStringValue(row, "KOD")?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(kod)) return null;

        // Toplam satırlarını kontrol et
        if (kod.Contains("TOPLAM")) return null;

        return kod switch
        {
            // Kasko türleri
            "K11" or "K18" or "K23" => "KASKO",

            // Trafik türleri
            "TR4" or "TR6" => "TRAFİK",

            // DASK
            "DSK" => "DASK",

            // Ticari Sigortalar
            "TS1" or "TS3" or "TSS" => "TİCARİ SİGORTA",

            // Sağlık türleri
            "S47" or "S50" or "SG1" => "SAĞLIK",

            // Yangın türleri
            "Y02" or "Y06" => "YANGIN",

            // İşyeri türleri
            "YI1" or "YI2" => "İŞYERİ",

            // Konut türleri
            "YK1" or "YK2" => "KONUT",

            // Genel eşleştirmeler
            var x when x.StartsWith("K") => "KASKO",
            var x when x.StartsWith("TR") => "TRAFİK",
            var x when x.StartsWith("TS") => "TİCARİ SİGORTA",
            var x when x.StartsWith("S") => "SAĞLIK",
            var x when x.StartsWith("YI") => "İŞYERİ",
            var x when x.StartsWith("YK") => "KONUT",
            var x when x.StartsWith("Y") => "YANGIN",

            _ => kod  // Bilinmeyen kodları olduğu gibi döndür
        };
    }

    /// <summary>
    /// KOD kolonundan BransId çıkarır
    /// </summary>
    private int? GetBransIdFromKod(IDictionary<string, object?> row)
    {
        var kod = GetStringValue(row, "KOD")?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(kod)) return null;

        // Toplam satırlarını kontrol et
        if (kod.Contains("TOPLAM")) return null;

        return kod switch
        {
            // Kasko türleri -> ID: 1
            "K11" or "K18" or "K23" => 1,
            var x when x.StartsWith("K") => 1,

            // Trafik türleri -> ID: 0
            "TR4" or "TR6" => 0,
            var x when x.StartsWith("TR") => 0,

            // DASK -> ID: 2
            "DSK" => 2,

            // Ticari Sigortalar (Kasko gibi değerlendir) -> ID: 1
            "TS1" or "TS3" or "TSS" => 1,
            var x when x.StartsWith("TS") => 1,

            // Sağlık türleri -> ID: 7
            "S47" or "S50" or "SG1" => 7,
            var x when x.StartsWith("S") => 7,

            // Yangın türleri -> ID: 21
            "Y02" or "Y06" => 21,

            // İşyeri türleri -> ID: 9
            "YI1" or "YI2" => 9,
            var x when x.StartsWith("YI") => 9,

            // Konut türleri -> ID: 5
            "YK1" or "YK2" => 5,
            var x when x.StartsWith("YK") => 5,

            // Y ile başlayan diğerleri -> Yangın ID: 21
            var x when x.StartsWith("Y") => 21,

            _ => 255  // Bilinmeyen
        };
    }

    /// <summary>
    /// Poliçe tipini belirler (TAHAKKUK veya İPTAL)
    /// </summary>
    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        var zeyilTuru = GetStringValue(row, "ZEYİL TÜRÜ", "ZEYIL TURU");

        // Zeyil türünden iptal kontrolü
        if (!string.IsNullOrEmpty(zeyilTuru))
        {
            var upper = zeyilTuru.ToUpperInvariant();
            if (upper.Contains("İPTAL") ||
                upper.Contains("IPTAL") ||
                upper.Contains("FESİH") ||
                upper.Contains("FESIH") ||
                upper.Contains("TAM ZİYA") ||
                upper.Contains("TAM ZIYA"))
            {
                return "İPTAL";
            }
        }

        // Brüt prim negatifse iptal
        var brutPrim = GetDecimalValue(row, "BRÜT PRİM", "BRUT PRIM");
        if (brutPrim < 0)
            return "İPTAL";

        return "TAHAKKUK";
    }

    /// <summary>
    /// BİLGİ1-4 kolonlarından plaka çıkarır
    /// </summary>
    private string? ExtractPlaka(IDictionary<string, object?> row)
    {
        // BİLGİ kolonlarını kontrol et
        var bilgiColumns = new[] { "BİLGİ1", "BILGI1", "BİLGİ2", "BILGI2", "BİLGİ3", "BILGI3", "BİLGİ4", "BILGI4" };

        foreach (var col in bilgiColumns)
        {
            var value = GetStringValue(row, col);
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Plaka formatı kontrolü: 2 rakam + harf + rakamlar veya harf + rakam kombinasyonları
                var trimmed = value.Trim().ToUpperInvariant().Replace(" ", "");

                // Türk plaka formatları: 34ABC123, 34A1234, 34AB123, 34ABC12
                if (trimmed.Length >= 6 && trimmed.Length <= 9 &&
                    char.IsDigit(trimmed[0]) && char.IsDigit(trimmed[1]))
                {
                    // İlk 2 karakter rakam (il kodu)
                    bool hasLetters = trimmed.Skip(2).Any(char.IsLetter);
                    bool hasDigitsAfterLetters = trimmed.Skip(2).SkipWhile(char.IsLetter).Any(char.IsDigit);

                    if (hasLetters && hasDigitsAfterLetters)
                        return trimmed;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Neova'ya özel tarih parsing
    /// EPPlus tarihleri DateTime, double (OLE date) veya string olarak döndürebilir
    /// </summary>
    private static DateTime? GetNeovaDateValue(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        foreach (var col in possibleColumns)
        {
            var key = row.Keys.FirstOrDefault(k =>
                NormalizeColumnName(k).Contains(NormalizeColumnName(col)) ||
                NormalizeColumnName(col).Contains(NormalizeColumnName(k)));

            if (key != null && row.TryGetValue(key, out var value) && value != null)
            {
                // 1. DateTime olarak gelmiş olabilir
                if (value is DateTime dt)
                    return dt;

                // 2. Double olarak gelmiş olabilir (OLE Automation Date)
                // Excel tarihleri genellikle double olarak saklanır (örn: 45987.5 = 2025-12-18)
                if (value is double dblValue)
                {
                    try
                    {
                        // OLE Automation date: 1 Ocak 1900'den itibaren gün sayısı
                        // Geçerli aralık kontrolü (1900-2100 arası)
                        if (dblValue > 1 && dblValue < 100000)
                        {
                            return DateTime.FromOADate(dblValue);
                        }
                    }
                    catch { /* Geçersiz OLE date */ }
                }

                // 3. Diğer sayısal tipler
                if (value is float floatValue && floatValue > 1 && floatValue < 100000)
                {
                    try { return DateTime.FromOADate(floatValue); }
                    catch { }
                }

                if (value is decimal decValue && decValue > 1 && decValue < 100000)
                {
                    try { return DateTime.FromOADate((double)decValue); }
                    catch { }
                }

                // 4. String olarak parse et
                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(strValue)) continue;

                // Tarih formatları
                var dateFormats = new[]
                {
                    "M/d/yyyy h:mm:ss tt",      // 12/18/2025 12:00:00 AM
                    "M/d/yyyy H:mm:ss",         // 12/18/2025 12:00:00
                    "MM/dd/yyyy h:mm:ss tt",
                    "MM/dd/yyyy HH:mm:ss",
                    "M/d/yyyy",
                    "MM/dd/yyyy",
                    "yyyy-MM-dd",
                    "yyyy-MM-dd HH:mm:ss",
                    "dd.MM.yyyy",
                    "dd/MM/yyyy",
                    "dd.MM.yyyy HH:mm:ss",
                    "dd/MM/yyyy HH:mm:ss",
                    "d.M.yyyy",
                    "d/M/yyyy",
                };

                if (DateTime.TryParseExact(strValue, dateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                    return parsed;

                // en-US kültürü ile dene
                if (DateTime.TryParse(strValue, new CultureInfo("en-US"), DateTimeStyles.None, out parsed))
                    return parsed;

                // Türkçe kültür ile dene
                if (DateTime.TryParse(strValue, new CultureInfo("tr-TR"), DateTimeStyles.None, out parsed))
                    return parsed;

                // Genel parse
                if (DateTime.TryParse(strValue, out parsed))
                    return parsed;
            }
        }
        return null;
    }

    /// <summary>
    /// Tam eşleşme ile kolon değeri alır (Contains yerine exact match)
    /// "KOD" ile "ACENTE KOD" karışmasını önler
    /// </summary>
    private static string? GetExactColumnValue(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        foreach (var col in possibleColumns)
        {
            var normalizedCol = NormalizeColumnName(col);

            // Önce tam eşleşme dene
            var key = row.Keys.FirstOrDefault(k =>
                NormalizeColumnName(k) == normalizedCol);

            // Tam eşleşme yoksa, kolon adı ile başlayanı dene
            if (key == null)
            {
                key = row.Keys.FirstOrDefault(k =>
                    NormalizeColumnName(k).StartsWith(normalizedCol));
            }

            if (key != null && row.TryGetValue(key, out var value) && value != null)
            {
                var strValue = value.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(strValue))
                    return strValue;
            }
        }
        return null;
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue)
            errors.Add("Başlangıç Tarihi geçersiz");

        // Zeyil kontrolü - zeyillerde 0 veya negatif prim olabilir
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil && (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            errors.Add("Brüt Prim boş veya sıfır");

        return errors;
    }
}
