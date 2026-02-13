using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DatabaseFacade Database { get; }
    DbSet<YakalananPolice> YakalananPoliceler { get; }
    DbSet<YetkiAdi> YetkiAdlari { get; }
    DbSet<Yetki> Yetkiler { get; }
    DbSet<Police> Policeler { get; }
    DbSet<PoliceHavuz> PoliceHavuzlari { get; }
    DbSet<PoliceTuru> PoliceTurleri { get; }
    DbSet<PoliceRizikoAdres> PoliceRizikoAdresleri { get; }
    DbSet<PoliceSigortali> PoliceSigortalilari { get; }
    DbSet<Musteri> Musteriler { get; }
    DbSet<Kullanici> Kullanicilar { get; }
    DbSet<KullaniciEski> KullanicilarEski { get; }
    DbSet<SigortaSirketi> SigortaSirketleri { get; }
    DbSet<Sube> Subeler { get; }
    DbSet<Firma> Firmalar { get; }
    DbSet<AcenteKodu> AcenteKodlari { get; }
    DbSet<KomisyonOrani> KomisyonOranlari { get; }
    DbSet<KomisyonGrubu> KomisyonGruplari { get; }
    DbSet<KomisyonKurali> KomisyonKurallari { get; }
    DbSet<KomisyonGrubuUyesi> KomisyonGrubuUyeleri { get; }
    DbSet<KomisyonGrubuSubesi> KomisyonGrubuSubeleri { get; }
    DbSet<FirmaDriveToken> FirmaDriveTokens { get; }
    DbSet<DriveUploadLog> DriveUploadLogs { get; }
    DbSet<Brans> Branslar { get; }
    DbSet<MuhasebeKullaniciToken> MuhasebeKullaniciTokens { get; }
    DbSet<MusteriNot> MusteriNotlari { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
