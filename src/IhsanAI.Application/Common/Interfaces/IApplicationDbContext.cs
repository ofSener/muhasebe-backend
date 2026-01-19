using Microsoft.EntityFrameworkCore;

namespace IhsanAI.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
