using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Yetkiler.Commands;

public record DeleteYetkiCommand(int Id) : IRequest<bool>;

public class DeleteYetkiCommandHandler : IRequestHandler<DeleteYetkiCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteYetkiCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteYetkiCommand request, CancellationToken cancellationToken)
    {
        var yetki = await _context.Yetkiler
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (yetki == null)
            return false;

        _context.Yetkiler.Remove(yetki);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
