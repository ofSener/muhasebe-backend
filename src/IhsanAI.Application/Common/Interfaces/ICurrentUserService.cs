namespace IhsanAI.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    int? FirmaId { get; }
    int? SubeId { get; }
    bool IsCompanyAdmin { get; }
    bool IsAuthenticated { get; }
}
