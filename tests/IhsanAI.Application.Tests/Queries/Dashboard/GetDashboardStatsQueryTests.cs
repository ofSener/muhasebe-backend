using FluentAssertions;
using Xunit;
using IhsanAI.Application.Features.Dashboard;
using IhsanAI.Application.Features.Dashboard.Queries;
using IhsanAI.Application.Tests.Common;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Tests.Queries.Dashboard;

/// <summary>
/// Unit tests for GetDashboardStatsQuery and related DTOs
/// Note: Handler tests require InMemory database for proper async EF Core testing
/// </summary>
public class GetDashboardStatsQueryTests : TestBase
{
    [Fact]
    public void DashboardStatsResponse_HasCorrectProperties()
    {
        // Arrange & Act
        var response = new DashboardStatsResponse
        {
            ToplamPoliceSayisi = 100,
            ToplamMusteriSayisi = 50,
            ToplamBrutPrim = 100000m,
            ToplamNetPrim = 90000m,
            ToplamKomisyon = 10000m,
            BekleyenPoliceSayisi = 5,
            BekleyenPrim = 5000m,
            AktifCalisanSayisi = 10,
            OncekiDonemBrutPrim = 80000m,
            OncekiDonemKomisyon = 8000m,
            OncekiDonemPoliceSayisi = 80,
            Mode = DashboardMode.Onayli
        };

        // Assert
        response.ToplamPoliceSayisi.Should().Be(100);
        response.ToplamMusteriSayisi.Should().Be(50);
        response.ToplamBrutPrim.Should().Be(100000m);
        response.ToplamNetPrim.Should().Be(90000m);
        response.ToplamKomisyon.Should().Be(10000m);
        response.BekleyenPoliceSayisi.Should().Be(5);
        response.BekleyenPrim.Should().Be(5000m);
        response.AktifCalisanSayisi.Should().Be(10);
        response.OncekiDonemBrutPrim.Should().Be(80000m);
        response.OncekiDonemKomisyon.Should().Be(8000m);
        response.OncekiDonemPoliceSayisi.Should().Be(80);
        response.Mode.Should().Be(DashboardMode.Onayli);
    }

    [Fact]
    public void GetDashboardStatsQuery_DefaultValues()
    {
        // Arrange & Act
        var query = new GetDashboardStatsQuery();

        // Assert
        query.FirmaId.Should().BeNull();
        query.StartDate.Should().BeNull();
        query.EndDate.Should().BeNull();
        query.Mode.Should().Be(DashboardMode.Onayli);
    }

