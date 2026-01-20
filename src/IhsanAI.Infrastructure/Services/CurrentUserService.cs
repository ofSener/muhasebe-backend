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
    public bool IsCompanyAdmin => GetClaimAsBool("yetkilerSayfasindaIslemYapabilsin");
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

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
