namespace IhsanAI.Application.DTOs;

public record KullaniciDto
{
    public int Id { get; init; }
    public int? FirmaId { get; init; }
    public int? SubeId { get; init; }
    public int? YetkiId { get; init; }
    public int? KullaniciTuru { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? Email { get; init; }
    public string? GsmNo { get; init; }
    public string? SabitTel { get; init; }
    public sbyte? Onay { get; init; }
    public sbyte? AnaYoneticimi { get; init; }
    public DateTime? KayitTarihi { get; init; }
    public DateTime? GuncellemeTarihi { get; init; }
    public DateTime? SonGirisZamani { get; init; }
    public string? ProfilYolu { get; init; }

    // Hesaplanan alanlar
    public string AdSoyad => $"{Adi} {Soyadi}".Trim();
    public bool Aktif => Onay == 1;
}

public record KullaniciListDto
{
    public int Id { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? Email { get; init; }
    public string? GsmNo { get; init; }
    public int? KullaniciTuru { get; init; }
    public sbyte? AnaYoneticimi { get; init; }
    public int? MuhasebeYetkiId { get; init; }
    public string? YetkiAdi { get; init; }
    public int? SubeId { get; init; }
    public string? SubeAdi { get; init; }
    public sbyte? Onay { get; init; }
    public DateTime? KayitTarihi { get; init; }
    public int PoliceSayisi { get; init; }
    public bool IsEski { get; init; } // Eski (silinen) kullanıcı mı?

    public string AdSoyad => $"{Adi} {Soyadi}".Trim();
    public bool Aktif => Onay == 1 && !IsEski;
}

public record YakalananPoliceDto
{
    public int Id { get; init; }
    public int SigortaSirketiId { get; init; }
    public string? SigortaSirketiAdi { get; init; }
    public int PoliceTuruId { get; init; }
    public string? PoliceTuruAdi { get; init; }
    public string PoliceNo { get; init; } = string.Empty;
    public string Plaka { get; init; } = string.Empty;
    public DateTime TanzimTarihi { get; init; }
    public DateTime BaslangicTarihi { get; init; }
    public DateTime BitisTarihi { get; init; }
    public decimal BrutPrim { get; init; }
    public decimal NetPrim { get; init; }
    public string? SigortaliAdi { get; init; }
    public int ProduktorId { get; init; }
    public int ProduktorSubeId { get; init; }
    public int UyeId { get; init; }
    public string? UyeAdi { get; init; }
    public int SubeId { get; init; }
    public string? SubeAdi { get; init; }
    public int FirmaId { get; init; }
    public int? MusteriId { get; init; }
    public int? CepTelefonu { get; init; }
    public int? GuncelleyenUyeId { get; init; }
    public sbyte? DisPolice { get; init; }
    public string? AcenteAdi { get; init; }
    public string AcenteNo { get; init; } = string.Empty;
    public DateTime EklenmeTarihi { get; init; }
    public DateTime? GuncellenmeTarihi { get; init; }
    public string? Aciklama { get; init; }
}
