using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using IhsanAI.Application.Features.Auth.Commands;
using IhsanAI.Application.Tests.Common;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Tests.Commands.Auth;

/// <summary>
/// Unit tests for LoginCommand and LoginCommandHandler
/// </summary>
public class LoginCommandTests : TestBase
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandTests()
    {
        _configurationMock = new Mock<IConfiguration>();

        // Setup JWT configuration
        var jwtSectionMock = new Mock<IConfigurationSection>();
        jwtSectionMock.Setup(x => x["SecretKey"]).Returns("TestSecretKey-For-Unit-Testing-Minimum-32-Characters");
        jwtSectionMock.Setup(x => x["Issuer"]).Returns("TestIssuer");
        jwtSectionMock.Setup(x => x["Audience"]).Returns("TestAudience");
        jwtSectionMock.Setup(x => x["ExpirationInMinutes"]).Returns("120");

        _configurationMock.Setup(x => x.GetSection("Jwt")).Returns(jwtSectionMock.Object);

        _handler = new LoginCommandHandler(
            DbContextMock.Object,
            _configurationMock.Object,
            DateTimeServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_ReturnsFailure()
    {
        // Arrange
        var command = new LoginCommand("", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("E-posta ve parola gereklidir");
    }

    [Fact]
    public async Task Handle_WithEmptyPassword_ReturnsFailure()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("E-posta ve parola gereklidir");
    }

    [Fact]
    public async Task Handle_WithBothEmpty_ReturnsFailure()
    {
        // Arrange
        var command = new LoginCommand("", "");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("E-posta ve parola gereklidir");
    }

    [Fact]
    public async Task Handle_WithWhitespaceCredentials_ReturnsFailure()
    {
        // Arrange
        var command = new LoginCommand("   ", "   ");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("E-posta ve parola gereklidir");
    }

    [Fact]
    public async Task Handle_WithInvalidCredentials_ReturnsFailure()
    {
        // Arrange
        var users = new List<Kullanici>
        {
            CreateTestUser(email: "existing@test.com", password: "correctpassword")
        };

        var mockSet = CreateMockDbSet(users);
        DbContextMock.Setup(x => x.Kullanicilar).Returns(mockSet.Object);

        var command = new LoginCommand("wrong@test.com", "wrongpassword");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("E-posta veya parola hatalı");
    }

    [Fact]
    public async Task Handle_WithCorrectCredentialsButNotApproved_ReturnsFailure()
    {
        // Arrange
        var users = new List<Kullanici>
        {
            CreateTestUser(email: "test@test.com", password: "password123", onay: 0) // Not approved
        };

        var mockSet = CreateMockDbSet(users);
        DbContextMock.Setup(x => x.Kullanicilar).Returns(mockSet.Object);

        var command = new LoginCommand("test@test.com", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("E-posta veya parola hatalı");
    }

    [Fact]
    public async Task Handle_WithExpiredAccount_ReturnsFailure()
    {
        // Arrange
        var expiredUser = CreateTestUser(email: "expired@test.com", password: "password123");
        expiredUser.BitisTarihi = TestDateTime.AddDays(-1); // Expired yesterday

        var users = new List<Kullanici> { expiredUser };
        var mockSet = CreateMockDbSet(users);
        DbContextMock.Setup(x => x.Kullanicilar).Returns(mockSet.Object);

        var command = new LoginCommand("expired@test.com", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("E-posta veya parola hatalı");
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var testUser = CreateTestUser(
            id: 1,
            email: "test@ihsanai.com",
            password: "password123",
            name: "Test",
            surname: "User",
            firmaId: 1,
            subeId: 1,
            muhasebeYetkiId: 1
        );

        var testYetki = CreateTestYetki(id: 1, yetkiAdi: "Admin", gorebilecegiPoliceler: "1");
        var testSube = CreateTestSube(id: 1, subeAdi: "Test Sube");

        var users = new List<Kullanici> { testUser };
        var yetkiler = new List<Yetki> { testYetki };
        var subeler = new List<Sube> { testSube };

        var userMockSet = CreateMockDbSet(users);
        var yetkiMockSet = CreateMockDbSet(yetkiler);
        var subeMockSet = CreateMockDbSet(subeler);

        DbContextMock.Setup(x => x.Kullanicilar).Returns(userMockSet.Object);
        DbContextMock.Setup(x => x.Yetkiler).Returns(yetkiMockSet.Object);
        DbContextMock.Setup(x => x.Subeler).Returns(subeMockSet.Object);
        DbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("test@ihsanai.com", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Giriş başarılı");
        result.Token.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().BeGreaterThan(0);
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(1);
        result.User.Email.Should().Be("test@ihsanai.com");
        result.User.Name.Should().Be("Test User");
        result.User.Role.Should().Be("admin");
    }

    [Fact]
    public async Task Handle_WithValidCredentials_UpdatesLastLoginTime()
    {
        // Arrange
        var testUser = CreateTestUser(email: "test@ihsanai.com", password: "password123");
        var originalLoginTime = testUser.SonGirisZamani;

        var users = new List<Kullanici> { testUser };
        var yetkiler = new List<Yetki> { CreateTestYetki() };
        var subeler = new List<Sube> { CreateTestSube() };

        DbContextMock.Setup(x => x.Kullanicilar).Returns(CreateMockDbSet(users).Object);
        DbContextMock.Setup(x => x.Yetkiler).Returns(CreateMockDbSet(yetkiler).Object);
        DbContextMock.Setup(x => x.Subeler).Returns(CreateMockDbSet(subeler).Object);
        DbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("test@ihsanai.com", "password123");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        testUser.SonGirisZamani.Should().Be(TestDateTime);
        testUser.Token.Should().NotBeNullOrEmpty();
        testUser.RefreshToken.Should().NotBeNullOrEmpty();
        DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUserWithoutPermissions_ReturnsViewerRole()
    {
        // Arrange
        var testUser = CreateTestUser(email: "test@ihsanai.com", password: "password123", muhasebeYetkiId: null);

        var users = new List<Kullanici> { testUser };
        var yetkiler = new List<Yetki>();
        var subeler = new List<Sube>();

        DbContextMock.Setup(x => x.Kullanicilar).Returns(CreateMockDbSet(users).Object);
        DbContextMock.Setup(x => x.Yetkiler).Returns(CreateMockDbSet(yetkiler).Object);
        DbContextMock.Setup(x => x.Subeler).Returns(CreateMockDbSet(subeler).Object);
        DbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("test@ihsanai.com", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.User!.Role.Should().Be("viewer");
        result.User.Permissions.Should().BeNull();
    }

    [Theory]
    [InlineData("1", "admin")]
    [InlineData("2", "editor")]
    [InlineData("3", "viewer")]
    [InlineData("4", "viewer")]
    [InlineData("", "viewer")]
    public async Task Handle_DeterminesCorrectRole_BasedOnPermissions(string gorebilecegiPoliceler, string expectedRole)
    {
        // Arrange
        var testUser = CreateTestUser(email: "test@ihsanai.com", password: "password123", muhasebeYetkiId: 1);
        var testYetki = CreateTestYetki(gorebilecegiPoliceler: gorebilecegiPoliceler);

        var users = new List<Kullanici> { testUser };
        var yetkiler = new List<Yetki> { testYetki };
        var subeler = new List<Sube>();

        DbContextMock.Setup(x => x.Kullanicilar).Returns(CreateMockDbSet(users).Object);
        DbContextMock.Setup(x => x.Yetkiler).Returns(CreateMockDbSet(yetkiler).Object);
        DbContextMock.Setup(x => x.Subeler).Returns(CreateMockDbSet(subeler).Object);
        DbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("test@ihsanai.com", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.User!.Role.Should().Be(expectedRole);
    }

    [Fact]
    public async Task Handle_IncludesSubeAdi_WhenUserHasSube()
    {
        // Arrange
        var testUser = CreateTestUser(email: "test@ihsanai.com", password: "password123", subeId: 5);
        var testSube = new Sube { Id = 5, SubeAdi = "Istanbul Sube" };

        var users = new List<Kullanici> { testUser };
        var yetkiler = new List<Yetki> { CreateTestYetki() };
        var subeler = new List<Sube> { testSube };

        DbContextMock.Setup(x => x.Kullanicilar).Returns(CreateMockDbSet(users).Object);
        DbContextMock.Setup(x => x.Yetkiler).Returns(CreateMockDbSet(yetkiler).Object);
        DbContextMock.Setup(x => x.Subeler).Returns(CreateMockDbSet(subeler).Object);
        DbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("test@ihsanai.com", "password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.User!.SubeId.Should().Be(5);
        result.User.SubeAdi.Should().Be("Istanbul Sube");
    }
}
