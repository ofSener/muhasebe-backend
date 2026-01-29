namespace IhsanAI.Application.Features.Dashboard;

/// <summary>
/// Dashboard veri kaynağı modu
/// </summary>
public enum DashboardMode
{
    /// <summary>
    /// Onaylı poliçeler (muhasebe_policeler_v2, OnayDurumu=1)
    /// </summary>
    Onayli = 0,

    /// <summary>
    /// Yakalanan poliçeler (muhasebe_yakalananpoliceler)
    /// </summary>
    Yakalama = 1
}
