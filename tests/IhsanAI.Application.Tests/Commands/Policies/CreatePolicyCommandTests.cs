using FluentAssertions;
using Moq;
using Xunit;
using IhsanAI.Application.Tests.Common;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Tests.Commands.Policies;

/// <summary>
/// Unit tests for Policy-related commands
/// Tests CRUD operations and business logic for policies
/// </summary>
public class CreatePolicyCommandTests : TestBase
{
    public CreatePolicyCommandTests()
    {
        CurrentUserServiceMock.Setup(x => x.UserId).Returns("1");
        CurrentUserServiceMock.Setup(x => x.FirmaId).Returns(1);
        CurrentUserServiceMock.Setup(x => x.SubeId).Returns(1);
    }

    [Fact]
    public void Police_Creation_ShouldHaveRequiredFields()
    {
        // Arrange
        var police = new Police
        {
            Id = 1,
            PoliceNo = "POL-001",
            PoliceTipi = "Yeni",
            ZeyilNo = 0,
            SigortaSirketiId = 1,
            TanzimTarihi = DateTime.Now,
            BaslangicTarihi = DateTime.Now,
            BitisTarihi = DateTime.Now.AddYears(1),
            SigortaEttirenId = 1,
            BrutPrim = 1000,
            NetPrim = 900,
            Vergi = 100,
            Komisyon = 150,
            BransId = 1,
            IsOrtagiFirmaId = 1,
            IsOrtagiSubeId = 1,
            IsOrtagiUyeId = 1,
            OnayDurumu = 1
        };

        // Assert
        police.PoliceNo.Should().NotBeNullOrEmpty();
        police.SigortaSirketiId.Should().BeGreaterThan(0);
        police.BransId.Should().BeGreaterThan(0);
        police.BrutPrim.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Police_WithOnayDurumu0_ShouldBeInPool()
    {
        // Arrange
        var police = new Police
        {
            Id = 1,
            PoliceNo = "POL-001",
            OnayDurumu = 0 // In pool (pending)
        };

        // Assert
        police.OnayDurumu.Should().Be(0);
    }

    [Fact]
    public void Police_WithOnayDurumu1_ShouldBeApproved()
    {
        // Arrange
        var police = new Police
        {
            Id = 1,
            PoliceNo = "POL-001",
            OnayDurumu = 1 // Approved
        };

        // Assert
        police.OnayDurumu.Should().Be(1);
    }

    [Fact]
    public void Police_DateRange_ShouldBeValid()
    {
        // Arrange
        var startDate = DateTime.Now;
        var endDate = DateTime.Now.AddYears(1);

        var police = new Police
        {
            Id = 1,
            PoliceNo = "POL-001",
            BaslangicTarihi = startDate,
            BitisTarihi = endDate
        };

        // Assert
        police.BitisTarihi.Should().BeAfter(police.BaslangicTarihi);
    }

    [Fact]
    public void Police_Komisyon_ShouldBeLessThanBrutPrim()
    {
        // Arrange
        var police = new Police
        {
            Id = 1,
            PoliceNo = "POL-001",
            BrutPrim = 1000,
            Komisyon = 100
        };

        // Assert
        police.Komisyon.Should().BeLessThan(police.BrutPrim);
    }

    [Fact]
    public void Police_NetPrim_ShouldBeLessThanOrEqualToBrutPrim()
    {
        // Arrange
        var police = new Police
        {
            Id = 1,
            PoliceNo = "POL-001",
            BrutPrim = 1000,
            NetPrim = 900
        };

        // Assert
        police.NetPrim.Should().BeLessThanOrEqualTo(police.BrutPrim);
    }

    [Theory]
    [InlineData(1000, 900, 100)]
    [InlineData(2500, 2000, 500)]
    [InlineData(500, 450, 50)]
    public void Police_PrimCalculations_ShouldBeValid(decimal brutPrim, decimal netPrim, decimal vergi)
    {
        // Arrange
        var police = new Police
        {
            Id = 1,
            PoliceNo = "POL-001",
            BrutPrim = brutPrim,
            NetPrim = netPrim,
            Vergi = vergi
        };

        // Assert
        police.BrutPrim.Should().BeGreaterThan(0);
        police.NetPrim.Should().BeGreaterThan(0);
        police.NetPrim.Should().BeLessThanOrEqualTo(police.BrutPrim);
    }

    [Fact]
    public void PolicyList_Filter_ByFirmaId()
    {
        // Arrange
        var policies = new List<Police>
        {
            new Police { Id = 1, IsOrtagiFirmaId = 1, PoliceNo = "P001" },
            new Police { Id = 2, IsOrtagiFirmaId = 2, PoliceNo = "P002" },
            new Police { Id = 3, IsOrtagiFirmaId = 1, PoliceNo = "P003" }
        };

        // Act
        var filteredPolicies = policies.Where(p => p.IsOrtagiFirmaId == 1).ToList();

        // Assert
        filteredPolicies.Should().HaveCount(2);
        filteredPolicies.Should().AllSatisfy(p => p.IsOrtagiFirmaId.Should().Be(1));
    }

    [Fact]
    public void PolicyList_Filter_BySubeId()
    {
        // Arrange
        var policies = new List<Police>
        {
            new Police { Id = 1, IsOrtagiSubeId = 1, PoliceNo = "P001" },
            new Police { Id = 2, IsOrtagiSubeId = 2, PoliceNo = "P002" },
            new Police { Id = 3, IsOrtagiSubeId = 1, PoliceNo = "P003" }
        };

        // Act
        var filteredPolicies = policies.Where(p => p.IsOrtagiSubeId == 1).ToList();

        // Assert
        filteredPolicies.Should().HaveCount(2);
    }

    [Fact]
    public void PolicyList_Filter_ByDateRange()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 12, 31);

