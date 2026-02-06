using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// HDI Sigorta Excel parser
/// Dosya formatı: raporSonuc_sid_*.xlsx
///
/// MAPPING:
/// *PoliceNo       = "Poliçe No"            [OK]
/// *YenilemeNo     = "Tecdit No"            [OK]
/// *ZeyilNo        = "Zeyil No"             [OK]
/// *ZeyilTipKodu   = "Zeyil Kod"            [OK]
/// *Brans          = "Branş"                [OK]
/// *PoliceTipi     = "İpt/Kay"              [OK]
/// *TanzimTarihi   = "Tanzim Tarihi"        [OK]
/// *BaslangicTarihi= "Vade Başlangıç"       [OK]
/// *BitisTarihi    = "Vade Bitiş"           [OK]
/// *ZeyilOnayTarihi= YOK                    [NO]
/// *ZeyilBaslangicTarihi = YOK              [NO]
/// *BrutPrim       = "Brüt Prim"            [OK]
/// *NetPrim        = "Net Prim"             [OK]
/// *Komisyon       = "Komisyon"             [OK]
/// *SigortaliAdi   = "Sigortalı Adı"        [OK]
/// *SigortaliSoyadi= "Sigortalı Soyadı"     [OK]
/// *Plaka          = YOK                    [NO]
/// *AcenteNo       = "Acente"               [OK]
/// </summary>
public class HdiExcelParser : BaseExcelParser
{
    public override int SigortaSirketiId => 7;
    public override string SirketAdi => "HDI Sigorta";
    public override string[] FileNamePatterns => new[] { "hdi" };

    // HDI formatında header ilk satırda
    public override int? HeaderRowIndex => 1;

    protected override string[] RequiredColumns => new[]
    {
        "Poliçe No", "Prim", "Vade"
    };

    // HDI'ya özgü kolonlar - içerik bazlı tespit için
    protected override string[] SignatureColumns => new[]
    {
        "Vade Başlangıç", "İpt/Kay", "Tecdit No"  // Bu kombinasyon sadece HDI'da var
    };

    public override List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        var result = new List<ExcelImportRowDto>();
        int rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;

            var policeNo = GetStringValue(row, "Poliçe No", "POLİÇE NO", "POLICE NO");

            if (string.IsNullOrWhiteSpace(policeNo))
                continue;

            // Header satırını atla
            if (policeNo.ToUpperInvariant().Contains("POLİÇE") ||
                policeNo.ToUpperInvariant().Contains("POLICE"))
                continue;

            var dto = new ExcelImportRowDto
            {
                RowNumber = rowNumber,

                // Poliçe Temel Bilgileri
                PoliceNo = policeNo,
                YenilemeNo = GetStringValue(row, "Tecdit No", "TECDİT NO"),
                ZeyilNo = GetStringValue(row, "Zeyil No", "ZEYİL NO", "ZEYIL NO"),
                ZeyilTipKodu = GetStringValue(row, "Zeyil Kod", "ZEYİL KOD", "ZEYIL KOD"),
                Brans = GetBransFromKod(row),
                PoliceTipi = GetPoliceTipi(row),

                // Tarihler
                TanzimTarihi = GetDateValue(row, "Tanzim Tarihi", "TANZİM TARİHİ", "TANZIM TARIHI"),
                BaslangicTarihi = GetDateValue(row, "Vade Başlangıç", "VADE BAŞLANGIÇ", "VADE BASLANGIC"),
                BitisTarihi = GetDateValue(row, "Vade Bitiş", "VADE BİTİŞ", "VADE BITIS"),
                ZeyilOnayTarihi = null,
                ZeyilBaslangicTarihi = null,

                // Primler
                BrutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "BRUT PRIM"),
                NetPrim = GetDecimalValue(row, "Net Prim", "NET PRİM", "NET PRIM"),
                Komisyon = GetDecimalValue(row, "Komisyon", "KOMİSYON", "KOMISYON"),

                // Müşteri Bilgileri
                SigortaliAdi = GetFullName(row),
                SigortaliSoyadi = null,  // Birleşik ad

                // Araç Bilgileri
                Plaka = null,  // HDI'da yok

                // Acente Bilgileri
                AcenteNo = GetStringValue(row, "Acente", "ACENTE")
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

    private string? GetFullName(IDictionary<string, object?> row)
    {
        var adi = GetStringValue(row, "Sigortalı Adı", "SİGORTALI ADI", "SIGORTALI ADI")?.Trim();
        var soyadi = GetStringValue(row, "Sigortalı Soyadı", "SİGORTALI SOYADI", "SIGORTALI SOYADI")?.Trim();

        if (string.IsNullOrEmpty(adi) && string.IsNullOrEmpty(soyadi))
            return null;

        if (string.IsNullOrEmpty(soyadi))
            return adi;

        if (string.IsNullOrEmpty(adi))
            return soyadi;

        return $"{adi} {soyadi}";
    }

    private string? GetBransFromKod(IDictionary<string, object?> row)
    {
        var brans = GetStringValue(row, "Branş", "BRANŞ", "BRANS");

        // HDI branş kodları
        return brans switch
        {
            "199" => "DASK",
            "310" => "TRAFİK",
            "320" => "KASKO",
            "330" => "KONUT",
            "340" => "İŞYERİ",
            _ => brans
        };
    }

    private string GetPoliceTipi(IDictionary<string, object?> row)
    {
        // İpt/Kay kolonu: K=Kayıt, I=İptal
        var iptKay = GetStringValue(row, "İpt/Kay", "IPT/KAY", "İPT/KAY");

        if (iptKay?.ToUpperInvariant() == "I")
            return "İPTAL";

        // Zeyil Ad'da iptal varsa
        var zeyilAd = GetStringValue(row, "Zeyil Ad", "ZEYİL AD", "ZEYIL AD");
        if (!string.IsNullOrEmpty(zeyilAd) &&
            (zeyilAd.ToUpperInvariant().Contains("İPTAL") ||
             zeyilAd.ToUpperInvariant().Contains("IPTAL")))
        {
            return "İPTAL";
        }

        // Brüt prim negatifse iptal
        var brutPrim = GetDecimalValue(row, "Brüt Prim", "BRÜT PRİM", "BRUT PRIM");
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
            errors.Add("Vade Başlangıç tarihi geçersiz");

        if (!row.BrutPrim.HasValue)
            errors.Add("Brüt Prim boş");

        return errors;
    }
}
