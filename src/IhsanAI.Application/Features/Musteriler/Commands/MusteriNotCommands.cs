using System.Text.Json.Serialization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Musteriler.Commands;

// Add Note
public record AddMusteriNotCommand : IRequest<AddMusteriNotResultDto>
{
    [JsonIgnore]
    public int MusteriId { get; init; }

    [JsonPropertyName("icerik")]
    public string Icerik { get; init; } = string.Empty;

    [JsonPropertyName("onemliMi")]
    public bool OnemliMi { get; init; }
}

public record AddMusteriNotResultDto
{
    public bool Success { get; init; }
    public int? NotId { get; init; }
    public string? ErrorMessage { get; init; }
}

public class AddMusteriNotCommandHandler : IRequestHandler<AddMusteriNotCommand, AddMusteriNotResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AddMusteriNotCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<AddMusteriNotResultDto> Handle(AddMusteriNotCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Icerik))
        {
            return new AddMusteriNotResultDto
            {
                Success = false,
                ErrorMessage = "Not içeriği boş olamaz."
            };
        }

        // Müşterinin varlığını ve erişim yetkisini kontrol et
        var musteriExists = await _context.Musteriler
            .AnyAsync(m => m.Id == request.MusteriId, cancellationToken);

        if (!musteriExists)
        {
            return new AddMusteriNotResultDto
            {
                Success = false,
                ErrorMessage = "Müşteri bulunamadı."
            };
        }

        var not = new MusteriNot
        {
            MusteriId = request.MusteriId,
            Icerik = request.Icerik.Trim(),
            OnemliMi = request.OnemliMi,
            EkleyenUyeId = _currentUserService.UyeId,
            EklemeTarihi = DateTime.UtcNow
        };

        _context.MusteriNotlari.Add(not);
        await _context.SaveChangesAsync(cancellationToken);

        return new AddMusteriNotResultDto
        {
            Success = true,
            NotId = not.Id
        };
    }
}

// Delete Note
public record DeleteMusteriNotCommand(int MusteriId, int NotId) : IRequest<DeleteMusteriNotResultDto>;

public record DeleteMusteriNotResultDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public class DeleteMusteriNotCommandHandler : IRequestHandler<DeleteMusteriNotCommand, DeleteMusteriNotResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteMusteriNotCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<DeleteMusteriNotResultDto> Handle(DeleteMusteriNotCommand request, CancellationToken cancellationToken)
    {
        var not = await _context.MusteriNotlari
            .FirstOrDefaultAsync(n => n.Id == request.NotId && n.MusteriId == request.MusteriId, cancellationToken);

        if (not == null)
        {
            return new DeleteMusteriNotResultDto
            {
                Success = false,
                ErrorMessage = "Not bulunamadı."
            };
        }

        _context.MusteriNotlari.Remove(not);
        await _context.SaveChangesAsync(cancellationToken);

        return new DeleteMusteriNotResultDto
        {
            Success = true
        };
    }
}
