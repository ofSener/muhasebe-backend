using System.Reflection;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets
    public DbSet<YakalananPolice> YakalananPoliceler => Set<YakalananPolice>();
    public DbSet<YetkiAdi> YetkiAdlari => Set<YetkiAdi>();
    public DbSet<Yetki> Yetkiler => Set<Yetki>();
    public DbSet<Police> Policeler => Set<Police>();
    public DbSet<PoliceHavuz> PoliceHavuzlari => Set<PoliceHavuz>();
    public DbSet<PoliceTuru> PoliceTurleri => Set<PoliceTuru>();
    public DbSet<PoliceRizikoAdres> PoliceRizikoAdresleri => Set<PoliceRizikoAdres>();
    public DbSet<PoliceSigortali> PoliceSigortalilari => Set<PoliceSigortali>();
    public DbSet<Musteri> Musteriler => Set<Musteri>();
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<KullaniciEski> KullanicilarEski => Set<KullaniciEski>();
    public DbSet<SigortaSirketi> SigortaSirketleri => Set<SigortaSirketi>();
    public DbSet<Sube> Subeler => Set<Sube>();
    public DbSet<Firma> Firmalar => Set<Firma>();
    public DbSet<AcenteKodu> AcenteKodlari => Set<AcenteKodu>();
    public DbSet<KomisyonOrani> KomisyonOranlari => Set<KomisyonOrani>();
    public DbSet<FirmaDriveToken> FirmaDriveTokens => Set<FirmaDriveToken>();
    public DbSet<DriveUploadLog> DriveUploadLogs => Set<DriveUploadLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }
}
