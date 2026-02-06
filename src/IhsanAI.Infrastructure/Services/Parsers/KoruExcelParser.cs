using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Koru Sigorta Excel parser
///
/// Doğa Sigorta ile aynı kolon yapısına sahip,
/// tek fark Koru'da ekstra "Sepet Id" kolonu bulunması.
/// Tüm parsing mantığı DogaExcelParser'dan miras alınır.
/// Branş kod eşleştirmeleri Koru'ya özgüdür.
///
/// KOLONLAR (Doğa ile aynı + Sepet Id):
/// Col 1-33: Doğa ile aynı (Branş → Üretim Kanalı)
/// Col 34: Sepet Id (Koru'ya özgü)
/// Col 35: Poliçe Zeyil Key
///
/// BRANŞ KODU EŞLEŞTİRME (Koru Kodu → BransId):
/// 310 → 0 (Trafik), 344/355/356/359/370/375 → 1 (Kasko), 199 → 2 (DASK)
/// 253/260/261/282/289/293/297 → 3 (Ferdi Kaza), 354 → 5 (Konut)
/// 400/416/417/421/424 → 6 (Nakliyat), 298 → 8 (Seyahat Sağlık)
/// 152 → 9 (İşyeri), 325 → 12 (IMM), 200 → 24 (Hukuksal Koruma)
/// 450 → 25 (Tekne), 510/530/540 → 28 (Mühendislik)
/// 251/252/281 → 29 (Sorumluluk), 385 → 33 (Eğitim)
/// </summary>
public class KoruExcelParser : DogaExcelParser
{
    public override int SigortaSirketiId => 96;
    public override string SirketAdi => "Koru Sigorta";
    public override string[] FileNamePatterns => new[] { "koru" };

    // Doğa'nın signature'ları + "Sepet Id" (Koru'ya özgü kolon)
    protected override string[] SignatureColumns => new[]
    {
        "İpt/Kay", "Vade Başlangıç", "Sbm Havuz", "Sepet Id"
    };

    /// <summary>
    /// Koru branş kodu → BransId eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, int> KoruBransKoduMapping = new()
    {
        // Trafik
        { "310", 0 },

        // Kasko
        { "344", 1 },
        { "355", 1 },
        { "356", 1 },
        { "359", 1 },
        { "370", 1 },
        { "375", 1 },

        // DASK
        { "199", 2 },

        // Ferdi Kaza
        { "253", 3 },
        { "260", 3 },
        { "261", 3 },
        { "282", 3 },
        { "289", 3 },
        { "293", 3 },
        { "297", 3 },

        // Konut
        { "354", 5 },

        // Nakliyat
        { "400", 6 },
        { "416", 6 },
        { "417", 6 },
        { "421", 6 },
        { "424", 6 },

        // Seyahat Sağlık
        { "298", 8 },

        // İşyeri
        { "152", 9 },

        // IMM
        { "325", 12 },

        // Hukuksal Koruma
        { "200", 24 },

        // Tekne
        { "450", 25 },

        // Mühendislik
        { "510", 28 },
        { "530", 28 },
        { "540", 28 },

        // Sorumluluk
        { "251", 29 },
        { "252", 29 },
        { "281", 29 },

        // Eğitim
        { "385", 33 },
    };

    /// <summary>
    /// Koru branş kodu → Branş adı eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, string> KoruBransAdiMapping = new()
    {
        // Trafik
        { "310", "TRAFİK" },

        // Kasko
        { "344", "KASKO" },
        { "355", "KASKO" },
        { "356", "KASKO" },
        { "359", "KASKO" },
        { "370", "KASKO" },
        { "375", "KASKO" },

        // DASK
        { "199", "DASK" },

        // Ferdi Kaza
        { "253", "FERDİ KAZA" },
        { "260", "FERDİ KAZA" },
        { "261", "FERDİ KAZA" },
        { "282", "FERDİ KAZA" },
        { "289", "FERDİ KAZA" },
        { "293", "FERDİ KAZA" },
        { "297", "FERDİ KAZA" },

        // Konut
        { "354", "KONUT" },

        // Nakliyat
        { "400", "NAKLİYAT" },
        { "416", "NAKLİYAT" },
        { "417", "NAKLİYAT" },
        { "421", "NAKLİYAT" },
        { "424", "NAKLİYAT" },

        // Seyahat Sağlık
        { "298", "SEYAHAT SAĞLIK" },

        // İşyeri
        { "152", "İŞYERİ" },

        // IMM
        { "325", "IMM" },

        // Hukuksal Koruma
        { "200", "HUKUKSAL KORUMA" },

        // Tekne
        { "450", "TEKNE" },

        // Mühendislik
        { "510", "MÜHENDİSLİK" },
        { "530", "MÜHENDİSLİK" },
        { "540", "MÜHENDİSLİK" },

        // Sorumluluk
        { "251", "SORUMLULUK" },
        { "252", "SORUMLULUK" },
        { "281", "SORUMLULUK" },

        // Eğitim
        { "385", "EĞİTİM" },

        // UTTTS
        { "545", "UTTTS" },
    };

    protected override int? GetBransIdFromKod(string? bransKodu)
    {
        if (string.IsNullOrWhiteSpace(bransKodu))
            return null;

        var kod = bransKodu.Trim();
        return KoruBransKoduMapping.TryGetValue(kod, out var bransId) ? bransId : 255;
    }

    protected override string? GetBransAdiFromKod(string? bransKodu)
    {
        if (string.IsNullOrWhiteSpace(bransKodu))
            return null;

        var kod = bransKodu.Trim();
        return KoruBransAdiMapping.TryGetValue(kod, out var bransAdi) ? bransAdi : kod;
    }
}
