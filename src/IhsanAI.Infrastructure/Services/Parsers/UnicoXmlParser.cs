using System.Globalization;
using System.Xml.Linq;
using IhsanAI.Application.Features.ExcelImport.Dtos;
using IhsanAI.Infrastructure.Common;

namespace IhsanAI.Infrastructure.Services.Parsers;

/// <summary>
/// Unico Sigorta XML parser
///
/// XML Yapısı:
/// &lt;Policy&gt;
///   &lt;PolicyNo&gt;185459310&lt;/PolicyNo&gt;
///   &lt;ProductNo&gt;499&lt;/ProductNo&gt;
///   &lt;Product&gt;&lt;ProductNo&gt;499&lt;/ProductNo&gt;&lt;ProductName&gt;UNIKASKO&lt;/ProductName&gt;&lt;/Product&gt;
///   &lt;BegDate&gt;2025-12-15&lt;/BegDate&gt;
///   &lt;EndDate&gt;2026-12-15&lt;/EndDate&gt;
///   &lt;IssueDate&gt;2025-12-01&lt;/IssueDate&gt;
///   &lt;Plate&gt;41TT241&lt;/Plate&gt;
///   &lt;Channel&gt;0516686&lt;/Channel&gt;
///   &lt;Insured&gt;
///     &lt;InsuredName&gt;ERKAN&lt;/InsuredName&gt;
///     &lt;InsuredSurName&gt;TOKUŞ&lt;/InsuredSurName&gt;
///     &lt;CitizenshipNumber&gt;43648589322&lt;/CitizenshipNumber&gt;
///     &lt;TaxNo&gt;...&lt;/TaxNo&gt;
///   &lt;/Insured&gt;
///   &lt;Client&gt;&lt;Address&gt;...&lt;/Address&gt;&lt;/Client&gt;
///   &lt;NetPremium&gt;7625&lt;/NetPremium&gt;
///   &lt;GrossPremium&gt;8006.23&lt;/GrossPremium&gt;
///   &lt;Endors&gt;&lt;EndorsNo&gt;0&lt;/EndorsNo&gt;&lt;/Endors&gt;
///   &lt;RenewalNo&gt;0&lt;/RenewalNo&gt;
///   &lt;PolicyDeductions&gt;
///     &lt;PolicyDeduction&gt;&lt;Name&gt;ACENTE KOMISYONU&lt;/Name&gt;&lt;Amount&gt;1143.75&lt;/Amount&gt;&lt;/PolicyDeduction&gt;
///   &lt;/PolicyDeductions&gt;
///   &lt;IsCancelled&gt;No&lt;/IsCancelled&gt;
/// &lt;/Policy&gt;
///
/// ÜRÜN KODU EŞLEŞTİRME (Unico ProductNo → BransId):
/// 408 → 0 (Trafik)
/// 499 → 1 (Kasko/UNIKASKO)
/// 137, 318 → 2 (Dask)
/// 598 → 3 (Ferdi Kaza)
/// 100 → 5 (Konut)
/// 599 → 7 (Sağlık/Kritik Hastalıklar)
/// 517, 521 → 30 (Yol Destek)
/// </summary>
public class UnicoXmlParser : IExcelParser
{
    public int SigortaSirketiId => 17;
    public string SirketAdi => "Unico Sigorta";
    public string[] FileNamePatterns => new[] { "unico", "aviva", "unicoxml" };
    public int? HeaderRowIndex => null;
    public string? MainSheetName => null;
    public string[]? AdditionalSheetNames => null;

    /// <summary>
    /// Unico ürün kodu → BransId eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, int> ProductNoMapping = new()
    {
        { "408", 0 },   // Trafik
        { "499", 1 },   // Kasko (UNIKASKO)
        { "137", 2 },   // Dask
        { "318", 2 },   // Dask
        { "598", 3 },   // Ferdi Kaza
        { "100", 5 },   // Konut
        { "599", 7 },   // Sağlık (Kritik Hastalıklar)
        { "517", 30 },  // Yol Destek (TR ASSIST)
        { "521", 30 },  // Yol Destek (TUR ASSIST)
    };

