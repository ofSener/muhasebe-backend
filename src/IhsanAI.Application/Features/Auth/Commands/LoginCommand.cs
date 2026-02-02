using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Auth.Commands;

// Login Request DTO
public record LoginRequest(string Email, string Password);

// Login Response DTO
public record LoginResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? Token { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresIn { get; init; }
    public UserDto? User { get; init; }
}

public record UserDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int? FirmaId { get; init; }
    public int? SubeId { get; init; }
    public string? SubeAdi { get; init; }
    public string? ProfilResmi { get; init; }
    public PermissionsDto? Permissions { get; init; }
}

public record PermissionsDto
{
    // Poliçe Yetkileri
    public string? GorebilecegiPoliceler { get; init; }
    public string? PoliceDuzenleyebilsin { get; init; }
    public string? PoliceHavuzunuGorebilsin { get; init; }
    public string? PoliceAktarabilsin { get; init; }
    public string? PoliceDosyalarinaErisebilsin { get; init; }
    public string? PoliceYakalamaSecenekleri { get; init; }

    // Yönetim Yetkileri
    public string? YetkilerSayfasindaIslemYapabilsin { get; init; }
    public string? AcenteliklerSayfasindaIslemYapabilsin { get; init; }
    public string? KomisyonOranlariniDuzenleyebilsin { get; init; }
    public string? ProduktorleriGorebilsin { get; init; }
    public string? AcenteliklereGorePoliceYakalansin { get; init; }

    // Müşteri Yetkileri
    public string? MusterileriGorebilsin { get; init; }
    public string? MusteriListesiGorebilsin { get; init; }
    public string? MusteriDetayGorebilsin { get; init; }
    public string? YenilemeTakibiGorebilsin { get; init; }

    // Finans Yetkileri
    public string? FinansSayfasiniGorebilsin { get; init; }
    public string? FinansDashboardGorebilsin { get; init; }
    public string? PoliceOdemeleriGorebilsin { get; init; }
    public string? TahsilatTakibiGorebilsin { get; init; }
    public string? FinansRaporlariGorebilsin { get; init; }

    // Entegrasyon Yetkileri
    public string? DriveEntegrasyonuGorebilsin { get; init; }
}

// Login Command
public record LoginCommand(
    string Email,
    string Password,
    string? IpAddress = null,
    string? DeviceInfo = null
) : IRequest<LoginResponse>;

