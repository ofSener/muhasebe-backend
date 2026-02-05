using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.KomisyonGruplari.Dtos;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.KomisyonGruplari.Commands;

#region Grup CRUD

/// <summary>
/// Yeni komisyon grubu oluşturur
/// </summary>
public record CreateKomisyonGrubuCommand(
    int FirmaId,
    int EkleyenUyeId,
    KomisyonGrubuRequest Request
) : IRequest<int>;

public class CreateKomisyonGrubuCommandHandler : IRequestHandler<CreateKomisyonGrubuCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateKomisyonGrubuCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateKomisyonGrubuCommand command, CancellationToken cancellationToken)
    {
        var grup = new KomisyonGrubu
        {
            FirmaId = command.FirmaId,
            GrupAdi = command.Request.GrupAdi,
            Aciklama = command.Request.Aciklama,
            Aktif = command.Request.Aktif,
            EkleyenUyeId = command.EkleyenUyeId,
            EklenmeTarihi = DateTime.Now
        };

        _context.KomisyonGruplari.Add(grup);
        await _context.SaveChangesAsync(cancellationToken);

        return grup.Id;
    }
}

/// <summary>
/// Komisyon grubunu günceller
/// </summary>
public record UpdateKomisyonGrubuCommand(
    int Id,
    int FirmaId,
    int GuncelleyenUyeId,
    KomisyonGrubuRequest Request
) : IRequest<bool>;

