namespace IhsanAI.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    int? FirmaId { get; }
    int? SubeId { get; }
    int? UyeId { get; }
    bool IsCompanyAdmin { get; }
    bool IsAuthenticated { get; }
    string? GorebilecegiPoliceler { get; }

    /// <summary>
    /// Kullanıcı SuperAdmin mi? (Tüm firmaların verilerine erişebilir)
    /// GorebilecegiPoliceler = "1" olan kullanıcılar SuperAdmin sayılır.
    /// </summary>
    bool IsSuperAdmin { get; }

    /// <summary>
    /// Kullanıcı Ana Yönetici mi? (AnaYoneticimi = 0)
    /// Ana yöneticiler tüm yetkilere sahiptir.
    /// </summary>
    bool IsAnaYonetici { get; }
}
