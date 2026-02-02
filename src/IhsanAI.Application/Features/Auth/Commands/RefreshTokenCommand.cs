using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Auth.Commands;

/// <summary>
/// Refresh token ile yeni access token almak için command.
/// HttpOnly cookie'den gelen refresh token ile çalışır.
/// </summary>
public record RefreshTokenCommand(
    string RefreshToken,
    string? IpAddress = null,
    string? DeviceInfo = null
) : IRequest<RefreshTokenResponse>;

public record RefreshTokenResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? Token { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresIn { get; init; }
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeService _dateTimeService;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        IConfiguration configuration,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _configuration = configuration;
        _dateTimeService = dateTimeService;
    }

    public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return new RefreshTokenResponse
            {
                Success = false,
                Message = "Refresh token gereklidir"
            };
        }

        // Refresh token ile token kaydını bul (Muhasebe özel token tablosundan)
        var tokenRecord = await _context.MuhasebeKullaniciTokens
            .Include(t => t.Kullanici)
            .FirstOrDefaultAsync(t =>
                t.RefreshToken == request.RefreshToken &&
                t.RefreshTokenExpiry > _dateTimeService.Now &&
                t.IsActive &&
                !t.IsRevoked,
                cancellationToken);

        if (tokenRecord == null || tokenRecord.Kullanici == null)
        {
            return new RefreshTokenResponse
            {
                Success = false,
                Message = "Geçersiz veya süresi dolmuş refresh token"
            };
        }

        var kullanici = tokenRecord.Kullanici;

        // Kullanıcı hesabı aktif mi kontrol et
        if (kullanici.Onay != 1 || (kullanici.BitisTarihi.HasValue && kullanici.BitisTarihi <= _dateTimeService.Now))
        {
            // Token'ı iptal et
            tokenRecord.IsRevoked = true;
            tokenRecord.RevokedAt = _dateTimeService.Now;
            tokenRecord.RevokeReason = "Kullanıcı hesabı devre dışı";
            await _context.SaveChangesAsync(cancellationToken);

            return new RefreshTokenResponse
            {
                Success = false,
                Message = "Kullanıcı hesabı aktif değil"
            };
        }

        // Yetki bilgilerini al
        var yetki = kullanici.MuhasebeYetkiId.HasValue
            ? await _context.Yetkiler.FirstOrDefaultAsync(y => y.Id == kullanici.MuhasebeYetkiId, cancellationToken)
            : null;

        // Yeni JWT token oluştur
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT:SecretKey yapılandırılmamış!");
        var issuer = jwtSettings["Issuer"] ?? "IhsanAI";
        var audience = jwtSettings["Audience"] ?? "IhsanAI";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationInMinutes"] ?? "15"); // Kısa ömürlü: 15 dakika

        var token = GenerateJwtToken(kullanici, yetki, secretKey, issuer, audience, expirationMinutes);

        // OPTIMIZASYON: Eğer token çok yeni oluşturulduysa (son 30 saniye), yeni token oluşturma
        // Sadece LastUsedAt'ı güncelle ve aynı token'ı dön (gereksiz kayıt şişmesini önler)
        var tokenAge = _dateTimeService.Now - tokenRecord.CreatedAt;
        if (tokenAge.TotalSeconds < 30)
        {
            // Token henüz çok yeni, aynı token'ı kullan
            tokenRecord.LastUsedAt = _dateTimeService.Now;
            await _context.SaveChangesAsync(cancellationToken);

            return new RefreshTokenResponse
            {
                Success = true,
                Token = tokenRecord.AccessToken,
                RefreshToken = tokenRecord.RefreshToken,
                ExpiresIn = expirationMinutes * 60
            };
        }

        // Token rotation (güvenlik için eski token iptal edilip yeni oluşturulur)
        var newRefreshToken = GenerateRefreshToken();

        // OPTIMIZASYON: Eski token'ı database'den tamamen sil (revoke yerine DELETE)
        // İptal edilmiş token'ların kayıt şişmesini önler
        _context.MuhasebeKullaniciTokens.Remove(tokenRecord);

        // Yeni token kaydı oluştur
        var newTokenRecord = new Domain.Entities.MuhasebeKullaniciToken
        {
            KullaniciId = kullanici.Id,
            AccessToken = token,
            AccessTokenExpiry = _dateTimeService.Now.AddMinutes(expirationMinutes),
            RefreshToken = newRefreshToken,
            RefreshTokenExpiry = _dateTimeService.Now.AddDays(7),
            // Eğer yeni bilgi gelirse kullan, yoksa eskiyi koru
            DeviceInfo = request.DeviceInfo ?? tokenRecord.DeviceInfo,
            IpAddress = request.IpAddress ?? tokenRecord.IpAddress,
            IsActive = true,
            IsRevoked = false,
            CreatedAt = _dateTimeService.Now,
            LastUsedAt = _dateTimeService.Now
        };

        _context.MuhasebeKullaniciTokens.Add(newTokenRecord);
        await _context.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResponse
        {
            Success = true,
            Token = token,
            RefreshToken = newRefreshToken,
            ExpiresIn = expirationMinutes * 60
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
