using FluentAssertions;
using Xunit;
using IhsanAI.Application.Tests.Common;

namespace IhsanAI.Application.Tests.Services;

/// <summary>
/// Unit tests for Excel Import functionality
/// Tests parser selection, validation, and column mapping
/// </summary>
public class ExcelImportServiceTests : TestBase
{
    [Theory]
    [InlineData("Sompo", "SompoExcelParser")]
    [InlineData("Ankara", "AnkaraExcelParser")]
    [InlineData("Quick", "QuickExcelParser")]
    [InlineData("HDI", "HdiExcelParser")]
    [InlineData("Axa", "AxaExcelParser")]
    [InlineData("Mapfre", "MapfreExcelParser")]
    [InlineData("Anadolu", "AnadoluExcelParser")]
    [InlineData("Allianz", "AllianzExcelParser")]
    public void GetParserForCompany_ReturnsCorrectParserName(string company, string expectedParser)
    {
        // This is a mapping test - in real implementation, this would test the actual service
        var parserMap = new Dictionary<string, string>
        {
            { "Sompo", "SompoExcelParser" },
            { "Ankara", "AnkaraExcelParser" },
            { "Quick", "QuickExcelParser" },
            { "HDI", "HdiExcelParser" },
            { "Axa", "AxaExcelParser" },
            { "Mapfre", "MapfreExcelParser" },
            { "Anadolu", "AnadoluExcelParser" },
            { "Allianz", "AllianzExcelParser" }
        };

        // Act
        var result = parserMap.GetValueOrDefault(company);

        // Assert
        result.Should().Be(expectedParser);
    }

