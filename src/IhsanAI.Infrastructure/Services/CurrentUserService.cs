using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
    public int? FirmaId => GetClaimAsInt("firmaId");
    public int? SubeId => GetClaimAsInt("subeId");
    public int? UyeId => int.TryParse(UserId, out var id) ? id : null;
    public bool IsCompanyAdmin => GetClaimAsBool("yetkilerSayfasindaIslemYapabilsin");
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    public string? GorebilecegiPoliceler => _httpContextAccessor.HttpContext?.User?.FindFirst("gorebilecegiPoliceler")?.Value;

    /// <summary>
    /// SuperAdmin kontrolü - şu an için false.
    /// Gelecekte tüm firmaları görebilen bir süper admin rolü eklenebilir.
    /// </summary>
    public bool IsSuperAdmin => false;

    /// <summary>
    /// Ana Yönetici kontrolü - AnaYoneticimi = 0 olan kullanıcılar.
    /// Ana yöneticiler KENDI FİRMASININ tüm verilerine erişebilir.
    /// Firma filtresi uygulanır, ama şube ve yetki filtreleri bypass edilir.
    /// </summary>
    public bool IsAnaYonetici
    {
        get
        {
            var anaYoneticimi = _httpContextAccessor.HttpContext?.User?.FindFirst("anaYoneticimi")?.Value;
            return anaYoneticimi == "0"; // 0 ise Firma Ana Yöneticisi
        }
    }

    private int? GetClaimAsInt(string claimType)
    {
        var value = _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
        return int.TryParse(value, out var result) ? result : null;
    }

    private bool GetClaimAsBool(string claimType)
    {
        var value = _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
        return value == "1";
    }
}
