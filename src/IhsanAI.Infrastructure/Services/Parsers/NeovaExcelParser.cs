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
/// BRANŞ KODLARI → BransId:
/// K11              = Kasko (1)
/// K18              = IMM (12)
/// K23              = Kasko (1)
/// TR4, TR6         = Trafik (0)
/// DSK              = DASK (2)
/// TS1, TS3         = Tarım (20)
/// TSS              = Tamamlayıcı Sağlık (16)
/// S47              = Ferdi Kaza (3)
/// S50              = Tehlikeli Madde (31)
/// SG1              = Seyahat Sağlık (8)
/// Y02, Y06         = Yangın (21)
/// YI1, YI2         = Yangın (21)
/// YK1              = Konut (5)
/// YK2              = Yangın (21)
/// </summary>
public class NeovaExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 93;
    public override string SirketAdi => "Neova Sigorta";
    public override string[] FileNamePatterns => new[] { "neova", "nva" };

    // Neova Excel'lerinde header satırı genellikle 8. satırda
    // (İlk 7 satır rapor başlığı, tarih aralığı vb. bilgiler içerir)
    public override int? HeaderRowIndex => 8;

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

            // POLİÇE NO - exact match kullan (ESKİ POLİÇE NO ile karışmaması için)
            var policeNo = GetExactColumnValue(row, "POLİÇE NO", "POLICE NO", "PoliceNo");

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
            // Kasko
            "K11" or "K23" => "KASKO",

            // IMM
            "K18" => "IMM",

            // Trafik
            "TR4" or "TR6" => "TRAFİK",

            // DASK
            "DSK" => "DASK",

            // Tarım
            "TS1" or "TS3" => "TARIM",

            // Tamamlayıcı Sağlık
            "TSS" => "TAMAMLAYICI SAĞLIK",

            // Ferdi Kaza
            "S47" => "FERDİ KAZA",

            // Tehlikeli Madde
            "S50" => "TEHLİKELİ MADDE",

            // Seyahat Sağlık
            "SG1" => "SEYAHAT SAĞLIK",

            // Yangın
            "Y02" or "Y06" or "YI1" or "YI2" or "YK2" => "YANGIN",

            // Konut
            "YK1" => "KONUT",

            // Genel eşleştirmeler (bilinmeyen kodlar için)
            var x when x.StartsWith("K") => "KASKO",
            var x when x.StartsWith("TR") => "TRAFİK",
            var x when x.StartsWith("TS") => "TARIM",
            var x when x.StartsWith("SG") => "SEYAHAT SAĞLIK",
            var x when x.StartsWith("S") => "FERDİ KAZA",
            var x when x.StartsWith("YI") => "YANGIN",
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
            // Kasko -> ID: 1
            "K11" or "K23" => 1,

            // IMM -> ID: 12
            "K18" => 12,

            // Trafik -> ID: 0
            "TR4" or "TR6" => 0,

            // DASK -> ID: 2
            "DSK" => 2,

            // Tarım -> ID: 20
            "TS1" or "TS3" => 20,

            // Tamamlayıcı Sağlık -> ID: 16
            "TSS" => 16,

            // Ferdi Kaza -> ID: 3
            "S47" => 3,

            // Tehlikeli Madde -> ID: 31
            "S50" => 31,

            // Seyahat Sağlık -> ID: 8
            "SG1" => 8,

            // Yangın -> ID: 21
            "Y02" or "Y06" or "YI1" or "YI2" or "YK2" => 21,

            // Konut -> ID: 5
            "YK1" => 5,

            // Genel eşleştirmeler (bilinmeyen kodlar için)
            var x when x.StartsWith("K") => 1,      // Kasko
            var x when x.StartsWith("TR") => 0,     // Trafik
            var x when x.StartsWith("TS") => 20,    // Tarım
            var x when x.StartsWith("SG") => 8,     // Seyahat Sağlık
            var x when x.StartsWith("S") => 3,      // Ferdi Kaza
            var x when x.StartsWith("YI") => 21,    // Yangın
            var x when x.StartsWith("YK") => 5,     // Konut
            var x when x.StartsWith("Y") => 21,     // Yangın

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

        // İptal veya Zeyil satırlarında 0/negatif prim kabul edilir
        var isIptal = row.PoliceTipi == "İPTAL";
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);

        if (!isIptal && !isZeyil && (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            errors.Add("Brüt Prim boş veya sıfır");

        return errors;
    }
}
