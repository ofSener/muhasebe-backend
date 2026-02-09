using System.Xml;
using System.Xml.Linq;
using IhsanAI.Application.Features.ExcelImport.Dtos;
using IhsanAI.Infrastructure.Common;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Quick Sigorta XML parser
/// XML yapisi: PoliceTransferDto -> Policeler -> Police[]
///
/// XML MAPPING:
/// *PoliceNo         = Police/PoliceNo
/// *YenilemeNo       = Police/YenilemeNo
/// *ZeyilNo          = Police/ZeyilNo
/// *ZeyilTipKodu     = Police/ZeyilTipKodu
/// *Brans            = Police/UrunAd
/// *PoliceTipi       = Police/ZeyilTipAd (IPTAL iceriyorsa) veya BrutPrim negatifse
/// *TanzimTarihi     = Police/TanzimTarihi
/// *BaslangicTarihi  = Police/BaslamaTarihi
/// *BitisTarihi      = Police/BitisTarihi
/// *BrutPrim         = Police/BrutPrimTL
/// *NetPrim          = Police/NetPrimTL
/// *Komisyon         = Police/AcenteKomisyonTL
/// *SigortaliAdi     = Police/Sigortalilar/Sigortali/SigortaliAd
/// *Tckn             = Police/Sigortalilar/Sigortali/Tckn
/// *Adres            = Police/Sigortalilar/Sigortali/Adres
/// *Plaka            = Police/UrunSorular/Soru[SoruKodu=10225]/SoruCevap + Police/UrunSorular/Soru[SoruKodu=10226]/SoruCevap
/// *AcenteNo         = AcenteBilgiler/AcenteNo
/// </summary>
public class QuickXmlParser
{
    public int SigortaSirketiId => 110;
    public string SirketAdi => "Quick Sigorta (XML)";
    public string[] FileNamePatterns => new[] { "quick", "quıck", "qck" };

    /// <summary>
    /// XML dosyasini parse eder
    /// </summary>
    public List<ExcelImportRowDto> ParseXml(Stream xmlStream)
    {
        var result = new List<ExcelImportRowDto>();

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var xmlReader = XmlReader.Create(xmlStream, settings);
            var doc = XDocument.Load(xmlReader);
            var root = doc.Root;

            if (root == null)
                return result;

            // Acente bilgisini al
            var acenteNo = root.Element("AcenteBilgiler")?.Element("AcenteNo")?.Value;

            // Policeler elementini bul
            var policeler = root.Element("Policeler");
            if (policeler == null)
                return result;

            int rowNumber = 0;
            foreach (var police in policeler.Elements("Police"))
            {
                rowNumber++;

                var dto = ParsePoliceElement(police, rowNumber, acenteNo);
                if (dto != null)
                {
                    result.Add(dto);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuickXmlParser] XML parse error: {ex.Message}");
        }

        return result;
    }

    private ExcelImportRowDto? ParsePoliceElement(XElement police, int rowNumber, string? acenteNo)
    {
        var policeNo = police.Element("PoliceNo")?.Value;

        if (string.IsNullOrWhiteSpace(policeNo))
            return null;

        // Temel police bilgileri
        var yenilemeNo = police.Element("YenilemeNo")?.Value;
        var zeyilNo = police.Element("ZeyilNo")?.Value;
        var zeyilTipKodu = police.Element("ZeyilTipKodu")?.Value;
        var zeyilTipAd = police.Element("ZeyilTipAd")?.Value;
        var urunAd = police.Element("UrunAd")?.Value;

        // Tarihler
        var tanzimTarihi = ParseDateTime(police.Element("TanzimTarihi")?.Value);
        var baslamaTarihi = ParseDateTime(police.Element("BaslamaTarihi")?.Value);
        var bitisTarihi = ParseDateTime(police.Element("BitisTarihi")?.Value);

        // Primler
        var brutPrim = ParseDecimal(police.Element("BrutPrimTL")?.Value ?? police.Element("BrutPrim")?.Value);
        var netPrim = ParseDecimal(police.Element("NetPrimTL")?.Value ?? police.Element("NetPrim")?.Value);
        var komisyon = ParseDecimal(police.Element("AcenteKomisyonTL")?.Value ?? police.Element("AcenteKomisyon")?.Value);

        // Sigortali bilgileri
        var sigortali = police.Element("Sigortalilar")?.Element("Sigortali");
        var sigortaliAd = sigortali?.Element("SigortaliAd")?.Value?.Trim();
        var tckn = sigortali?.Element("Tckn")?.Value?.Trim();
        var vkn = sigortali?.Element("Vkn")?.Value?.Trim();
        var adres = sigortali?.Element("Adres")?.Value?.Trim();

        // Ad ve Soyad ayirma
        string? adi = null;
        string? soyadi = null;
        if (!string.IsNullOrWhiteSpace(sigortaliAd))
        {
            var parts = sigortaliAd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                soyadi = parts[^1]; // Son kelime soyad
                adi = string.Join(" ", parts[..^1]); // Geri kalani ad
            }
            else
            {
                adi = sigortaliAd;
            }
        }

