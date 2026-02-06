using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Api.Endpoints;

/// <summary>
/// Simple lookup endpoints - direct DB queries without CQRS overhead.
/// These endpoints return reference data with no business logic.
/// </summary>
public static class LookupEndpoints
{
    // DTO for Branslar (projects PoliceTuru into a simpler shape)
    public record BransDto(int Id, string BransAdi);

    // DTO for branch search
    public record BranchSearchDto
    {
        public int Id { get; init; }
        public string? SubeAdi { get; init; }
        public string? IlIlce { get; init; }
        public string? YetkiliAdiSoyadi { get; init; }
    }

    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Policy Types (/api/policy-types) ──────────────────────────

        var policyTypes = app.MapGroup("/api/policy-types")
            .WithTags("Policy Types")
            .RequireAuthorization();

        policyTypes.MapGet("/", async (IApplicationDbContext db, CancellationToken ct) =>
            Results.Ok(await db.PoliceTurleri
                .OrderBy(x => x.Turu)
                .AsNoTracking()
                .ToListAsync(ct)))
            .WithName("GetPoliceTurleri")
            .WithDescription("Police turlerini listeler");

        policyTypes.MapGet("/{id:int}", async (int id, IApplicationDbContext db, CancellationToken ct) =>
        {
            var result = await db.PoliceTurleri.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
            .WithName("GetPoliceTuruById")
            .WithDescription("ID'ye gore police turu getirir");

        // ── Insurance Types / Branslar (/api/insurance-types) ─────────

        var insuranceTypes = app.MapGroup("/api/insurance-types")
            .WithTags("Insurance Types")
            .RequireAuthorization();

        insuranceTypes.MapGet("/", async (IApplicationDbContext db, CancellationToken ct) =>
            Results.Ok(await db.PoliceTurleri
                .OrderBy(x => x.Id)
                .Select(x => new BransDto(x.Id, x.Turu ?? string.Empty))
                .AsNoTracking()
                .ToListAsync(ct)))
            .WithName("GetBranslar")
            .WithDescription("Branslari listeler");

        // ── Insurance Companies (/api/insurance-companies) ────────────

        var companies = app.MapGroup("/api/insurance-companies")
            .WithTags("Insurance Companies")
            .RequireAuthorization();

        companies.MapGet("/", async (bool? sadeceFaal, IApplicationDbContext db, CancellationToken ct) =>
        {
            var query = db.SigortaSirketleri.AsQueryable();
            if (sadeceFaal == true)
                query = query.Where(x => x.Faal == 1);

            return Results.Ok(await query
                .OrderBy(x => x.Ad)
                .AsNoTracking()
                .ToListAsync(ct));
        })
            .WithName("GetSigortaSirketleri")
            .WithDescription("Sigorta sirketlerini listeler");

        companies.MapGet("/{id:int}", async (int id, IApplicationDbContext db, CancellationToken ct) =>
        {
            var result = await db.SigortaSirketleri.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
            .WithName("GetSigortaSirketiById")
            .WithDescription("ID'ye gore sigorta sirketi getirir");

        // ── Branches / Subeler (/api/branches) ───────────────────────

        var branches = app.MapGroup("/api/branches")
            .WithTags("Branches")
            .RequireAuthorization();

        branches.MapGet("/", async (int? firmaId, IApplicationDbContext db, CancellationToken ct) =>
        {
            var query = db.Subeler.AsQueryable();
            if (firmaId.HasValue)
                query = query.Where(x => x.FirmaId == firmaId.Value);

            return Results.Ok(await query
                .Where(x => x.Silinmismi != 1)
                .OrderBy(x => x.SubeAdi)
                .AsNoTracking()
                .ToListAsync(ct));
        })
            .WithName("GetSubeler")
            .WithDescription("Subeleri listeler");

        branches.MapGet("/{id:int}", async (int id, IApplicationDbContext db, CancellationToken ct) =>
        {
            var result = await db.Subeler.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
            .WithName("GetSubeById")
            .WithDescription("ID'ye gore sube getirir");

        branches.MapGet("/search", async (string name, int? firmaId, int? limit, IApplicationDbContext db, CancellationToken ct) =>
        {
            var query = db.Subeler
                .Where(x => x.Silinmismi != 1);

            if (firmaId.HasValue)
                query = query.Where(x => x.FirmaId == firmaId.Value);

            if (!string.IsNullOrWhiteSpace(name))
            {
                var searchTerm = name.ToLower();
                query = query.Where(x => x.SubeAdi != null && x.SubeAdi.ToLower().Contains(searchTerm));
            }

            return Results.Ok(await query
                .OrderBy(x => x.SubeAdi)
                .Take(limit ?? 20)
                .Select(x => new BranchSearchDto
                {
                    Id = x.Id,
                    SubeAdi = x.SubeAdi,
                    IlIlce = x.IlIlce,
                    YetkiliAdiSoyadi = x.YetkiliAdiSoyadi
                })
                .AsNoTracking()
                .ToListAsync(ct));
        })
            .WithName("SearchBranches")
            .WithDescription("Sube arama");

        // ── Companies / Firmalar (/api/companies) ─────────────────────

        var firms = app.MapGroup("/api/companies")
            .WithTags("Companies")
            .RequireAuthorization();

        firms.MapGet("/", async (bool? sadeceOnaylananlar, IApplicationDbContext db, CancellationToken ct) =>
        {
            var query = db.Firmalar.AsQueryable();
            if (sadeceOnaylananlar == true)
                query = query.Where(x => x.Onay == 1);

            return Results.Ok(await query
                .OrderBy(x => x.FirmaAdi)
                .AsNoTracking()
                .ToListAsync(ct));
        })
            .WithName("GetFirmalar")
            .WithDescription("Firmalari listeler");

        firms.MapGet("/{id:int}", async (int id, IApplicationDbContext db, CancellationToken ct) =>
        {
            var result = await db.Firmalar.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
            .WithName("GetFirmaById")
            .WithDescription("ID'ye gore firma getirir");

        // ── Commission Rates (/api/commission-rates) ──────────────────

        var commissionRates = app.MapGroup("/api/commission-rates")
            .WithTags("Commission Rates")
            .RequireAuthorization();

        commissionRates.MapGet("/", async (int? firmaId, IApplicationDbContext db, CancellationToken ct) =>
        {
            var query = db.KomisyonOranlari.AsQueryable();
            if (firmaId.HasValue)
                query = query.Where(x => x.FirmaId == firmaId.Value);

            return Results.Ok(await query
                .OrderByDescending(x => x.EklenmeTarihi)
                .AsNoTracking()
                .ToListAsync(ct));
        })
            .WithName("GetKomisyonOranlari")
            .WithDescription("Komisyon oranlarini listeler");

        commissionRates.MapGet("/{id:int}", async (int id, IApplicationDbContext db, CancellationToken ct) =>
        {
            var result = await db.KomisyonOranlari.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
            .WithName("GetKomisyonOraniById")
            .WithDescription("ID'ye gore komisyon orani getirir");

        // ── Policy Risk Addresses (/api/policy-risk-addresses) ────────

        var riskAddresses = app.MapGroup("/api/policy-risk-addresses")
            .WithTags("Policy Risk Addresses")
            .RequireAuthorization();

        riskAddresses.MapGet("/", async (int? policeId, IApplicationDbContext db, CancellationToken ct) =>
        {
            var query = db.PoliceRizikoAdresleri.AsQueryable();
            if (policeId.HasValue)
                query = query.Where(x => x.PoliceId == policeId.Value);

            return Results.Ok(await query
                .OrderByDescending(x => x.EklenmeTarihi)
                .AsNoTracking()
                .ToListAsync(ct));
        })
            .WithName("GetPoliceRizikoAdresleri")
            .WithDescription("Police riziko adreslerini listeler");

        riskAddresses.MapGet("/{id:int}", async (int id, IApplicationDbContext db, CancellationToken ct) =>
        {
            var result = await db.PoliceRizikoAdresleri.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
            .WithName("GetPoliceRizikoAdresiById")
            .WithDescription("ID'ye gore police riziko adresi getirir");

        // ── Policy Insureds (/api/policy-insureds) ────────────────────

        var insureds = app.MapGroup("/api/policy-insureds")
            .WithTags("Policy Insureds")
            .RequireAuthorization();

        insureds.MapGet("/", async (int? policeId, IApplicationDbContext db, CancellationToken ct) =>
        {
            var query = db.PoliceSigortalilari.AsQueryable();
            if (policeId.HasValue)
                query = query.Where(x => x.PoliceId == policeId.Value);

            return Results.Ok(await query
                .OrderByDescending(x => x.EklenmeTarihi)
                .AsNoTracking()
                .ToListAsync(ct));
        })
            .WithName("GetPoliceSigortalilari")
            .WithDescription("Police sigortalilarini listeler");

        insureds.MapGet("/{id:int}", async (int id, IApplicationDbContext db, CancellationToken ct) =>
        {
            var result = await db.PoliceSigortalilari.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
            .WithName("GetPoliceSigortaliById")
            .WithDescription("ID'ye gore police sigortaliyi getirir");

        return app;
    }
}
