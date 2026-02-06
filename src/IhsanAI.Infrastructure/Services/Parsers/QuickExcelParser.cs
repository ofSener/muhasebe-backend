using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Quick Sigorta Excel parser
/// Ana Sheet: PoliceListesi
/// Ek Sheet: Sigortalilar (TC, Ad, Soyad bilgileri için PoliceNo+ZeyilNo ile join edilir)
///
/// MAPPING (PoliceListesi sayfasından):
/// *PoliceNo       = "PoliceNo"             [OK]
/// *YenilemeNo     = "YenilemeNo"           [OK]
/// *ZeyilNo        = "ZeyilNo"              [OK]
/// *ZeyilTipKodu   = "ZeyilTipKodu"         [OK]
/// *Brans          = "UrunAd"               [OK]
/// *PoliceTipi     = "ZeyilAd" (İPTAL içeriyorsa)  [OK]
/// *TanzimTarihi   = "TanzimTarihi"         [OK]
/// *BaslangicTarihi= "BaslamaTarihi"        [OK]
/// *BitisTarihi    = "BitisTarihi"          [OK]
/// *ZeyilOnayTarihi= YOK                    [NO]
/// *ZeyilBaslangicTarihi = YOK              [NO]
/// *BrutPrim       = "BrutPrimTL"           [OK]
/// *NetPrim        = "NetPrimTL"            [OK]
/// *Komisyon       = "AcenteKomisyonTL"     [OK]
/// *AcenteNo       = "AcenteNo"             [OK]
/// *Plaka          = "Plaka"                [OK]
///
/// MAPPING (Sigortalilar sayfasından - PoliceNo+ZeyilNo ile eşleşir):
/// *SigortaliAdi   = "Ad"                   [OK]
/// *SigortaliSoyadi= "Soyad"                [OK]
/// *Tckn           = "Tckn"                 [OK]
/// *Vkn            = "Vkn"                  [OK]
/// *FirmaAd        = "FirmaAd" (kurumsal)   [OK]
/// *Adres          = "Adres"                [OK]
/// </summary>
public class QuickExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 110;
    public override string SirketAdi => "Quick Sigorta";
    public override string[] FileNamePatterns => new[] { "quick", "quıck", "qck" };

    // Ana sayfa: PoliceListesi
    public override string? MainSheetName => "PoliceListesi";

    // Ek sayfa: Sigortalilar (TC, Ad, Soyad bilgileri burada)
    public override string[]? AdditionalSheetNames => new[] { "Sigortalilar" };

    // RequiredColumns - hem camelCase hem UPPER_CASE formatları destekleniyor
    protected override string[] RequiredColumns => new[]
    {
        "Police", "Prim", "Tarih"  // Genel anahtar kelimeler
    };

    // Quick'e özgü kolonlar - içerik bazlı tespit için
    // "UrunAd" + "AcenteKomisyon" kombinasyonu sadece Quick'te var
    // NormalizeColumnName ile camelCase/UPPER_CASE fark etmez
    protected override string[] SignatureColumns => new[]
    {
        "UrunAd", "AcenteKomisyon"
    };

    /// <summary>
    /// Sigortalilar sayfasından oluşturulan lookup için kayıt
    /// </summary>
    private record SigortaliInfo(string? Tckn, string? Vkn, string? Ad, string? Soyad, string? FirmaAd, string? Adres);

    public override List<ExcelImportRowDto> ParseWithAdditionalSheets(
        IEnumerable<IDictionary<string, object?>> mainRows,
        Dictionary<string, List<IDictionary<string, object?>>> additionalSheets)
    {
        // Sigortalilar sayfasından lookup oluştur
        var sigortaliLookup = new Dictionary<string, SigortaliInfo>(StringComparer.OrdinalIgnoreCase);

        if (additionalSheets.TryGetValue("Sigortalilar", out var sigortaliRows))
        {
            foreach (var row in sigortaliRows)
            {
                var policeNo = GetStringValue(row, "PoliceNo", "POLICENO", "POLİCENO");
                var zeyilNo = GetStringValue(row, "ZeyilNo", "ZEYILNO", "ZEYİLNO") ?? "0";

                if (string.IsNullOrWhiteSpace(policeNo))
                    continue;

                var key = $"{policeNo}_{GetZeyilNo(zeyilNo)}";

                // Aynı PoliceNo+ZeyilNo için ilk kaydı al
                if (!sigortaliLookup.ContainsKey(key))
                {
                    // Exact match kullan (Ad/Adres karışmasın diye)
                    var info = new SigortaliInfo(
                        Tckn: GetExactColumnValue(row, "Tckn"),
                        Vkn: GetExactColumnValue(row, "Vkn"),
                        Ad: GetExactColumnValue(row, "Ad"),
                        Soyad: GetExactColumnValue(row, "Soyad"),
                        FirmaAd: GetExactColumnValue(row, "FirmaAd"),
                        Adres: GetExactColumnValue(row, "Adres")
                    );
                    sigortaliLookup[key] = info;
                }
            }
        }

        // Ana sayfayı parse et ve sigortalı bilgilerini ekle
        return ParseWithLookup(mainRows, sigortaliLookup);
    }

    private List<ExcelImportRowDto> ParseWithLookup(
        IEnumerable<IDictionary<string, object?>> rows,
        Dictionary<string, SigortaliInfo> sigortaliLookup)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "PoliceNo", "POLICENO", "POLİCE NO", "POLICE NO", "POLICE_NO");

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            var zeyilNo = GetStringValue(row, "ZeyilNo", "ZEYILNO", "ZEYİLNO", "SIRA_NO", "EKBELGE_NO");
            var urunAdi = GetStringValue(row, "UrunAd", "URUNAD", "ÜRÜNAD", "URUN_AD", "MODELLEME_URUN_AD", "URUN_ADI", "ÜRÜN_ADI", "Ürün", "URUN");
            var isZeyil = IsZeyilPolicy(zeyilNo);

            // Debug: BransId tespiti
            var detectedBransId = DetectBransIdFromUrunAdi(urunAdi, isZeyil);
            System.Diagnostics.Debug.WriteLine($"[QuickParser] Row {rowNumber}: UrunAdi='{urunAdi}', DetectedBransId={detectedBransId}");

            // Sigortalı bilgilerini lookup'tan al
            var lookupKey = $"{policeNo}_{GetZeyilNo(zeyilNo)}";
            sigortaliLookup.TryGetValue(lookupKey, out var sigortaliInfo);

            // Ad ve Soyad - Sigortalilar sayfasından
            var ad = sigortaliInfo?.Ad;
            var soyad = sigortaliInfo?.Soyad;

            // Kurumsal müşteri ise (Ad ve Soyad boş, FirmaAd var) firma adını kullan
            if (string.IsNullOrWhiteSpace(ad) && string.IsNullOrWhiteSpace(soyad))
            {
                if (!string.IsNullOrWhiteSpace(sigortaliInfo?.FirmaAd))
                {
                    ad = sigortaliInfo.FirmaAd;
                }
                else
                {
                    // Fallback: PoliceListesi'ndeki SigortaliAdi (varsa)
                    ad = GetStringValue(row, "SigortaliAdi", "SIGORTALI_ADI", "SIGORTALI_AD_SOYAD");
                }
            }

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "YenilemeNo", "YENILEMENO", "YENILEME_NO"),
                ZeyilNo = zeyilNo,
                ZeyilTipKodu = GetStringValue(row, "ZeyilTipKodu", "ZEYILTIPKODU", "ZEYİLTİPKODU", "EKBELGE_KOD"),
                Brans = urunAdi,
                BransId = detectedBransId,
                PoliceTipi = GetPoliceTipiFromPrim(row),

                // Tarihler
                TanzimTarihi = GetDateValue(row, "TanzimTarihi", "TANZIMTARIHI", "TANZİMTARİHİ", "POLICE_TANZIM_TARIH", "TANZIM_TARIH"),
                BaslangicTarihi = GetDateValue(row, "BaslamaTarihi", "BASLAMATARIHI", "BAŞLAMATARIHI", "POLICE_BASLAMA_TARIH", "BASLAMA_TARIH"),
                BitisTarihi = GetDateValue(row, "BitisTarihi", "BITISTARIHI", "BİTİŞTARİHİ", "POLICE_BITIS_TARIH", "BITIS_TARIH"),
                ZeyilOnayTarihi = null,
                ZeyilBaslangicTarihi = null,

                // Primler
                BrutPrim = GetDecimalValue(row, "BrutPrimTL", "BrutPrim", "BRUTPRIMTL", "BRUTPRIM", "BRUT_PRIM", "PRIM_TL", "PRIM"),
                NetPrim = GetDecimalValue(row, "NetPrimTL", "NetPrim", "NETPRIMTL", "NETPRIM", "NET_PRIM_TL", "NET_PRIM"),
                Komisyon = GetDecimalValue(row, "AcenteKomisyonTL", "AcenteKomisyon", "ACENTEKOMISYONTL", "ACENTEKOMISYON", "ACENTE_KOMISYON"),

                // Müşteri Bilgileri - Sigortalilar sayfasından
                SigortaliAdi = ad,
                SigortaliSoyadi = soyad,
                Tckn = sigortaliInfo?.Tckn,
                Vkn = sigortaliInfo?.Vkn,
                Adres = sigortaliInfo?.Adres,

                // Araç Bilgileri
                Plaka = GetStringValue(row, "Plaka", "PLAKA"),

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "AcenteNo", "ACENTENO", "ACENTE_NO", "ACENTE_KOD")
            };

            // Tanzim tarihi yoksa başlangıç tarihini kullan
            if (!dto.TanzimTarihi.HasValue && dto.BaslangicTarihi.HasValue)
            {
                dto = dto with { TanzimTarihi = dto.BaslangicTarihi };
            }

            // Zeyil değilse ve brüt prim yoksa/0 ise net prim'i kullan
            if (!isZeyil && (!dto.BrutPrim.HasValue || dto.BrutPrim == 0) && dto.NetPrim.HasValue)
            {
                dto = dto with { BrutPrim = dto.NetPrim };
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

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        // Sigortalilar sayfası olmadan parse - eski davranış
        return ParseWithLookup(rows, new Dictionary<string, SigortaliInfo>(StringComparer.OrdinalIgnoreCase));
    }

    private string GetPoliceTipiFromPrim(IDictionary<string, object?> row)
    {
        // Police tipi kolonu varsa onu kullan
        var policeTipi = GetStringValue(row, "POLICE_TIP", "POLICE_TIPI", "PoliceTipi");
        if (!string.IsNullOrEmpty(policeTipi) &&
            (policeTipi.ToUpperInvariant().Contains("İPTAL") ||
             policeTipi.ToUpperInvariant().Contains("IPTAL")))
        {
            return "İPTAL";
        }

        var zeyilAd = GetStringValue(row, "ZeyilAd", "ZEYILAD", "ZEYİLAD", "EKBELGE_AD");

        if (!string.IsNullOrEmpty(zeyilAd) &&
            (zeyilAd.ToUpperInvariant().Contains("İPTAL") ||
             zeyilAd.ToUpperInvariant().Contains("IPTAL")))
        {
            return "İPTAL";
        }

        var brutPrim = GetDecimalValue(row, "BrutPrimTL", "BrutPrim", "BRUTPRIMTL", "BRUTPRIM", "BRUT_PRIM", "PRIM_TL", "PRIM");
        return brutPrim < 0 ? "İPTAL" : "TAHAKKUK";
    }

    /// <summary>
    /// Tam kolon adı eşleşmesi yapar (Ad/Adres karışmaması için)
    /// </summary>
    private static string? GetExactColumnValue(IDictionary<string, object?> row, string columnName)
    {
        // Önce tam eşleşme dene
        var key = row.Keys.FirstOrDefault(k =>
            k.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        if (key != null && row.TryGetValue(key, out var value) && value != null)
        {
            var strValue = value.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(strValue))
                return strValue;
        }

        return null;
    }

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue && !row.TanzimTarihi.HasValue)
            errors.Add("Tarih bilgisi geçersiz");

        // Zeyil kontrolü - robust parsing ile
        var isZeyil = IsZeyilPolicy(row.ZeyilNo);

        // Brüt prim veya net prim olmalı (zeyillerde 0 veya negatif olabilir)
        if (!isZeyil)
        {
            if ((!row.BrutPrim.HasValue || row.BrutPrim == 0) &&
                (!row.NetPrim.HasValue || row.NetPrim == 0))
            {
                errors.Add("Prim bilgisi boş veya sıfır");
            }
        }
        // Zeyil için prim 0 veya negatif olabilir, null kontrolü yapılmıyor

        return errors;
    }
}