        // Plaka bilgisi (UrunSorular icerisinden)
        var plaka = GetPlakaFromSorular(police);

        // Zeyil kontrolu
        var isZeyil = IsZeyilPolicy(zeyilNo);

        // BransId tespiti
        var bransId = DetectBransIdFromUrunAdi(urunAd, isZeyil);

        // Police tipi (iptal mi?)
        var policeTipi = GetPoliceTipi(zeyilTipAd, brutPrim);

        var dto = new ExcelImportRowDto
        {
            RowNumber = rowNumber,

            // Police Temel Bilgileri
            PoliceNo = policeNo,
            YenilemeNo = yenilemeNo,
            ZeyilNo = zeyilNo,
            ZeyilTipKodu = zeyilTipKodu,
            Brans = urunAd,
            BransId = bransId,
            PoliceTipi = policeTipi,

            // Tarihler
            TanzimTarihi = tanzimTarihi,
            BaslangicTarihi = baslamaTarihi,
            BitisTarihi = bitisTarihi,
            ZeyilOnayTarihi = null,
            ZeyilBaslangicTarihi = null,

            // Primler
            BrutPrim = brutPrim,
            NetPrim = netPrim,
            Komisyon = komisyon,

            // Musteri Bilgileri
            SigortaliAdi = adi,
            SigortaliSoyadi = soyadi,
            Tckn = tckn,
            Vkn = vkn,
            Adres = adres,

            // Arac Bilgileri
            Plaka = plaka,

            // Acente Bilgileri
            AcenteNo = acenteNo
        };

        // Tanzim tarihi yoksa baslangic tarihini kullan
        if (!dto.TanzimTarihi.HasValue && dto.BaslangicTarihi.HasValue)
        {
            dto = dto with { TanzimTarihi = dto.BaslangicTarihi };
        }

        // Zeyil degilse ve brut prim yoksa/0 ise net prim'i kullan
        if (!isZeyil && (!dto.BrutPrim.HasValue || dto.BrutPrim == 0) && dto.NetPrim.HasValue)
        {
            dto = dto with { BrutPrim = dto.NetPrim };
        }

        var errors = ValidateRow(dto, isZeyil);
        dto = dto with
        {
            IsValid = errors.Count == 0,
            ValidationErrors = errors
        };