public class UpdateKomisyonGrubuCommandHandler : IRequestHandler<UpdateKomisyonGrubuCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateKomisyonGrubuCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateKomisyonGrubuCommand command, CancellationToken cancellationToken)
    {
        var grup = await _context.KomisyonGruplari
            .FirstOrDefaultAsync(g => g.Id == command.Id && g.FirmaId == command.FirmaId, cancellationToken);

        if (grup == null)
            return false;

        grup.GrupAdi = command.Request.GrupAdi;
        grup.Aciklama = command.Request.Aciklama;
        grup.Aktif = command.Request.Aktif;
        grup.GuncelleyenUyeId = command.GuncelleyenUyeId;
        grup.GuncellenmeTarihi = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>
/// Komisyon grubunu siler
/// </summary>
public record DeleteKomisyonGrubuCommand(int Id, int FirmaId) : IRequest<bool>;

public class DeleteKomisyonGrubuCommandHandler : IRequestHandler<DeleteKomisyonGrubuCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteKomisyonGrubuCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteKomisyonGrubuCommand command, CancellationToken cancellationToken)
    {
        var grup = await _context.KomisyonGruplari
            .Include(g => g.Kurallar)
            .Include(g => g.Uyeler)
            .Include(g => g.Subeler)
            .FirstOrDefaultAsync(g => g.Id == command.Id && g.FirmaId == command.FirmaId, cancellationToken);

        if (grup == null)
            return false;

        // İlişkili kuralları, üyeleri ve şubeleri sil
        _context.KomisyonKurallari.RemoveRange(grup.Kurallar);
        _context.KomisyonGrubuUyeleri.RemoveRange(grup.Uyeler);
        _context.KomisyonGrubuSubeleri.RemoveRange(grup.Subeler);
        _context.KomisyonGruplari.Remove(grup);

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Kural CRUD

/// <summary>
/// Gruba yeni kural ekler
/// </summary>
public record CreateKomisyonKuraliCommand(
    int GrupId,
    int FirmaId,
    int EkleyenUyeId,
    KomisyonKuraliRequest Request
) : IRequest<int?>;

public class CreateKomisyonKuraliCommandHandler : IRequestHandler<CreateKomisyonKuraliCommand, int?>
{
    private readonly IApplicationDbContext _context;

    public CreateKomisyonKuraliCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int?> Handle(CreateKomisyonKuraliCommand command, CancellationToken cancellationToken)
    {
        // Grup kontrolü
        var grupExists = await _context.KomisyonGruplari
            .AnyAsync(g => g.Id == command.GrupId && g.FirmaId == command.FirmaId, cancellationToken);

        if (!grupExists)
            return null;

        var kural = new KomisyonKurali
        {
            KomisyonGrupId = command.GrupId,
            FirmaId = command.FirmaId,
            SigortaSirketiId = command.Request.SigortaSirketiId,
            BransId = command.Request.BransId,
            KosulAlani = command.Request.KosulAlani,
            Operator = command.Request.Operator,
            EsikDeger = command.Request.EsikDeger,
            KomisyonOrani = command.Request.KomisyonOrani,
            EkleyenUyeId = command.EkleyenUyeId,
            EklenmeTarihi = DateTime.Now
        };

        _context.KomisyonKurallari.Add(kural);
        await _context.SaveChangesAsync(cancellationToken);

        return kural.Id;
    }
}

/// <summary>
/// Kuralı günceller
/// </summary>
public record UpdateKomisyonKuraliCommand(
    int KuralId,
    int GrupId,
    int FirmaId,
    int GuncelleyenUyeId,
    KomisyonKuraliRequest Request
) : IRequest<bool>;

public class UpdateKomisyonKuraliCommandHandler : IRequestHandler<UpdateKomisyonKuraliCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateKomisyonKuraliCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateKomisyonKuraliCommand command, CancellationToken cancellationToken)
    {
        var kural = await _context.KomisyonKurallari
            .FirstOrDefaultAsync(k => k.Id == command.KuralId &&
                                      k.KomisyonGrupId == command.GrupId &&
                                      k.FirmaId == command.FirmaId, cancellationToken);

        if (kural == null)
            return false;

        kural.SigortaSirketiId = command.Request.SigortaSirketiId;
        kural.BransId = command.Request.BransId;
        kural.KosulAlani = command.Request.KosulAlani;
        kural.Operator = command.Request.Operator;
        kural.EsikDeger = command.Request.EsikDeger;
        kural.KomisyonOrani = command.Request.KomisyonOrani;
        kural.GuncelleyenUyeId = command.GuncelleyenUyeId;
        kural.GuncellenmeTarihi = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>
/// Kuralı siler
/// </summary>
public record DeleteKomisyonKuraliCommand(int KuralId, int GrupId, int FirmaId) : IRequest<bool>;

public class DeleteKomisyonKuraliCommandHandler : IRequestHandler<DeleteKomisyonKuraliCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteKomisyonKuraliCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteKomisyonKuraliCommand command, CancellationToken cancellationToken)
    {
        var kural = await _context.KomisyonKurallari
            .FirstOrDefaultAsync(k => k.Id == command.KuralId &&
                                      k.KomisyonGrupId == command.GrupId &&
                                      k.FirmaId == command.FirmaId, cancellationToken);

        if (kural == null)
            return false;

        _context.KomisyonKurallari.Remove(kural);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Üye CRUD

/// <summary>
/// Gruba üye ekler
/// </summary>
public record AddUyeToGrupCommand(
    int GrupId,
    int FirmaId,
    int EkleyenUyeId,
    int UyeId
) : IRequest<int?>;

public class AddUyeToGrupCommandHandler : IRequestHandler<AddUyeToGrupCommand, int?>
{
    private readonly IApplicationDbContext _context;

    public AddUyeToGrupCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int?> Handle(AddUyeToGrupCommand command, CancellationToken cancellationToken)
    {
        // Grup kontrolü
        var grupExists = await _context.KomisyonGruplari
            .AnyAsync(g => g.Id == command.GrupId && g.FirmaId == command.FirmaId, cancellationToken);

        if (!grupExists)
            return null;

        // Zaten ekli mi kontrol et
        var alreadyExists = await _context.KomisyonGrubuUyeleri
            .AnyAsync(u => u.KomisyonGrupId == command.GrupId && u.UyeId == command.UyeId, cancellationToken);

        if (alreadyExists)
            return null;

        var uye = new KomisyonGrubuUyesi
        {
            KomisyonGrupId = command.GrupId,
            UyeId = command.UyeId,
            FirmaId = command.FirmaId,
            EkleyenUyeId = command.EkleyenUyeId,
            EklenmeTarihi = DateTime.Now
        };

        _context.KomisyonGrubuUyeleri.Add(uye);
        await _context.SaveChangesAsync(cancellationToken);

        return uye.Id;
    }
}

/// <summary>
/// Gruptan üye çıkarır
/// </summary>
public record RemoveUyeFromGrupCommand(int GrupId, int UyeId, int FirmaId) : IRequest<bool>;

public class RemoveUyeFromGrupCommandHandler : IRequestHandler<RemoveUyeFromGrupCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RemoveUyeFromGrupCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RemoveUyeFromGrupCommand command, CancellationToken cancellationToken)
    {
        var uye = await _context.KomisyonGrubuUyeleri
            .FirstOrDefaultAsync(u => u.KomisyonGrupId == command.GrupId &&
                                      u.UyeId == command.UyeId &&
                                      u.FirmaId == command.FirmaId, cancellationToken);

        if (uye == null)
            return false;

        _context.KomisyonGrubuUyeleri.Remove(uye);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Şube CRUD

/// <summary>
/// Gruba şube ekler
/// </summary>
public record AddSubeToGrupCommand(
    int GrupId,
    int FirmaId,
    int EkleyenUyeId,
    int SubeId
) : IRequest<int?>;

public class AddSubeToGrupCommandHandler : IRequestHandler<AddSubeToGrupCommand, int?>
{
    private readonly IApplicationDbContext _context;

    public AddSubeToGrupCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int?> Handle(AddSubeToGrupCommand command, CancellationToken cancellationToken)
    {
        // Grup kontrolü
        var grupExists = await _context.KomisyonGruplari
            .AnyAsync(g => g.Id == command.GrupId && g.FirmaId == command.FirmaId, cancellationToken);

        if (!grupExists)
            return null;

        // Zaten ekli mi kontrol et
        var alreadyExists = await _context.KomisyonGrubuSubeleri
            .AnyAsync(s => s.KomisyonGrupId == command.GrupId && s.SubeId == command.SubeId, cancellationToken);

        if (alreadyExists)
            return null;

        var sube = new KomisyonGrubuSubesi
        {
            KomisyonGrupId = command.GrupId,
            SubeId = command.SubeId,
            FirmaId = command.FirmaId,
            EkleyenUyeId = command.EkleyenUyeId,
            EklenmeTarihi = DateTime.Now
        };

        _context.KomisyonGrubuSubeleri.Add(sube);
        await _context.SaveChangesAsync(cancellationToken);

        return sube.Id;
    }
}

/// <summary>
/// Gruptan şube çıkarır
/// </summary>
public record RemoveSubeFromGrupCommand(int GrupId, int SubeId, int FirmaId) : IRequest<bool>;

public class RemoveSubeFromGrupCommandHandler : IRequestHandler<RemoveSubeFromGrupCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RemoveSubeFromGrupCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RemoveSubeFromGrupCommand command, CancellationToken cancellationToken)
    {
        var sube = await _context.KomisyonGrubuSubeleri
            .FirstOrDefaultAsync(s => s.KomisyonGrupId == command.GrupId &&
                                      s.SubeId == command.SubeId &&
                                      s.FirmaId == command.FirmaId, cancellationToken);

        if (sube == null)
            return false;

        _context.KomisyonGrubuSubeleri.Remove(sube);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
