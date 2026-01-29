using Microsoft.EntityFrameworkCore;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Common.Interfaces;

public interface IApplicationDbContext
{
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
    DbSet<FirmaDriveToken> FirmaDriveTokens { get; }
    DbSet<DriveUploadLog> DriveUploadLogs { get; }
    DbSet<Brans> Branslar { get; }
    DbSet<MuhasebeKullaniciToken> MuhasebeKullaniciTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