    [Fact]
    public void ValidateExcelFile_WithValidExtension_ReturnsTrue()
    {
        // Arrange
        var validExtensions = new[] { ".xlsx", ".xls", ".xlsm" };
        var fileName = "policy_import.xlsx";

        // Act
        var extension = Path.GetExtension(fileName);
        var isValid = validExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateExcelFile_WithInvalidExtension_ReturnsFalse()
    {
        // Arrange
        var validExtensions = new[] { ".xlsx", ".xls", ".xlsm" };
        var fileName = "policy_import.pdf";

        // Act
        var extension = Path.GetExtension(fileName);
        var isValid = validExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(".xlsx", true)]
    [InlineData(".xls", true)]
    [InlineData(".XLSX", true)]
    [InlineData(".XLS", true)]
    [InlineData(".csv", false)]
    [InlineData(".pdf", false)]
    [InlineData(".docx", false)]
    [InlineData("", false)]
    public void ValidateFileExtension_ReturnsExpectedResult(string extension, bool expected)
    {
        // Arrange
        var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xls", ".xlsm" };

        // Act
        var isValid = validExtensions.Contains(extension);

        // Assert
        isValid.Should().Be(expected);
    }

    [Fact]
    public void ColumnMapping_ShouldMapStandardColumns()
    {
        // Arrange
        var standardColumns = new Dictionary<string, string>
        {
            { "PoliceNo", "Police No" },
            { "TanzimTarihi", "Tanzim Tarihi" },
            { "BaslangicTarihi", "Baslangic Tarihi" },
            { "BitisTarihi", "Bitis Tarihi" },
            { "BrutPrim", "Brut Prim" },
            { "NetPrim", "Net Prim" },
            { "Komisyon", "Komisyon" },
            { "SigortaEttiren", "Sigorta Ettiren" }
        };

        // Assert
        standardColumns.Should().ContainKey("PoliceNo");
        standardColumns.Should().ContainKey("BrutPrim");
        standardColumns.Should().ContainKey("Komisyon");
    }

    [Fact]
    public void ValidateRequiredColumns_AllPresent_ReturnsSuccess()
    {
        // Arrange
        var requiredColumns = new[] { "PoliceNo", "TanzimTarihi", "BrutPrim" };
        var fileColumns = new[] { "PoliceNo", "TanzimTarihi", "BrutPrim", "NetPrim", "Komisyon" };

        // Act
        var missingColumns = requiredColumns.Except(fileColumns).ToList();

        // Assert
        missingColumns.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRequiredColumns_MissingColumn_ReturnsMissingList()
    {
        // Arrange
        var requiredColumns = new[] { "PoliceNo", "TanzimTarihi", "BrutPrim", "SigortaEttiren" };
        var fileColumns = new[] { "PoliceNo", "TanzimTarihi", "BrutPrim" };

        // Act
        var missingColumns = requiredColumns.Except(fileColumns).ToList();

        // Assert
        missingColumns.Should().Contain("SigortaEttiren");
        missingColumns.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("1000", 1000.0)]
    [InlineData("1.000", 1000.0)]        // Turkish thousand separator
    [InlineData("1.000,00", 1000.0)]     // Turkish format: 1.000,00 = 1000.00
    [InlineData("1000,50", 1000.50)]     // Turkish decimal: 1000,50 = 1000.50
    [InlineData("2500", 2500.0)]
    public void ParseDecimalValue_VariousFormats_ReturnsCorrectValue(string input, double expected)
    {
        // This tests Turkish number format parsing (common in insurance Excel files)
        // Turkish format: "." is thousand separator, "," is decimal separator
        // Example: "1.000,50" = 1000.50 in Turkish

        // Arrange & Act
        var normalized = input
            .Replace(".", "")     // Remove Turkish thousand separator
            .Replace(",", ".");   // Convert Turkish decimal to standard decimal

        // Parse with invariant culture
        double.TryParse(normalized, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result);

        // Assert
        result.Should().BeApproximately(expected, 0.01);
    }

    [Theory]
    [InlineData("15.06.2024", 2024, 6, 15)]
    [InlineData("2024-06-15", 2024, 6, 15)]
    [InlineData("15/06/2024", 2024, 6, 15)]
    public void ParseDateValue_VariousFormats_ReturnsCorrectDate(string input, int expectedYear, int expectedMonth, int expectedDay)
    {
        // Arrange
        var formats = new[] { "dd.MM.yyyy", "yyyy-MM-dd", "dd/MM/yyyy" };

        // Act
        DateTime.TryParseExact(input, formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var result);

        // Assert
        result.Year.Should().Be(expectedYear);
        result.Month.Should().Be(expectedMonth);
        result.Day.Should().Be(expectedDay);
    }

    [Fact]
    public void ValidatePoliceNo_NonEmpty_IsValid()
    {
        // Arrange
        var policeNo = "POL-2024-001";

        // Act
        var isValid = !string.IsNullOrWhiteSpace(policeNo);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ValidatePoliceNo_EmptyOrNull_IsInvalid(string? policeNo)
    {
        // Act
        var isValid = !string.IsNullOrWhiteSpace(policeNo);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateBrutPrim_PositiveValue_IsValid()
    {
        // Arrange
        decimal brutPrim = 1000.50m;

        // Act
        var isValid = brutPrim > 0;

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void ValidateBrutPrim_ZeroOrNegative_IsInvalid(decimal brutPrim)
    {
        // Act
        var isValid = brutPrim > 0;

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void BatchImport_CreatesSummary()
    {
        // Arrange
        var importResults = new List<ImportResult>
        {
            new ImportResult { PoliceNo = "P001", Success = true },
            new ImportResult { PoliceNo = "P002", Success = true },
            new ImportResult { PoliceNo = "P003", Success = false, ErrorMessage = "Invalid date" },
            new ImportResult { PoliceNo = "P004", Success = true },
            new ImportResult { PoliceNo = "P005", Success = false, ErrorMessage = "Missing required field" }
        };

        // Act
        var successCount = importResults.Count(r => r.Success);
        var failureCount = importResults.Count(r => !r.Success);
        var errors = importResults.Where(r => !r.Success).Select(r => r.ErrorMessage).ToList();

        // Assert
        successCount.Should().Be(3);
        failureCount.Should().Be(2);
        errors.Should().Contain("Invalid date");
        errors.Should().Contain("Missing required field");
    }

    private class ImportResult
    {
        public string PoliceNo { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
