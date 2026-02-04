using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// AK Sigorta SKAY Excel parser
/// Dosya formatı: SKAY_*.xlsx
/// Header 8. satırda (1-indexed)
///
/// MAPPING:
/// *PoliceNo       = "POLICE NO"             [OK]
/// *YenilemeNo     = YOK                     [NO]
/// *ZeyilNo        = "ZEYL"                  [OK]
/// *ZeyilTipKodu   = YOK                     [NO]
/// *Brans          = "TRF" (Tarife kodu)     [WARN]
/// *PoliceTipi     = "TAH TIP"               [WARN]
/// *TanzimTarihi   = "TANZ. T"               [OK]
/// *BaslangicTarihi= "BAS/YUK T"             [OK]
/// *BitisTarihi    = "BITIS T"               [OK]
/// *ZeyilOnayTarihi= YOK                     [NO]
/// *ZeyilBaslangicTarihi = YOK               [NO]
/// *BrutPrim       = "TOPLAM"                [OK]
/// *NetPrim        = "NET PRIM"              [OK]
/// *Komisyon       = "KOM TUTARI"            [OK]
/// *SigortaliAdi   = "SIGORTALI"             [OK]
/// *SigortaliSoyadi= YOK                     [NO]
/// *Plaka          = YOK                     [NO]
/// *AcenteNo       = YOK (dosyada mevcut)    [NO]
/// </summary>
public class AkExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 4;
    public override string SirketAdi => "AK Sigorta";
    public override string[] FileNamePatterns => new[] { "aksigorta", "ak_" };

    /// <summary>
    /// SKAY formatında header 8. satırda (1-indexed)
    /// Satır 1-7: Şirket adı, tarih aralığı, başlıklar vs.
    /// Satır 8: Kolonlar
    /// </summary>
    public override int? HeaderRowIndex => 8;

    protected override string[] RequiredColumns => new[]
    {
        "POLICE NO", "PRIM", "TANZ"
    };

    // SKAY'a özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "ZEYL", "BAS/YUK", "SIGORTALI"  // Bu kombinasyon sadece SKAY'da var
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;
        bool isInIptalSection = false;  // TAHAKKUK/IPTAL bölüm takibi

        foreach (var row in rows)
        {
            rowNumber++;

            // Satır içeriğini kontrol et - POLICE NO kolonunu veya tüm değerleri tara
            var rowMarker = DetectRowMarker(row);

            // TAHAKKUK/IPTAL section marker kontrolü
            if (rowMarker == RowMarkerType.IptalSectionStart)
            {
                isInIptalSection = true;
                continue;
            }
            if (rowMarker == RowMarkerType.TahakkukSectionStart)
            {
                isInIptalSection = false;
                continue;
            }
            // Toplam satırlarını atla
            if (rowMarker == RowMarkerType.TotalRow)
            {
                continue;
            }
            // Metadata satırlarını atla
            if (rowMarker == RowMarkerType.MetadataRow)
            {
                continue;
            }

            var policeNo = GetPoliceNo(row);

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Header satırını atla
            if (policeNo.ToUpperInvariant().Contains("POLICE"))
                continue;

            // Primleri al
            var brutPrim = GetDecimalFromColumn(row, "TOPLAM");
            var netPrim = GetDecimalFromColumn(row, "NET PRIM", "NETPRIM", "NET PRİM");
            var komisyon = GetDecimalFromColumn(row, "KOM TUTARI", "KOMTUTARI", "KOMİSYON");

            // İptal bölümündeyse primleri negatife çevir
            if (isInIptalSection)
            {
                if (brutPrim.HasValue && brutPrim > 0) brutPrim = -brutPrim;
                if (netPrim.HasValue && netPrim > 0) netPrim = -netPrim;
                if (komisyon.HasValue && komisyon > 0) komisyon = -komisyon;
            }

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = null,
                ZeyilNo = GetZeyilNo(row),
                ZeyilTipKodu = null,
                Brans = GetBransFromTrf(row),
                PoliceTipi = isInIptalSection ? "İPTAL" : GetPoliceTipi(row),

                // Tarihler
                TanzimTarihi = GetTanzimTarihi(row),
                BaslangicTarihi = GetBaslangicTarihi(row),
                BitisTarihi = GetBitisTarihi(row),
                ZeyilOnayTarihi = null,
                ZeyilBaslangicTarihi = null,

                // Primler
                BrutPrim = brutPrim,
                NetPrim = netPrim,
                Komisyon = komisyon,

                // Müşteri Bilgileri
                SigortaliAdi = GetStringFromColumn(row, "SIGORTALI", "SİGORTALI")?.Trim(),
                SigortaliSoyadi = null,

                // Araç Bilgileri
                Plaka = null,

                // Acente Bilgileri
                AcenteNo = null
            };

            // Brüt prim yoksa net primi kullan
            if (!dto.BrutPrim.HasValue || dto.BrutPrim == 0)
            {
                dto = dto with { BrutPrim = dto.NetPrim };
            }

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
    /// Satır tipi - section marker, metadata, toplam satırı veya veri satırı
    /// </summary>
    private enum RowMarkerType
    {
        DataRow,
        IptalSectionStart,
        TahakkukSectionStart,
        TotalRow,
        MetadataRow
    }

    /// <summary>
    /// Satırın tipini tespit eder - tüm kolonları tarar
    /// </summary>
    private RowMarkerType DetectRowMarker(IDictionary<string, object?> row)
    {
        // Tüm kolon değerlerini kontrol et
        foreach (var kvp in row)
        {
            var value = kvp.Value?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(value)) continue;

            var valueUpper = value.ToUpperInvariant();

            // TAHAKKUK/IPTAL section markers
            // Format: "TAHAKKUK/IPTAL : Iptal" veya "TAHAKKUK/IPTAL : Tahakkuk"
            if (valueUpper.Contains("TAHAKKUK/IPTAL") || valueUpper.Contains("TAHAKKUK/İPTAL"))
            {
                // Colon'dan sonraki kısmı kontrol et
                var colonIdx = value.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var afterColon = value.Substring(colonIdx + 1).Trim().ToUpperInvariant();
                    if (afterColon.Contains("IPTAL") || afterColon.Contains("İPTAL"))
                    {
                        return RowMarkerType.IptalSectionStart;
                    }
                }
                // Colon yoksa veya colon sonrası iptal değilse -> tahakkuk
                return RowMarkerType.TahakkukSectionStart;
            }

            // Toplam satırları (TAHAKKUK TOPLAM, İPTAL TOPLAM vb.)
            if (valueUpper.Contains("TOPLAM") && !valueUpper.Contains("POLICE"))
            {
                return RowMarkerType.TotalRow;
            }

            // Metadata patterns
            var metadataPatterns = new[]
            {
                "AK SİGORTA", "AK SIGORTA",
                "KAYIT DEFTER", "KAYIT DEFTERİ",
                "PARA BİRİMİ", "PARA BIRIMI",
                "ACENTA", "ACENTE",
                "TARİHLERİ ARASI", "TARIHLERI ARASI"
            };

            if (metadataPatterns.Any(p => valueUpper.Contains(p)))
            {
                return RowMarkerType.MetadataRow;
            }
        }

        return RowMarkerType.DataRow;
    }

    private string? GetPoliceNo(IDictionary<string, object?> row)
    {
        // POLICE NO kolonu
        return GetStringFromColumn(row, "POLICE NO", "POLICENO", "POLİCE NO", "POLİÇE NO");
    }

    private string? GetZeyilNo(IDictionary<string, object?> row)
    {
        return GetStringFromColumn(row, "ZEYL", "ZEYİL", "ZEYIL");
    }

    private DateTime? GetTanzimTarihi(IDictionary<string, object?> row)
    {
        // "TANZ.\nT" veya benzeri kolonlar
        return GetDateFromColumn(row, "TANZ", "TANZIM", "TANZİM");
    }

    private DateTime? GetBaslangicTarihi(IDictionary<string, object?> row)
    {
        // "BAS/YUK\nT" veya benzeri kolonlar
        return GetDateFromColumn(row, "BAS", "BASLANGIC", "BAŞLANGIÇ", "YUK");
    }

    private DateTime? GetBitisTarihi(IDictionary<string, object?> row)
    {
        // "BITIS\nT" veya benzeri kolonlar
        return GetDateFromColumn(row, "BITIS", "BİTİŞ");
    }

    /// <summary>
    /// Kolon isimlerinde özel karakterler (\n) olabileceği için özel arama
    /// </summary>
    private string? GetStringFromColumn(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        foreach (var col in possibleColumns)
        {
            var key = row.Keys.FirstOrDefault(k =>
            {
                var normalizedKey = NormalizeSkayColumnName(k);
                var normalizedCol = NormalizeSkayColumnName(col);
                return normalizedKey.Contains(normalizedCol) || normalizedCol.Contains(normalizedKey);
            });

            if (key != null && row.TryGetValue(key, out var value) && value != null)
            {
                var strValue = value.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(strValue))
                    return strValue;
            }
        }
        return null;
    }

    private decimal? GetDecimalFromColumn(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        foreach (var col in possibleColumns)
        {
            var key = row.Keys.FirstOrDefault(k =>
            {
                var normalizedKey = NormalizeSkayColumnName(k);
                var normalizedCol = NormalizeSkayColumnName(col);
                return normalizedKey.Contains(normalizedCol) || normalizedCol.Contains(normalizedKey);
            });

            if (key != null && row.TryGetValue(key, out var value) && value != null)
            {
                // Numeric tipleri doğrudan dönüştür (Excel'den Double olarak gelir)
                if (value is decimal d) return d;
                if (value is double dbl) return (decimal)dbl;
                if (value is float f) return (decimal)f;
                if (value is int i) return i;
                if (value is long l) return l;

                // String ise parse et
                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrEmpty(strValue)) continue;

                // Türkçe para formatını temizle (sadece string değerler için)
                // Örn: "1.621,10" -> "1621.10"
                strValue = strValue
                    .Replace("₺", "")
                    .Replace("TL", "")
                    .Replace(" ", "")
                    .Replace(".", "")  // Binlik ayırıcı
                    .Replace(",", ".")  // Ondalık ayırıcı
                    .Trim();

                if (decimal.TryParse(strValue, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var result))
                    return result;
            }
        }
        return null;
    }

    private DateTime? GetDateFromColumn(IDictionary<string, object?> row, params string[] possibleColumns)
    {
        foreach (var col in possibleColumns)
        {
            var key = row.Keys.FirstOrDefault(k =>
            {
                var normalizedKey = NormalizeSkayColumnName(k);
                var normalizedCol = NormalizeSkayColumnName(col);
                return normalizedKey.Contains(normalizedCol) || normalizedCol.Contains(normalizedKey);
            });

            if (key != null && row.TryGetValue(key, out var value) && value != null)
            {
                // DateTime olarak gelmiş olabilir
                if (value is DateTime dt)
                    return dt;

                var strValue = value.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(strValue)) continue;

                // SKAY formatı: dd/MM/yy veya dd/MM/yyyy
                var formats = new[]
                {
                    "dd/MM/yy",
                    "dd/MM/yyyy",
                    "dd.MM.yy",
                    "dd.MM.yyyy",
                    "d/M/yy",
                    "d/M/yyyy"
                };

                if (DateTime.TryParseExact(strValue, formats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed))
                    return parsed;

                // Genel parse
                if (DateTime.TryParse(strValue, new System.Globalization.CultureInfo("tr-TR"),
                    System.Globalization.DateTimeStyles.None, out parsed))
                    return parsed;
            }
        }
        return null;
    }

    private static string NormalizeSkayColumnName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        // SKAY kolonlarında \n ve özel karakterler var
        return name
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace(" ", "")
            .Replace(".", "")
            .Replace("/", "")
            .ToUpperInvariant()
            .Replace("İ", "I")
            .Replace("Ş", "S")
            .Replace("Ğ", "G")
            .Replace("Ü", "U")
            .Replace("Ö", "O")
            .Replace("Ç", "C");
    }

    private string? GetBransFromTrf(IDictionary<string, object?> row)
    {
        var trf = GetStringFromColumn(row, "TRF", "TARIFE");

        if (string.IsNullOrEmpty(trf)) return null;

        // -1 suffix'i temizle (örn: T41-1 -> T41)
        var cleanTrf = trf.ToUpperInvariant().Split('-')[0].Trim();

        // AK Sigorta tarife kodları
        return cleanTrf switch
        {
            "T41" => "TRAFİK",
            "K11" => "KASKO",
            "ZDS" => "DASK",
            "Y17" => "KONUT",
            var x when x.StartsWith("T4") => "TRAFİK",
            var x when x.StartsWith("K1") => "KASKO",
            var x when x.StartsWith("Y") => "KONUT",
            _ => trf
        };
    }

    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        // TAH\nTIP kolonu: SPS=Satış, İPT=İptal vb.
        var tahTip = GetStringFromColumn(row, "TAH", "TIP", "TAHTIP");

        if (!string.IsNullOrEmpty(tahTip) &&
            (tahTip.ToUpperInvariant().Contains("İPT") ||
             tahTip.ToUpperInvariant().Contains("IPT")))
        {
            return "İPTAL";
        }

        // Net prim negatifse iptal
        var netPrim = GetDecimalFromColumn(row, "NET PRIM", "NETPRIM");
        if (netPrim < 0)
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

        // Zeyil kontrolü - robust parsing ile (zeyillerde 0 veya negatif prim olabilir)
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);
        if (!isZeyil)
        {
            // Net prim veya brüt prim olmalı
            if ((!row.NetPrim.HasValue || row.NetPrim == 0) &&
                (!row.BrutPrim.HasValue || row.BrutPrim == 0))
            {
                errors.Add("Prim bilgisi boş");
            }
        }
        // Zeyil için prim 0 veya negatif olabilir

        return errors;
    }
}