// Login Command Handler
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeService _dateTimeService;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IConfiguration configuration,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _configuration = configuration;
        _dateTimeService = dateTimeService;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new LoginResponse
            {
                Success = false,
                Message = "E-posta ve parola gereklidir"
            };
        }

        // Find user by email and password
        var kullanici = await _context.Kullanicilar
            .FirstOrDefaultAsync(k =>
                k.Email == request.Email &&
                k.Parola == request.Password &&
                k.Onay == 1 &&
                (k.BitisTarihi == null || k.BitisTarihi > _dateTimeService.Now),
                cancellationToken);

        if (kullanici == null)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "E-posta veya parola hatalı"
            };
        }

        // OPTIMIZASYON: Eski token'ları database'den sil (revoke yerine DELETE)
        // Kayıt şişmesini önler, aynı cihazdan yeni giriş yapıldığında eski token'a gerek yok
        var existingTokens = await _context.MuhasebeKullaniciTokens
            .Where(t => t.KullaniciId == kullanici.Id && t.IsActive && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        if (existingTokens.Count > 0)
        {
            _context.MuhasebeKullaniciTokens.RemoveRange(existingTokens);
        }

        // Get user permissions
        var yetki = kullanici.MuhasebeYetkiId.HasValue
            ? await _context.Yetkiler.FirstOrDefaultAsync(y => y.Id == kullanici.MuhasebeYetkiId, cancellationToken)
            : null;

        // Get branch name
        var sube = kullanici.SubeId.HasValue
            ? await _context.Subeler.FirstOrDefaultAsync(s => s.Id == kullanici.SubeId, cancellationToken)
            : null;

        // Generate JWT token
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? "IhsanAI-Default-Secret-Key-For-Development-Only-32chars";
        var issuer = jwtSettings["Issuer"] ?? "IhsanAI";
        var audience = jwtSettings["Audience"] ?? "IhsanAI";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationInMinutes"] ?? "120"); // Default 2 saat

        var token = GenerateJwtToken(kullanici, yetki, secretKey, issuer, audience, expirationMinutes);
        var refreshToken = GenerateRefreshToken();

        // Update user's last login
        kullanici.SonGirisZamani = _dateTimeService.Now;

        // Yeni token kaydı oluştur (Muhasebe özel token tablosunda)
        var muhasebeToken = new Domain.Entities.MuhasebeKullaniciToken
        {
            KullaniciId = kullanici.Id,
            AccessToken = token,
            AccessTokenExpiry = _dateTimeService.Now.AddMinutes(expirationMinutes),
            RefreshToken = refreshToken,
            RefreshTokenExpiry = _dateTimeService.Now.AddDays(7),
            DeviceInfo = request.DeviceInfo,
            IpAddress = request.IpAddress,
            IsActive = true,
            IsRevoked = false,
            CreatedAt = _dateTimeService.Now,
            LastUsedAt = _dateTimeService.Now
        };

        _context.MuhasebeKullaniciTokens.Add(muhasebeToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new LoginResponse
        {
            Success = true,
            Message = "Giriş başarılı",
            Token = token,
            RefreshToken = refreshToken,
            ExpiresIn = expirationMinutes * 60, // Convert to seconds
            User = new UserDto
            {
                Id = kullanici.Id,
                Name = $"{kullanici.Adi} {kullanici.Soyadi}".Trim(),
                Email = kullanici.Email ?? string.Empty,
                FirmaId = kullanici.FirmaId,
                SubeId = kullanici.SubeId,
                SubeAdi = sube?.SubeAdi,
                ProfilResmi = kullanici.ProfilYolu,
                Permissions = yetki != null ? new PermissionsDto
                {
                    // Poliçe Yetkileri
                    GorebilecegiPoliceler = yetki.GorebilecegiPolicelerveKartlar,
                    PoliceDuzenleyebilsin = yetki.PoliceDuzenleyebilsin,
                    PoliceHavuzunuGorebilsin = yetki.PoliceHavuzunuGorebilsin,
                    PoliceAktarabilsin = yetki.PoliceAktarabilsin,
                    PoliceDosyalarinaErisebilsin = yetki.PoliceDosyalarinaErisebilsin,
                    PoliceYakalamaSecenekleri = yetki.PoliceYakalamaSecenekleri,
                    // Yönetim Yetkileri
                    YetkilerSayfasindaIslemYapabilsin = yetki.YetkilerSayfasindaIslemYapabilsin,
                    AcenteliklerSayfasindaIslemYapabilsin = yetki.AcenteliklerSayfasindaIslemYapabilsin,
                    KomisyonOranlariniDuzenleyebilsin = yetki.KomisyonOranlariniDuzenleyebilsin,
                    ProduktorleriGorebilsin = yetki.ProduktorleriGorebilsin,
                    AcenteliklereGorePoliceYakalansin = yetki.AcenteliklereGorePoliceYakalansin,
                    // Müşteri Yetkileri
                    MusterileriGorebilsin = yetki.MusterileriGorebilsin,
                    MusteriListesiGorebilsin = yetki.MusteriListesiGorebilsin,
                    MusteriDetayGorebilsin = yetki.MusteriDetayGorebilsin,
                    YenilemeTakibiGorebilsin = yetki.YenilemeTakibiGorebilsin,
                    // Finans Yetkileri
                    FinansSayfasiniGorebilsin = yetki.FinansSayfasiniGorebilsin,
                    FinansDashboardGorebilsin = yetki.FinansDashboardGorebilsin,
                    PoliceOdemeleriGorebilsin = yetki.PoliceOdemeleriGorebilsin,
                    TahsilatTakibiGorebilsin = yetki.TahsilatTakibiGorebilsin,
                    FinansRaporlariGorebilsin = yetki.FinansRaporlariGorebilsin,
                    // Entegrasyon Yetkileri
                    DriveEntegrasyonuGorebilsin = yetki.DriveEntegrasyonuGorebilsin
                } : null
            }
        };
    }

    private string GenerateJwtToken(
        Domain.Entities.Kullanici kullanici,
        Domain.Entities.Yetki? yetki,
        string secretKey,
        string issuer,
        string audience,
        int expirationMinutes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // JWT Token kimlik ve yetki bilgilerini içerir
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, kullanici.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, kullanici.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", $"{kullanici.Adi} {kullanici.Soyadi}".Trim()),
            new("firmaId", kullanici.FirmaId?.ToString() ?? ""),
            new("subeId", kullanici.SubeId?.ToString() ?? ""),
            new("anaYoneticimi", kullanici.AnaYoneticimi?.ToString() ?? "1") // 0=AnaYönetici, 1=Değil
        };

        // Yetki claim'lerini ekle (authorization policy'ler için gerekli)
        // AnaYoneticimi = 0 ise (Firma Ana Yöneticisi), yetki tablosuna gerek yok - tüm yetkiler
        if (kullanici.AnaYoneticimi == 0)
        {
            // Ana Yönetici - Firma içinde tüm yetkiler
            claims.Add(new Claim("yetkilerSayfasindaIslemYapabilsin", "1"));
            claims.Add(new Claim("acenteliklerSayfasindaIslemYapabilsin", "1"));
            claims.Add(new Claim("policeDuzenleyebilsin", "1"));
            claims.Add(new Claim("policeHavuzunuGorebilsin", "1"));
            claims.Add(new Claim("policeAktarabilsin", "1"));
            claims.Add(new Claim("komisyonOranlariniDuzenleyebilsin", "1"));
            claims.Add(new Claim("produktorleriGorebilsin", "1"));
            claims.Add(new Claim("gorebilecegiPoliceler", "1")); // Firma yöneticisi seviyesinde
            claims.Add(new Claim("kazanclarimGorebilsin", "1"));
        }
        else if (yetki != null)
        {
            // Normal kullanıcı - Yetki tablosundan al
            claims.Add(new Claim("yetkilerSayfasindaIslemYapabilsin", yetki.YetkilerSayfasindaIslemYapabilsin ?? "0"));
            claims.Add(new Claim("acenteliklerSayfasindaIslemYapabilsin", yetki.AcenteliklerSayfasindaIslemYapabilsin ?? "0"));
            claims.Add(new Claim("policeDuzenleyebilsin", yetki.PoliceDuzenleyebilsin ?? "0"));
            claims.Add(new Claim("policeHavuzunuGorebilsin", yetki.PoliceHavuzunuGorebilsin ?? "0"));
            claims.Add(new Claim("policeAktarabilsin", yetki.PoliceAktarabilsin ?? "0"));
            claims.Add(new Claim("komisyonOranlariniDuzenleyebilsin", yetki.KomisyonOranlariniDuzenleyebilsin ?? "0"));
            claims.Add(new Claim("produktorleriGorebilsin", yetki.ProduktorleriGorebilsin ?? "0"));
            claims.Add(new Claim("gorebilecegiPoliceler", yetki.GorebilecegiPolicelerveKartlar ?? "3"));
            claims.Add(new Claim("kazanclarimGorebilsin", yetki.KazanclarimGorebilsin ?? "0"));
        }
        else
        {
            // Yetki yok ve Ana Yönetici de değil - Varsayılan minimum yetkiler
            claims.Add(new Claim("yetkilerSayfasindaIslemYapabilsin", "0"));
            claims.Add(new Claim("acenteliklerSayfasindaIslemYapabilsin", "0"));
            claims.Add(new Claim("policeDuzenleyebilsin", "0"));
            claims.Add(new Claim("policeHavuzunuGorebilsin", "0"));
            claims.Add(new Claim("policeAktarabilsin", "0"));
            claims.Add(new Claim("komisyonOranlariniDuzenleyebilsin", "0"));
            claims.Add(new Claim("produktorleriGorebilsin", "0"));
            claims.Add(new Claim("gorebilecegiPoliceler", "3")); // Sadece kendi poliçeleri
            claims.Add(new Claim("kazanclarimGorebilsin", "0"));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: _dateTimeService.Now.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