    /// <summary>
    /// Unico ürün kodu → Standart branş adı eşleştirmesi
    /// </summary>
    private static readonly Dictionary<string, string> ProductAdiMapping = new()
    {
        { "408", "TRAFİK" },
        { "499", "KASKO" },
        { "137", "DASK" },
        { "318", "DASK" },
        { "598", "FERDİ KAZA" },
        { "100", "KONUT" },
        { "599", "SAĞLIK" },
        { "517", "YOL DESTEK" },
        { "521", "YOL DESTEK" },
    };

    public bool CanParse(string fileName, IEnumerable<string> headerColumns)
    {
        // XML dosyası için dosya adı kontrolü
        // Türkçe karakter normalizasyonu ile pattern eşleştirme
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension != ".xml")
            return false;

        var normalizedFileName = TurkishStringHelper.Normalize(fileName);
        return FileNamePatterns.Any(pattern => normalizedFileName.Contains(TurkishStringHelper.Normalize(pattern)));
    }

    public List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows)
    {
        // Bu metod XML için kullanılmayacak, ParseXml kullanılacak
        return new List<ExcelImportRowDto>();
    }

    /// <summary>
    /// XML stream'den poliçeleri parse eder
    /// </summary>
    public List<ExcelImportRowDto> ParseXml(Stream xmlStream)
    {
        var result = new List<ExcelImportRowDto>();
        var doc = XDocument.Load(xmlStream);

        // Root element'i bul (Policies veya başka bir isim olabilir)
        var policies = doc.Descendants("Policy").ToList();
        int rowNumber = 0;

        foreach (var policy in policies)
        {
            rowNumber++;

            try
            {
                var dto = ParsePolicy(policy, rowNumber);
                if (dto != null)
                {
                    result.Add(dto);
                }
            }
            catch (Exception ex)
            {
                // Parse hatası olan satırları hatalı olarak ekle
                result.Add(new ExcelImportRowDto
                {
                    RowNumber = rowNumber,
                    PoliceNo = GetElementValue(policy, "PolicyNo") ?? $"HATA-{rowNumber}",
                    IsValid = false,
                    ValidationErrors = new List<string> { $"XML parse hatası: {ex.Message}" }
                });
            }
        }

        return result;
    }

    private ExcelImportRowDto? ParsePolicy(XElement policy, int rowNumber)
    {
        var policeNo = GetElementValue(policy, "PolicyNo");
        if (string.IsNullOrWhiteSpace(policeNo))
            return null;

        // Ürün bilgileri
        var productNo = GetElementValue(policy, "ProductNo");
        var productName = GetElementValue(policy, "Product", "ProductName");

        // BransId ve standart branş adı belirleme
        int? bransId = null;
        string? bransAdi = null;
        if (!string.IsNullOrWhiteSpace(productNo))
        {
            var kod = productNo.Trim();
            if (ProductNoMapping.TryGetValue(kod, out var mappedId))
            {
                bransId = mappedId;
            }
            if (ProductAdiMapping.TryGetValue(kod, out var mappedAdi))
            {
                bransAdi = mappedAdi;
            }
        }

        // Kod eşleşmezse ürün adından tespit et
        if (!bransId.HasValue)
        {
            bransId = GetBransIdFromProductName(productName);
            bransAdi = GetStandardBransAdi(productName);
        }

        // Hala bransAdi yoksa orijinal adı kullan
        bransAdi ??= productName;

        // Zeyil bilgileri
        var endorsNo = GetElementValue(policy, "Endors", "EndorsNo");
        var isZeyil = !string.IsNullOrEmpty(endorsNo) && endorsNo != "0";

        // Sigortalı bilgileri - önce Insured, yoksa Client
        var insuredName = GetElementValue(policy, "Insured", "InsuredName");
        var insuredSurname = GetElementValue(policy, "Insured", "InsuredSurName");
        var citizenshipNumber = GetElementValue(policy, "Insured", "CitizenshipNumber");
        var taxNo = GetElementValue(policy, "Insured", "TaxNo");
        var address = GetElementValue(policy, "Client", "Address");

        // Fallback to Client if Insured is empty
        if (string.IsNullOrWhiteSpace(insuredName))
        {
            insuredName = GetElementValue(policy, "Client", "Name");
            insuredSurname = GetElementValue(policy, "Client", "SurName");
        }
        if (string.IsNullOrWhiteSpace(citizenshipNumber))
        {
            citizenshipNumber = GetElementValue(policy, "Client", "CitizenshipNumber");
        }
        if (string.IsNullOrWhiteSpace(taxNo))
        {
            taxNo = GetElementValue(policy, "Client", "TaxNo");
        }

        // Firma adı (kurumsal müşteri)
        var firmName = GetElementValue(policy, "Insured", "InsuredFirmName");
        if (string.IsNullOrWhiteSpace(firmName))
        {
            firmName = GetElementValue(policy, "Client", "FirmName");
        }

        // Eğer firma adı varsa ve kişi adı yoksa, firma adını kullan
        if (!string.IsNullOrWhiteSpace(firmName) && firmName != " - " &&
            string.IsNullOrWhiteSpace(insuredName))
        {
            insuredName = firmName;
        }

        // TC/VKN temizleme
        citizenshipNumber = CleanValue(citizenshipNumber);
        taxNo = CleanValue(taxNo);

        // Komisyon hesaplama
        decimal? komisyon = null;
        var deductions = policy.Element("PolicyDeductions")?.Elements("PolicyDeduction");
        if (deductions != null)
        {
            var acenteKomisyon = deductions.FirstOrDefault(d =>
                GetElementValue(d, "Name")?.Contains("ACENTE", StringComparison.OrdinalIgnoreCase) == true ||
                GetElementValue(d, "Name")?.Contains("KOMISYON", StringComparison.OrdinalIgnoreCase) == true);

            if (acenteKomisyon != null)
            {
                komisyon = ParseDecimal(GetElementValue(acenteKomisyon, "Amount"));
            }
        }

        // İptal kontrolü
        var isCancelled = GetElementValue(policy, "IsCancelled");
        var policeTipi = isCancelled?.Equals("Yes", StringComparison.OrdinalIgnoreCase) == true
            ? "İPTAL"
            : "TAHAKKUK";

        // Prim negatif ise de iptal
        var brutPrim = ParseDecimal(GetElementValue(policy, "GrossPremium"));
        if (brutPrim < 0)
            policeTipi = "İPTAL";

        var dto = new ExcelImportRowDto
        {
            RowNumber = rowNumber,

            // Poliçe Temel Bilgileri
            PoliceNo = policeNo,
            YenilemeNo = GetElementValue(policy, "RenewalNo"),
            ZeyilNo = endorsNo,
            ZeyilTipKodu = GetElementValue(policy, "Endors", "EndorsType"),
            Brans = bransAdi,
            BransId = bransId,
            PoliceTipi = policeTipi,

            // Tarihler
            TanzimTarihi = ParseDate(GetElementValue(policy, "IssueDate")),
            BaslangicTarihi = ParseDate(GetElementValue(policy, "BegDate") ?? GetElementValue(policy, "PolBegDate")),
            BitisTarihi = ParseDate(GetElementValue(policy, "EndDate") ?? GetElementValue(policy, "PolEndDate")),
            ZeyilOnayTarihi = isZeyil ? ParseDate(GetElementValue(policy, "ConfirmDate")) : null,
            ZeyilBaslangicTarihi = null,

            // Primler
            BrutPrim = brutPrim,
            NetPrim = ParseDecimal(GetElementValue(policy, "NetPremium")),
            Komisyon = komisyon,

            // Müşteri Bilgileri
            SigortaliAdi = CleanValue(insuredName),
            SigortaliSoyadi = CleanValue(insuredSurname),
            Tckn = citizenshipNumber?.Length == 11 ? citizenshipNumber : null,
            Vkn = taxNo?.Length == 10 ? taxNo : null,
            Adres = CleanValue(address),

            // Araç/Acente Bilgileri
            Plaka = CleanValue(GetElementValue(policy, "Plate")),
            AcenteNo = GetElementValue(policy, "Channel")
        };

        // Validasyon
        var errors = ValidateRow(dto);
        dto = dto with
        {
            IsValid = errors.Count == 0,
            ValidationErrors = errors
        };

        return dto;
    }

    /// <summary>
    /// Ürün adından BransId çıkarır
    /// </summary>
    private static int? GetBransIdFromProductName(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return null;

        var value = productName.ToUpperInvariant()
            .Replace("İ", "I")
            .Replace("Ü", "U")
            .Replace("Ö", "O")
            .Replace("Ş", "S")
            .Replace("Ç", "C")
            .Replace("Ğ", "G");

        if (value.Contains("TRAFIK"))
            return 0;
        if (value.Contains("KASKO"))
            return 1;
        if (value.Contains("DASK"))
            return 2;
        if (value.Contains("FERDI KAZA") || value.Contains("FERDIKAZA"))
            return 3;
        if (value.Contains("KONUT"))
            return 5;
        if (value.Contains("SAGLIK") || value.Contains("KRITIK") || value.Contains("HASTALIK"))
            return 7;
        if (value.Contains("YOL DESTEK") || value.Contains("YOL YARDIM") || value.Contains("ASSIST"))
            return 30;

        return 255;
    }

    /// <summary>
    /// Ürün adından standart branş adı çıkarır
    /// </summary>
    private static string? GetStandardBransAdi(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return null;

        var value = productName.ToUpperInvariant()
            .Replace("İ", "I")
            .Replace("Ü", "U")
            .Replace("Ö", "O")
            .Replace("Ş", "S")
            .Replace("Ç", "C")
            .Replace("Ğ", "G");

        if (value.Contains("TRAFIK") || value.Contains("ZORUNLU MALI SORUMLULUK"))
            return "TRAFİK";
        if (value.Contains("KASKO"))
            return "KASKO";
        if (value.Contains("DASK") || value.Contains("DEPREM"))
            return "DASK";
        if (value.Contains("FERDI KAZA") || value.Contains("FERDIKAZA"))
            return "FERDİ KAZA";
        if (value.Contains("KONUT"))
            return "KONUT";
        if (value.Contains("SAGLIK") || value.Contains("KRITIK") || value.Contains("HASTALIK"))
            return "SAĞLIK";
        if (value.Contains("YOL DESTEK") || value.Contains("YOL YARDIM") || value.Contains("ASSIST"))
            return "YOL DESTEK";

        return null; // Eşleşme yoksa null döndür, orijinal ad kullanılacak
    }

    private static string? GetElementValue(XElement parent, params string[] path)
    {
        var current = parent;
        foreach (var name in path)
        {
            current = current?.Element(name);
            if (current == null)
                return null;
        }
        return current?.Value?.Trim();
    }

    private static string? CleanValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == " - " || value == "-")
            return null;
        return value.Trim();
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == " - ")
            return null;

        value = value.Replace(",", ".").Trim();
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == " - ")
            return null;

        // Çeşitli formatları dene
        var formats = new[]
        {
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "yyyy-MM-dd'T'HH:mm:ss",
            "dd.MM.yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                return result;
        }

        if (DateTime.TryParse(value, out var generalResult))
            return generalResult;

        return null;
    }

    private List<string> ValidateRow(ExcelImportRowDto row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.PoliceNo))
            errors.Add("Poliçe No boş olamaz");

        if (!row.BaslangicTarihi.HasValue && !row.TanzimTarihi.HasValue)
            errors.Add("Tarih bilgisi geçersiz");

        // Zeyil kontrolü
        var isZeyil = !string.IsNullOrEmpty(row.ZeyilNo) && row.ZeyilNo != "0";
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