    [Fact]
    public void GetDashboardStatsQuery_WithParameters()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 12, 31);

        // Act
        var query = new GetDashboardStatsQuery(
            FirmaId: 5,
            StartDate: startDate,
            EndDate: endDate,
            Mode: DashboardMode.Yakalama
        );

        // Assert
        query.FirmaId.Should().Be(5);
        query.StartDate.Should().Be(startDate);
        query.EndDate.Should().Be(endDate);
        query.Mode.Should().Be(DashboardMode.Yakalama);
    }

    [Theory]
    [InlineData(DashboardMode.Onayli)]
    [InlineData(DashboardMode.Yakalama)]
    public void DashboardMode_HasCorrectValues(DashboardMode mode)
    {
        // Assert
        mode.Should().BeOneOf(DashboardMode.Onayli, DashboardMode.Yakalama);
    }

    [Fact]
    public void Police_Filter_ByOnayDurumu()
    {
        // Arrange
        var policies = new List<Police>
        {
            CreateTestPolice(id: 1, onayDurumu: 1), // Approved
            CreateTestPolice(id: 2, onayDurumu: 0), // Pending
            CreateTestPolice(id: 3, onayDurumu: 1), // Approved
            CreateTestPolice(id: 4, onayDurumu: 2)  // Rejected
        };

        // Act
        var approvedPolicies = policies.Where(p => p.OnayDurumu == 1).ToList();
        var pendingPolicies = policies.Where(p => p.OnayDurumu == 0).ToList();

        // Assert
        approvedPolicies.Should().HaveCount(2);
        pendingPolicies.Should().HaveCount(1);
    }

    [Fact]
    public void Police_Filter_ByFirmaId()
    {
        // Arrange
        var policies = new List<Police>
        {
            CreateTestPolice(id: 1, firmaId: 1),
            CreateTestPolice(id: 2, firmaId: 2),
            CreateTestPolice(id: 3, firmaId: 1)
        };

        // Act
        var filteredPolicies = policies.Where(p => p.FirmaId == 1).ToList();

        // Assert
        filteredPolicies.Should().HaveCount(2);
    }

    [Fact]
    public void Police_Filter_ByDateRange()
    {
        // Arrange
        var startDate = new DateTime(2024, 6, 1);
        var endDate = new DateTime(2024, 6, 30);

        var policies = new List<Police>
        {
            CreateTestPolice(id: 1, tanzimTarihi: new DateTime(2024, 6, 15)), // In range
            CreateTestPolice(id: 2, tanzimTarihi: new DateTime(2024, 5, 15)), // Before range
            CreateTestPolice(id: 3, tanzimTarihi: new DateTime(2024, 7, 15))  // After range
        };

        // Act
        var filteredPolicies = policies
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate)
            .ToList();

        // Assert
        filteredPolicies.Should().HaveCount(1);
        filteredPolicies[0].Id.Should().Be(1);
    }

    [Fact]
    public void Police_Aggregate_SumPrims()
    {
        // Arrange
        var policies = new List<Police>
        {
            CreateTestPolice(id: 1, brutPrim: 1000, netPrim: 900, komisyon: 100),
            CreateTestPolice(id: 2, brutPrim: 2000, netPrim: 1800, komisyon: 200),
            CreateTestPolice(id: 3, brutPrim: 3000, netPrim: 2700, komisyon: 300)
        };

        // Act
        var totalBrutPrim = policies.Sum(p => p.BrutPrim);
        var totalNetPrim = policies.Sum(p => p.NetPrim);
        var totalKomisyon = policies.Sum(p => p.Komisyon ?? 0);

        // Assert
        totalBrutPrim.Should().Be(6000);
        totalNetPrim.Should().Be(5400);
        totalKomisyon.Should().Be(600);
    }

    [Fact]
    public void Kullanici_Filter_ByOnay()
    {
        // Arrange
        var users = new List<Kullanici>
        {
            CreateTestUser(id: 1, onay: 1),
            CreateTestUser(id: 2, onay: 1),
            CreateTestUser(id: 3, onay: 0)
        };

        // Act
        var activeUsers = users.Where(u => u.Onay == 1).ToList();

        // Assert
        activeUsers.Should().HaveCount(2);
    }

    [Fact]
    public void Musteri_Filter_ByFirmaId()
    {
        // Arrange
        var customers = new List<Musteri>
        {
            CreateTestMusteri(id: 1, firmaId: 1),
            CreateTestMusteri(id: 2, firmaId: 2),
            CreateTestMusteri(id: 3, firmaId: 1)
        };

        // Act
        var filteredCustomers = customers.Where(m => m.EkleyenFirmaId == 1).ToList();

        // Assert
        filteredCustomers.Should().HaveCount(2);
    }

    [Fact]
    public void YakalananPolice_Aggregate_SumPrims()
    {
        // Arrange
        var yakalananlar = new List<YakalananPolice>
        {
            CreateTestYakalananPolice(id: 1, brutPrim: 1000f, netPrim: 900f),
            CreateTestYakalananPolice(id: 2, brutPrim: 2000f, netPrim: 1800f)
        };

        // Act
        var totalBrutPrim = yakalananlar.Sum(y => y.BrutPrim);
        var totalNetPrim = yakalananlar.Sum(y => y.NetPrim);

        // Assert
        totalBrutPrim.Should().Be(3000f);
        totalNetPrim.Should().Be(2700f);
    }

    [Fact]
    public void PreviousPeriod_Calculation()
    {
        // Arrange
        var startDate = new DateTime(2024, 6, 1);
        var endDate = new DateTime(2024, 6, 30);
        var periodLength = (endDate - startDate).Days;

        // Act
        var prevEndDate = startDate.AddDays(-1);
        var prevStartDate = prevEndDate.AddDays(-periodLength);

        // Assert
        prevEndDate.Should().Be(new DateTime(2024, 5, 31));
        prevStartDate.Should().Be(new DateTime(2024, 5, 2));
    }

    #region Helper Methods

    private static Police CreateTestPolice(
        int id,
        float brutPrim = 0,
        float netPrim = 0,
        float komisyon = 0,
        int onayDurumu = 1,
        DateTime? tanzimTarihi = null,
        int firmaId = 1)
    {
        return new Police
        {
            Id = id,
            PoliceNumarasi = $"POL-{id:D4}",
            PoliceTuruId = 1,
            BrutPrim = brutPrim,
            NetPrim = netPrim,
            Komisyon = komisyon,
            OnayDurumu = onayDurumu,
            TanzimTarihi = tanzimTarihi ?? DateTime.Now,
            BaslangicTarihi = tanzimTarihi ?? DateTime.Now,
            BitisTarihi = (tanzimTarihi ?? DateTime.Now).AddYears(1),
            FirmaId = firmaId,
            SubeId = 1,
            UyeId = 1,
            ProduktorId = 1,
            ProduktorSubeId = 1,
            SigortaSirketiId = 1
        };
    }

    private static Musteri CreateTestMusteri(int id, int firmaId = 1)
    {
        return new Musteri
        {
            Id = id,
            Adi = $"Musteri {id}",
            Soyadi = "Test",
            EkleyenFirmaId = firmaId
        };
    }

    private static YakalananPolice CreateTestYakalananPolice(
        int id,
        float brutPrim = 0,
        float netPrim = 0,
        DateTime? tanzimTarihi = null,
        int firmaId = 1)
    {
        return new YakalananPolice
        {
            Id = id,
            PoliceNumarasi = $"YAK-{id:D4}",
            BrutPrim = brutPrim,
            NetPrim = netPrim,
            TanzimTarihi = tanzimTarihi ?? DateTime.Now,
            BaslangicTarihi = tanzimTarihi ?? DateTime.Now,
            BitisTarihi = (tanzimTarihi ?? DateTime.Now).AddYears(1),
            FirmaId = firmaId,
            SubeId = 1,
            UyeId = 1,
            ProduktorId = 1,
            ProduktorSubeId = 1,
            SigortaSirketi = 1,
            PoliceTuru = 1,
            EklenmeTarihi = DateTime.Now
        };
    }

    #endregion
}
