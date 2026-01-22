using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Constants;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.YakalananPoliceler.Queries;

public static class YakalananPoliceQueryExtensions
{
    /// <summary>
    /// Kullanıcının yetkisine göre poliçe sorgusuna filtre uygular.
    /// FirmaId ve GorebilecegiPoliceler kontrolü yapar.
    /// </summary>
    public static IQueryable<YakalananPolice> ApplyAuthorizationFilters(
        this IQueryable<YakalananPolice> query,
        ICurrentUserService currentUserService)
    {
        // Firma bazlı temel filtre
        if (currentUserService.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == currentUserService.FirmaId.Value);
        }

        // GorebilecegiPoliceler yetkisine göre filtrele
        var gorebilecegiPoliceler = currentUserService.GorebilecegiPoliceler ?? PermissionLevels.OwnPolicies;
        var userId = int.TryParse(currentUserService.UserId, out var uid) ? uid : 0;

        return gorebilecegiPoliceler switch
        {
            PermissionLevels.AllCompanyPolicies => query,
            PermissionLevels.BranchPolicies => currentUserService.SubeId.HasValue
                ? query.Where(x => x.SubeId == currentUserService.SubeId.Value)
                : query,
            PermissionLevels.OwnPolicies => query.Where(x => x.UyeId == userId),
            PermissionLevels.NoPolicies => query.Where(x => false),
            _ => query.Where(x => x.UyeId == userId)
        };
    }
}
