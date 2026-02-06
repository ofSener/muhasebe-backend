using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Corpus Sigorta Excel parser
///
/// Excel Yapısı:
/// - Row 1: Headers
/// - Row 2: Bazen boş/döviz satırı (atlanır)
/// - Row 3+: Veriler
/// - Son satırlar: Özet (TARİFE TOPLAM, DOVİZ TOPLAM, ACENTE TOPLAM, GENEL TOPLAM)
///
/// KOLONLAR (33 kolon):
/// Col 1: BÖLGE
/// Col 2: ESKİ MUST KOD (Acente kodu)
/// Col 3: ACENTE ÜNVAN
/// Col 4: ÜRÜN NO (Branş kodu: TKP, OTO, YNG, vb.)
/// Col 5: POLİÇE NO
/// Col 6: ZEYİL NO
/// Col 7: YENİLEME NO
/// Col 8: ACENTA POL NO
/// Col 9: P / T
/// Col 10: ONAY TANZİM TARİHİ (OLE Automation date)
/// Col 11: BAŞLAMA TAR. (OLE Automation date)
/// Col 12: BİTİŞ TAR. (OLE Automation date)
/// Col 13: ONAYLAYAN KULLANICI
/// Col 14: SİGORTALI ÜNVANI
/// Col 15: SİGORTALI NO
/// Col 16: ÖZEL / TÜZEL
/// Col 17: DÖVİZ
/// Col 18: NET PRİM
/// Col 19: BRÜT PRİM
/// Col 20: KOMİSYON
/// Col 21: GV
/// Col 22: YSV
/// Col 23: GF
/// Col 24: TF
/// Col 25: ORTAKLIK_BEDELI
/// Col 26: SIGORTA_ETTIREN_NO
/// Col 27: YERLİ / YABANCI
/// Col 28: SIGORTALI_ETTIREN_UNVANI
/// Col 29: YERLİ / YABANCI (tekrar)
/// Col 30: ÜRÜN SEGMENT
/// Col 31: ABONMAN NO
/// Col 32: İPTAL / YÜRÜRLÜK
/// Col 33: PLAKA
/// </summary>
public class CorpusExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 19;
    public override string SirketAdi => "Corpus Sigorta";
    public override string[] FileNamePatterns => new[] { "corpus" };

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Brüt Prim"
    };

    // Corpus'a özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "ACENTA POL NO", "ORTAKLIK_BEDELI", "SİGORTALI ÜNVANI"
    };

    /// <summary>
    /// Corpus ÜRÜN NO → BransId eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, int> CorpusBransKoduMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // Trafik (0)
        { "TRT", 0 }, { "TR1", 0 }, { "TR4", 0 }, { "TR5", 0 }, { "TR6", 0 }, { "TTT", 0 },

        // Kasko (1)
        { "FKH", 1 }, { "FKT", 1 }, { "DMR", 1 },
        { "KSA", 1 }, { "KSI", 1 }, { "KSM", 1 },
        { "KS1", 1 }, { "KS2", 1 }, { "KS3", 1 }, { "KS4", 1 }, { "KS5", 1 },
        { "PFH", 1 }, { "PFK", 1 }, { "PFM", 1 }, { "PFO", 1 }, { "PFQ", 1 }, { "PFS", 1 },
        { "PKM", 1 }, { "PKN", 1 }, { "PKP", 1 }, { "PKR", 1 }, { "PKY", 1 },
        { "997", 1 }, { "100", 1 }, { "101", 1 },

        // DASK (2)
        { "DSG", 2 }, { "DSK", 2 }, { "DTK", 2 },

        // Ferdi Kaza (3)
        { "FOS", 3 }, { "FRD", 3 }, { "FRK", 3 },
        { "GBF", 3 }, { "GFC", 3 }, { "GFK", 3 }, { "GMF", 3 },
        { "FFF", 3 }, { "FKB", 3 }, { "FKG", 3 }, { "FKK", 3 }, { "FKP", 3 }, { "FKZ", 3 },
        { "FK1", 3 }, { "FK2", 3 },
        { "ERK", 3 }, { "BFK", 3 }, { "KFK", 3 }, { "KFS", 3 },
        { "SAF", 3 }, { "SFK", 3 }, { "KFG", 3 },
        { "252", 3 },

        // Koltuk (4)
        { "TAF", 4 },

        // Konut (5)
        { "DNM", 5 }, { "BTM", 5 }, { "CAM", 5 }, { "KPS", 5 },
        { "211", 5 }, { "250", 5 }, { "251", 5 },
        { "YK1", 5 }, { "YK2", 5 }, { "YK3", 5 },

        // Nakliyat (6)
        { "EMN", 6 }, { "EMT", 6 }, { "ENA", 6 }, { "EN2", 6 }, { "EN3", 6 },
        { "ABN", 6 }, { "NBA", 6 }, { "KYN", 6 }, { "KIY", 6 },

        // Sağlık (7)
        { "FVS", 7 }, { "DSS", 7 }, { "ASA", 7 }, { "ASC", 7 }, { "SGL", 7 },

        // Seyahat Sağlık (8)
        { "GSS", 8 }, { "BSS", 8 }, { "CSS", 8 }, { "SEY", 8 },
        { "SSS", 8 }, { "SYH", 8 }, { "SYS", 8 }, { "SYY", 8 }, { "SY1", 8 },
        { "YIS", 8 },

        // İşyeri (9)
        { "IMS", 9 }, { "INM", 9 }, { "IPS", 9 }, { "ISV", 9 },
        { "SPI", 9 }, { "204", 9 }, { "YAP", 9 }, { "YI1", 9 },

        // IMM (12)
        { "IHM", 12 }, { "IHS", 12 }, { "IMM", 12 },

        // Tarım (20)
        { "810", 20 }, { "820", 20 }, { "830", 20 }, { "840", 20 }, { "850", 20 },
        { "860", 20 }, { "870", 20 }, { "890", 20 }, { "900", 20 }, { "910", 20 }, { "920", 20 },

        // Yangın (21)
        { "EKE", 21 }, { "EKK", 21 }, { "EKO", 21 }, { "EKP", 21 }, { "EK1", 21 },
        { "KTY", 21 }, { "KSY", 21 }, { "KAS", 21 }, { "144", 21 },
        { "YAS", 21 }, { "YI2", 21 }, { "YKK", 21 }, { "YK", 21 }, { "YMM", 21 }, { "YMS", 21 },

        // Hukuksal Koruma (24)
        { "HUK", 24 },

        // Tekne (25)
        { "GTS", 25 }, { "DTZ", 25 }, { "DYZ", 25 }, { "DZM", 25 },
        { "NTS", 25 }, { "NYS", 25 }, { "NY1", 25 }, { "SBR", 25 },

        // Hayat (26)
        { "HYT", 26 },

        // Mühendislik (28)
        { "EAR", 28 }, { "ECS", 28 }, { "CAR", 28 }, { "INS", 28 }, { "SIN", 28 },
        { "MKK", 28 }, { "MKS", 28 }, { "MKU", 28 }, { "MMM", 28 }, { "MMS", 28 },
        { "MON", 28 }, { "MSL", 28 }, { "MTA", 28 }, { "MTP", 28 }, { "MUB", 28 },
        { "MBD", 28 }, { "MBK", 28 }, { "MDG", 28 },

        // Sorumluluk (29)
        { "EXC", 29 }, { "ASM", 29 }, { "ASN", 29 }, { "ASS", 29 }, { "AUS", 29 },
        { "LIS", 29 }, { "SMM", 29 }, { "S15", 29 },
        { "TAB", 29 }, { "TSS", 29 }, { "TSY", 29 }, { "TYU", 29 },
        { "TPG", 29 }, { "TPP", 29 },

        // Eğitim (33)
        { "CEG", 33 },
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // Poliçe No
            var policeNo = GetStringValue(row, "POLİÇE NO", "Poliçe No", "POLICE NO")?.Trim();
            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Poliçe No sayısal olmalı - özet satırlarını atla
            if (!long.TryParse(policeNo, out _))
                continue;

            // Zeyil
            var zeyilNo = GetStringValue(row, "ZEYİL NO", "Zeyil No")?.Trim();
            var isZeyil = IsZeyilPolicy(zeyilNo);

            // Branş - ÜRÜN NO (kod) ve ÜRÜN SEGMENT (açıklama)
            var urunNo = GetStringValue(row, "ÜRÜN NO", "Ürün No", "URUN NO")?.Trim();
            var urunSegment = GetStringValue(row, "ÜRÜN SEGMENT", "Ürün Segment", "URUN SEGMENT")?.Trim();
            var bransAdi = !string.IsNullOrWhiteSpace(urunSegment) ? urunSegment : urunNo;

            // Önce ÜRÜN NO kodundan eşleştir, bulamazsa metin bazlı fallback
            int? bransId = null;
            if (!string.IsNullOrWhiteSpace(urunNo) && CorpusBransKoduMapping.TryGetValue(urunNo, out var mappedId))
            {
                bransId = mappedId;
            }
            else
            {
                bransId = DetectBransIdFromUrunAdi(bransAdi, isZeyil);
            }

            // Tarihler (OLE Automation date olarak geliyor)
            var tanzimTarihi = GetDateValue(row, "ONAY TANZİM TARİHİ", "Onay Tanzim Tarihi", "TANZIM TARIHI");
            var baslangicTarihi = GetDateValue(row, "BAŞLAMA TAR.", "BAŞLAMA TAR", "Başlama Tar", "BASLAMA TAR");
            var bitisTarihi = GetDateValue(row, "BİTİŞ TAR.", "BİTİŞ TAR", "Bitiş Tar", "BITIS TAR");

            // İptal durumu
            var iptalDurumu = GetStringValue(row, "İPTAL / YÜRÜRLÜK", "IPTAL / YÜRÜRLÜK",
                "İPTAL/YÜRÜRLÜK", "IPTAL/YÜRÜRLÜK")?.Trim().ToUpperInvariant();
            var isIptal = iptalDurumu != null && iptalDurumu.Contains("İPTAL") ||
                          iptalDurumu != null && iptalDurumu.Contains("IPTAL");
            var policeTipi = isIptal ? "İPTAL" : "TAHAKKUK";

            // Primler
            var brutPrim = GetDecimalValue(row, "BRÜT PRİM", "Brüt Prim", "BRUT PRIM");
            var netPrim = GetDecimalValue(row, "NET PRİM", "Net Prim", "NET PRIM");
            var komisyon = GetDecimalValue(row, "KOMİSYON", "Komisyon", "KOMISYON");

            // Negatif primler zaten iptal göstergesi
            if (!isIptal && brutPrim.HasValue && brutPrim < 0)
            {
                isIptal = true;
                policeTipi = "İPTAL";
            }

            // Müşteri bilgileri
            var sigortaliAdi = GetStringValue(row, "SİGORTALI ÜNVANI", "Sigortalı Ünvanı",
                "SIGORTALI UNVANI")?.Trim();

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "YENİLEME NO", "Yenileme No")?.Trim(),
                ZeyilNo = zeyilNo,
                Brans = bransAdi?.ToUpperInvariant(),
                BransId = bransId,
                PoliceTipi = policeTipi,

                // Tarihler
                TanzimTarihi = tanzimTarihi,
                BaslangicTarihi = baslangicTarihi,
                BitisTarihi = bitisTarihi,
                ZeyilOnayTarihi = isZeyil ? tanzimTarihi : null,
                ZeyilBaslangicTarihi = isZeyil ? baslangicTarihi : null,

                // Primler
                BrutPrim = brutPrim,
                NetPrim = netPrim,
                Komisyon = komisyon,

                // Müşteri Bilgileri
                SigortaliAdi = sigortaliAdi,

                // Araç/Acente Bilgileri
                Plaka = GetStringValue(row, "PLAKA", "Plaka")?.Trim(),
                AcenteNo = GetStringValue(row, "ESKİ MUST KOD", "Eski Must Kod",
                    "ESKI MUST KOD")?.Trim()
            };

            // Validasyon
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

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.TanzimTarihi.HasValue && !row.BaslangicTarihi.HasValue)
            errors.Add("Tarih bilgisi geçersiz");

        // Zeyil değilse prim kontrolü
        var isZeyil = !string.IsNullOrWhiteSpace(row.ZeyilNo) &&
                      int.TryParse(row.ZeyilNo, out var zeyilNum) &&
                      zeyilNum > 0;

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
