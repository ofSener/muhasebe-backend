using System.Linq.Expressions;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Common.Extensions;

/// <summary>
/// Multi-tenant authorization için query extension metodları.
/// Tüm veri sorgularında FirmaId kontrolü yapar.
/// </summary>
public static class QueryAuthorizationExtensions
{
    /// <summary>
    /// Poliçe sorguları için FirmaId filtresi uygular.
    /// SuperAdmin değilse sadece kendi firmasının poliçelerini görür.
    /// </summary>
    public static IQueryable<T> ApplyFirmaFilter<T>(
        this IQueryable<T> query,
        ICurrentUserService currentUser,
        Expression<Func<T, int>> firmaIdSelector)
    {
        // SuperAdmin tüm verilere erişebilir
        if (currentUser.IsSuperAdmin)
        {
            return query;
        }

        // Kullanıcının FirmaId'si yoksa boş sonuç döndür
        if (!currentUser.FirmaId.HasValue)
        {
            return query.Where(_ => false);
        }

        var userFirmaId = currentUser.FirmaId.Value;

        // Expression tree oluştur: x => firmaIdSelector(x) == userFirmaId
        var parameter = firmaIdSelector.Parameters[0];
        var body = Expression.Equal(
            firmaIdSelector.Body,
            Expression.Constant(userFirmaId));
        var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);

        return query.Where(lambda);
    }

    /// <summary>
    /// Nullable FirmaId için overload.
    /// </summary>
    public static IQueryable<T> ApplyFirmaFilterNullable<T>(
        this IQueryable<T> query,
        ICurrentUserService currentUser,
        Expression<Func<T, int?>> firmaIdSelector)
    {
        // SuperAdmin tüm verilere erişebilir
        if (currentUser.IsSuperAdmin)
        {
            return query;
        }

        // Kullanıcının FirmaId'si yoksa boş sonuç döndür
        if (!currentUser.FirmaId.HasValue)
        {
            return query.Where(_ => false);
        }

        var userFirmaId = currentUser.FirmaId.Value;

        // Expression tree oluştur: x => firmaIdSelector(x) == userFirmaId
        var parameter = firmaIdSelector.Parameters[0];
        var body = Expression.Equal(
            firmaIdSelector.Body,
            Expression.Convert(Expression.Constant(userFirmaId), typeof(int?)));
        var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);

        return query.Where(lambda);
    }

    /// <summary>
    /// Şube bazlı filtreleme için extension.
    /// SubeId kontrolü yapar (opsiyonel).
    /// Ana Yönetici (AnaYoneticimi = 0) tüm şubeleri görebilir.
    /// </summary>
    public static IQueryable<T> ApplySubeFilter<T>(
        this IQueryable<T> query,
        ICurrentUserService currentUser,
        Expression<Func<T, int>> subeIdSelector)
    {
        // Ana Yönetici firma içindeki tüm şubeleri görebilir
        if (currentUser.IsAnaYonetici)
        {
            return query;
        }

        // GorebilecegiPoliceler = "2" ise sadece kendi şubesini görür
        if (currentUser.GorebilecegiPoliceler == "2" && currentUser.SubeId.HasValue)
        {
            var userSubeId = currentUser.SubeId.Value;

            var parameter = subeIdSelector.Parameters[0];
            var body = Expression.Equal(
                subeIdSelector.Body,
                Expression.Constant(userSubeId));
            var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);

            return query.Where(lambda);
        }

        return query;
    }

    /// <summary>
    /// Nullable şube bazlı filtreleme için extension.
    /// SubeId kontrolü yapar (opsiyonel).
    /// Ana Yönetici (AnaYoneticimi = 0) tüm şubeleri görebilir.
    /// </summary>
    public static IQueryable<T> ApplySubeFilterNullable<T>(
        this IQueryable<T> query,
        ICurrentUserService currentUser,
        Expression<Func<T, int?>> subeIdSelector)
    {
        // Ana Yönetici firma içindeki tüm şubeleri görebilir
        if (currentUser.IsAnaYonetici)
        {
            return query;
        }

        if (!currentUser.SubeId.HasValue) return query;

        var userSubeId = currentUser.SubeId.Value;
        var parameter = subeIdSelector.Parameters[0];
        var body = Expression.Equal(
            subeIdSelector.Body,
            Expression.Convert(Expression.Constant(userSubeId), typeof(int?)));
        var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);

        return query.Where(lambda);
    }

    /// <summary>
    /// Müşteri erişim filtresi uygular.
    /// GorebilecegiPoliceler değerine göre firma veya şube bazlı filtre uygular.
    /// Ana Yönetici (AnaYoneticimi = 0) firma içindeki tüm müşterileri görebilir.
    /// "1" = Firma yöneticisi - tüm firmadaki müşteriler
    /// "2" = Şube çalışanı - sadece şubedeki müşteriler
    /// </summary>
    public static IQueryable<T> ApplyMusteriAccessFilter<T>(
        this IQueryable<T> query,
        ICurrentUserService currentUser,
        Expression<Func<T, int?>> firmaIdSelector,
        Expression<Func<T, int?>> subeIdSelector)
    {
        // Firma filtresi her zaman uygulanır (Ana Yönetici dahil)
        query = query.ApplyFirmaFilterNullable(currentUser, firmaIdSelector);

        // Ana Yönetici firma içindeki tüm şubeleri görebilir
        if (currentUser.IsAnaYonetici)
        {
            return query;
        }

        // gorebilecegiPoliceler = "2" ise sadece şube müşterilerini gör
        if (currentUser.GorebilecegiPoliceler == "2" && currentUser.SubeId.HasValue)
        {
            query = query.ApplySubeFilterNullable(currentUser, subeIdSelector);
        }
        // gorebilecegiPoliceler = "1" ise firma müşterilerini gör (zaten uygulandı)

        return query;
    }
}