        return dto;
    }

    private string? GetPlakaFromSorular(XElement police)
    {
        var sorular = police.Element("UrunSorular");
        if (sorular == null)
            return null;

        string? plakaNo = null;
        string? plakaIl = null;

        foreach (var soru in sorular.Elements("Soru"))
        {
            var soruKodu = soru.Element("SoruKodu")?.Value;
            var soruCevap = soru.Element("SoruCevap")?.Value?.Trim();

            if (soruKodu == "10225") // PLAKA NO
            {
                plakaNo = soruCevap;
            }
            else if (soruKodu == "10226") // PLAKA IL KODU
            {
                plakaIl = soruCevap?.TrimStart('0'); // Bastaki sifirlari kaldir (034 -> 34)
            }
        }

        if (!string.IsNullOrWhiteSpace(plakaNo))
        {
            if (!string.IsNullOrWhiteSpace(plakaIl))
            {
                return $"{plakaIl} {plakaNo}";
            }
            return plakaNo;
        }

        return null;
    }

    private string GetPoliceTipi(string? zeyilTipAd, decimal? brutPrim)
    {
        if (!string.IsNullOrEmpty(zeyilTipAd))
        {
            var upper = zeyilTipAd.ToUpperInvariant();
            if (upper.Contains("IPTAL") || upper.Contains("İPTAL"))
            {
                return "İPTAL";
            }
        }

        if (brutPrim.HasValue && brutPrim < 0)
        {
            return "İPTAL";
        }

        return "TAHAKKUK";
    }

    private bool IsZeyilPolicy(string? zeyilNo)
    {
        if (string.IsNullOrWhiteSpace(zeyilNo))
            return false;

        // "0" veya bos degilse zeyildir
        if (int.TryParse(zeyilNo.Trim(), out var num))
        {
            return num > 0;
        }

        return false;
    }

    private int? DetectBransIdFromUrunAdi(string? urunAdi, bool isZeyil = false)
    {
        if (string.IsNullOrWhiteSpace(urunAdi))
            return null;

        var value = urunAdi.ToUpperInvariant()
            .Replace("İ", "I")
            .Replace("Ş", "S")
            .Replace("Ğ", "G")
            .Replace("Ü", "U")
            .Replace("Ö", "O")
            .Replace("Ç", "C");

        // Trafik (ID: 0)
        if (value.Contains("TRAFIK") || value.Contains("TRAFFIC") || value.Contains("ZMSS") || value.Contains("ZORUNLU MALI"))
            return 0;

        // Kasko (ID: 1)
        if (value.Contains("KASKO"))
            return 1;

        // Konut (ID: 2)
        if (value.Contains("KONUT") || value.Contains("EV") || value.Contains("MESKEN"))
            return 2;

        // DASK (ID: 3)
        if (value.Contains("DASK") || value.Contains("DEPREM"))
            return 3;

        // Isyeri (ID: 4)
        if (value.Contains("ISYERI") || value.Contains("IS YERI") || value.Contains("ISYER"))
            return 4;

        // Nakliyat (ID: 5)
        if (value.Contains("NAKLIYAT") || value.Contains("TASIMA"))
            return 5;

        // Saglik (ID: 6)
        if (value.Contains("SAGLIK") && !value.Contains("TAMAMLAYICI") && !value.Contains("AYAKTA") && !value.Contains("YATARAK"))
            return 6;

        // Hayat (ID: 7)
        if (value.Contains("HAYAT") || value.Contains("LIFE"))
            return 7;

        // Ferdi Kaza (ID: 8)
        if (value.Contains("FERDI KAZA") || value.Contains("FERDI") || value.Contains("KAZA"))
            return 8;

        // Sorumluluk (ID: 9)
        if (value.Contains("SORUMLULUK") || value.Contains("MESLEKI"))
            return 9;

        // Tarim (ID: 10)
        if (value.Contains("TARIM") || value.Contains("HAYVAN") || value.Contains("BITKISEL"))
            return 10;

        // Muhendislik (ID: 12)
        if (value.Contains("MUHENDISLIK") || value.Contains("MAKINE") || value.Contains("INSAAT") || value.Contains("ELEKTRONIK"))
            return 12;

        // Tamamlayici Saglik, Ayakta, Yatarak -> 16 (Tamamlayici Saglik)
        if (value.Contains("TAMAMLAYICI") || value.Contains("AYAKTA") || value.Contains("YATARAK"))
            return 16;

        // Diger (ID: 15)
        if (value.Contains("DIGER") || value.Contains("FINANSAL") || value.Contains("KREDI"))
            return 15;

        // Seyahat (ID: 17)
        if (value.Contains("SEYAHAT") || value.Contains("TRAVEL"))
            return 17;

        // IMM (ID: 19)
        if (value.Contains("IMM") || value.Contains("IHTIYARI MALI"))
            return 19;

        // Default
        return null;
    }

    private List<string> ValidateRow(ExcelImportRowDto row, bool isZeyil)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Police No bos olamaz");

        if (!row.BaslangicTarihi.HasValue && !row.TanzimTarihi.HasValue)
            errors.Add("Tarih bilgisi gecersiz");

        // Brut prim veya net prim olmali (zeyillerde 0 veya negatif olabilir)
        if (!isZeyil)
        {
            if ((!row.BrutPrim.HasValue || row.BrutPrim == 0) &&
                (!row.NetPrim.HasValue || row.NetPrim == 0))
            {
                errors.Add("Prim bilgisi bos veya sifir");
            }
        }

        return errors;
    }

    private DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParse(value, out var dt))
            return dt;

        return null;
    }

    private decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Turkce format dene
        value = value.Replace(" ", "").Trim();

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            new System.Globalization.CultureInfo("tr-TR"), out result))
        {
            return result;
        }

        return null;
    }

    /// <summary>
    /// Dosyanin Quick XML formati olup olmadigini kontrol eder.
    /// Türkçe karakter normalizasyonu ile dosya adı pattern eşleştirmesi yapar.
    /// </summary>
    public bool CanParseXml(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension != ".xml")
            return false;

        // Türkçe karakter normalizasyonu ile pattern eşleştirme
        // Örn: "quıck" ve "quick" aynı şekilde eşleşir
        var normalizedFileName = TurkishStringHelper.Normalize(fileName);
        return FileNamePatterns.Any(p => normalizedFileName.Contains(TurkishStringHelper.Normalize(p)));
    }

    /// <summary>
    /// Stream'den XML formatini dogrular
    /// </summary>
    public bool ValidateXmlFormat(Stream xmlStream)
    {
        try
        {
            var position = xmlStream.Position;
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var xmlReader = XmlReader.Create(xmlStream, settings);
            var doc = XDocument.Load(xmlReader);
            xmlStream.Position = position;

            var root = doc.Root;
            if (root == null)
                return false;

            // PoliceTransferDto root element veya Policeler elementi olmali
            return root.Name.LocalName == "PoliceTransferDto" ||
                   root.Element("Policeler") != null ||
                   root.Element("AcenteBilgiler") != null;
        }
        catch
        {
            return false;
        }
    }
}
