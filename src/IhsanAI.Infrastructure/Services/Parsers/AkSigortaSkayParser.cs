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
public class AkSigortaSkayParser : BaseExcelParser
{
    public override int SigortaSirketiId => 8;
    public override string SirketAdi => "AK Sigorta";
    public override string[] FileNamePatterns => new[] { "skay", "aksigorta", "ak_sigorta" };

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

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetPoliceNo(row);

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Header satırını atla
            if (policeNo.ToUpperInvariant().Contains("POLICE"))
                continue;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = null,  // SKAY'da yok
                ZeyilNo = GetZeyilNo(row),
                ZeyilTipKodu = null,  // SKAY'da yok
                Brans = GetBransFromTrf(row),
                PoliceTipi = GetPoliceTipi(row),

                // Tarihler - SKAY kolonları özel karakterler içeriyor
                TanzimTarihi = GetTanzimTarihi(row),
                BaslangicTarihi = GetBaslangicTarihi(row),
                BitisTarihi = GetBitisTarihi(row),
                ZeyilOnayTarihi = null,
                ZeyilBaslangicTarihi = null,

                // Primler
                BrutPrim = GetDecimalFromColumn(row, "TOPLAM"),
                NetPrim = GetDecimalFromColumn(row, "NET PRIM", "NETPRIM", "NET PRİM"),
                Komisyon = GetDecimalFromColumn(row, "KOM TUTARI", "KOMTUTARI", "KOMİSYON"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringFromColumn(row, "SIGORTALI", "SİGORTALI")?.Trim(),
                SigortaliSoyadi = null,  // SKAY'da birleşik

                // Araç Bilgileri
                Plaka = null,  // SKAY'da yok

                // Acente Bilgileri
                AcenteNo = null  // SKAY'da ayrı kolon yok
            };

            // Brüt prim yoksa toplam'ı kullan, o da yoksa net primi kullan
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
        var strValue = GetStringFromColumn(row, possibleColumns);
        if (string.IsNullOrEmpty(strValue)) return null;

        // Para formatını temizle
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

        // SKAY tarife kodları
        return trf.ToUpperInvariant() switch
        {
            var x when x.StartsWith("K11") => "TRAFİK",
            var x when x.StartsWith("ZDS") => "DASK",
            var x when x.StartsWith("K16") => "KASKO",
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
