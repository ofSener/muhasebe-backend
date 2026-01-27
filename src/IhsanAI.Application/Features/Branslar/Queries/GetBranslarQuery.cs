using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Branslar.Queries;

public record BransDto(int Id, string BransAdi);

public record GetBranslarQuery() : IRequest<List<BransDto>>;

public class GetBranslarQueryHandler : IRequestHandler<GetBranslarQuery, List<BransDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBranslarQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BransDto>> Handle(GetBranslarQuery request, CancellationToken cancellationToken)
    {
        return await _context.PoliceTurleri
            .OrderBy(x => x.Turu)
            .Select(x => new BransDto(x.Id, x.Turu ?? string.Empty))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
