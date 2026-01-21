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
    public sbyte? Onay { get; init; }
    public DateTime? KayitTarihi { get; init; }
    public int PoliceSayisi { get; init; }
    public bool IsEski { get; init; } // Eski (silinen) kullanıcı mı?

    public string AdSoyad => $"{Adi} {Soyadi}".Trim();
    public bool Aktif => Onay == 1 && !IsEski;
}
