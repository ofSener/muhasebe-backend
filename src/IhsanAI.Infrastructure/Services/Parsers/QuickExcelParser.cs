using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Quick Sigorta Excel parser
/// Sheet: PoliceListesi
///
/// MAPPING:
/// *PoliceNo       = "PoliceNo"             [OK]
/// *YenilemeNo     = "YenilemeNo"           [OK]
/// *ZeyilNo        = "ZeyilNo"              [OK]
/// *ZeyilTipKodu   = "ZeyilTipKodu"         [OK]
/// *Brans          = "UrunAd"               [OK]
/// *PoliceTipi     = YOK                    [NO]
/// *TanzimTarihi   = "TanzimTarihi"         [OK]
/// *BaslangicTarihi= "BaslamaTarihi"        [OK]
/// *BitisTarihi    = "BitisTarihi"          [OK]
/// *ZeyilOnayTarihi= YOK                    [NO]
/// *ZeyilBaslangicTarihi = YOK              [NO]
/// *BrutPrim       = "BrutPrimTL"           [OK]
/// *NetPrim        = "NetPrimTL"            [OK]
/// *Komisyon       = "AcenteKomisyonTL"     [OK]
/// *SigortaliAdi   = YOK                    [NO]
/// *SigortaliSoyadi= YOK                    [NO]
/// *Plaka          = YOK                    [NO]
/// *AcenteNo       = "AcenteNo"             [OK]
/// </summary>
public class QuickExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 3;
    public override string SirketAdi => "Quick Sigorta";
    public override string[] FileNamePatterns => new[] { "quick", "quıck", "qck", "police_listesi", "policelistesi" };

    // RequiredColumns - hem camelCase hem UPPER_CASE formatları destekleniyor
    protected override string[] RequiredColumns => new[]
    {
        "Police", "Prim", "Tarih"  // Genel anahtar kelimeler
    };

    // Quick'e özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "POLICE_HAREKET_NO", "EKBELGE_NO"  // Bu kombinasyon sadece Quick Police_Listesi'nde var
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            // Büyük/küçük harf ve alt çizgili alternatifler destekleniyor
            // Police_Listesi formatı: POLICE_NO, POLICE_BASLAMA_TARIH, PRIM_TL vs.
            var policeNo = GetStringValue(row, "PoliceNo", "POLICENO", "POLİCE NO", "POLICE NO", "POLICE_NO");

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "YenilemeNo", "YENILEMENO", "YENILEME_NO"),
                ZeyilNo = GetStringValue(row, "ZeyilNo", "ZEYILNO", "ZEYİLNO", "SIRA_NO", "EKBELGE_NO"),
                ZeyilTipKodu = GetStringValue(row, "ZeyilTipKodu", "ZEYILTIPKODU", "ZEYİLTİPKODU", "EKBELGE_KOD"),
                Brans = GetStringValue(row, "UrunAd", "URUNAD", "ÜRÜNAD", "URUN_AD", "MODELLEME_URUN_AD"),
                PoliceTipi = GetPoliceTipiFromPrim(row),

                // Tarihler - Police_Listesi formatı: POLICE_TANZIM_TARIH, POLICE_BASLAMA_TARIH
                TanzimTarihi = GetDateValue(row, "TanzimTarihi", "TANZIMTARIHI", "TANZİMTARİHİ", "POLICE_TANZIM_TARIH", "TANZIM_TARIH"),
                BaslangicTarihi = GetDateValue(row, "BaslamaTarihi", "BASLAMATARIHI", "BAŞLAMATARIHI", "POLICE_BASLAMA_TARIH", "BASLAMA_TARIH"),
                BitisTarihi = GetDateValue(row, "BitisTarihi", "BITISTARIHI", "BİTİŞTARİHİ", "POLICE_BITIS_TARIH", "BITIS_TARIH"),
                ZeyilOnayTarihi = null,  // Quick'te yok
                ZeyilBaslangicTarihi = null,  // Quick'te yok

                // Primler - Police_Listesi formatı: PRIM_TL, NET_PRIM_TL
                BrutPrim = GetDecimalValue(row, "BrutPrimTL", "BrutPrim", "BRUTPRIMTL", "BRUTPRIM", "BRUT_PRIM", "PRIM_TL", "PRIM"),
                NetPrim = GetDecimalValue(row, "NetPrimTL", "NetPrim", "NETPRIMTL", "NETPRIM", "NET_PRIM_TL", "NET_PRIM"),
                Komisyon = GetDecimalValue(row, "AcenteKomisyonTL", "AcenteKomisyon", "ACENTEKOMISYONTL", "ACENTEKOMISYON", "ACENTE_KOMISYON"),

                // Müşteri Bilgileri
                SigortaliAdi = GetStringValue(row, "SigortaliAdi", "SIGORTALI_ADI", "SIGORTALI_AD_SOYAD"),
                SigortaliSoyadi = null,

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

            // Brüt prim yoksa net prim'i kullan
            if ((!dto.BrutPrim.HasValue || dto.BrutPrim == 0) && dto.NetPrim.HasValue)
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

    protected override List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue && !row.TanzimTarihi.HasValue)
            errors.Add("Tarih bilgisi geçersiz");

        // Brüt prim veya net prim olmalı
        if ((!row.BrutPrim.HasValue || row.BrutPrim == 0) &&
            (!row.NetPrim.HasValue || row.NetPrim == 0))
        {
            errors.Add("Prim bilgisi boş veya sıfır");
        }

        return errors;
    }
}
