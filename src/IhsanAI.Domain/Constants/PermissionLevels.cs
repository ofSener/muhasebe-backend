namespace IhsanAI.Domain.Constants;

/// <summary>
/// Poliçe görüntüleme yetki seviyeleri.
/// GorebilecegiPoliceler alanında kullanılır.
/// </summary>
public static class PermissionLevels
{
    /// <summary>
    /// Tüm firma poliçelerini görebilir (Admin)
    /// </summary>
    public const string AllCompanyPolicies = "1";

    /// <summary>
    /// Sadece kendi şubesindeki poliçeleri görebilir (Editor)
    /// </summary>
    public const string BranchPolicies = "2";

    /// <summary>
    /// Sadece kendi poliçelerini görebilir (Viewer)
    /// </summary>
    public const string OwnPolicies = "3";

    /// <summary>
    /// Hiçbir poliçeyi göremez
    /// </summary>
    public const string NoPolicies = "4";
}