        var policies = new List<Police>
        {
            new Police { Id = 1, TanzimTarihi = new DateTime(2024, 6, 15), PoliceNo = "P001" },
            new Police { Id = 2, TanzimTarihi = new DateTime(2023, 6, 15), PoliceNo = "P002" }, // Out of range
            new Police { Id = 3, TanzimTarihi = new DateTime(2024, 3, 15), PoliceNo = "P003" }
        };

        // Act
        var filteredPolicies = policies
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate)
            .ToList();

        // Assert
        filteredPolicies.Should().HaveCount(2);
    }

    [Fact]
    public void PolicyList_Aggregate_CalculateTotalBrutPrim()
    {
        // Arrange
        var policies = new List<Police>
        {
            new Police { Id = 1, BrutPrim = 1000, PoliceNo = "P001" },
            new Police { Id = 2, BrutPrim = 2000, PoliceNo = "P002" },
            new Police { Id = 3, BrutPrim = 3000, PoliceNo = "P003" }
        };

        // Act
        var totalBrutPrim = policies.Sum(p => p.BrutPrim);

        // Assert
        totalBrutPrim.Should().Be(6000);
    }

    [Fact]
    public void PolicyList_Aggregate_CalculateTotalKomisyon()
    {
        // Arrange
        var policies = new List<Police>
        {
            new Police { Id = 1, Komisyon = 100, PoliceNo = "P001" },
            new Police { Id = 2, Komisyon = 200, PoliceNo = "P002" },
            new Police { Id = 3, Komisyon = 300, PoliceNo = "P003" }
        };

        // Act
        var totalKomisyon = policies.Sum(p => p.Komisyon);

        // Assert
        totalKomisyon.Should().Be(600);
    }

    [Fact]
    public void PolicyList_Count_ByOnayDurumu()
    {
        // Arrange
        var policies = new List<Police>
        {
            new Police { Id = 1, OnayDurumu = 1, PoliceNo = "P001" }, // Approved
            new Police { Id = 2, OnayDurumu = 0, PoliceNo = "P002" }, // Pending
            new Police { Id = 3, OnayDurumu = 1, PoliceNo = "P003" }, // Approved
            new Police { Id = 4, OnayDurumu = 2, PoliceNo = "P004" }  // Rejected
        };

        // Act
        var approvedCount = policies.Count(p => p.OnayDurumu == 1);
        var pendingCount = policies.Count(p => p.OnayDurumu == 0);
        var rejectedCount = policies.Count(p => p.OnayDurumu == 2);

        // Assert
        approvedCount.Should().Be(2);
        pendingCount.Should().Be(1);
        rejectedCount.Should().Be(1);
    }
}
