namespace IhsanAI.Application.Features.KomisyonGruplari.Dtos;

/// <summary>
/// Komisyon grubu DTO
/// </summary>
public record KomisyonGrubuDto
{
    public int Id { get; init; }
    public string? GrupAdi { get; init; }
    public string? Aciklama { get; init; }
    public bool Aktif { get; init; }
    public int KuralSayisi { get; init; }
    public int UyeSayisi { get; init; }
    public int SubeSayisi { get; init; }
    public DateTime EklenmeTarihi { get; init; }
    public DateTime? GuncellenmeTarihi { get; init; }
}

/// <summary>
/// Komisyon grubu detay DTO (kurallar ve üyelerle birlikte)
/// </summary>
public record KomisyonGrubuDetayDto
{
    public int Id { get; init; }
    public string? GrupAdi { get; init; }
    public string? Aciklama { get; init; }
    public bool Aktif { get; init; }
    public DateTime EklenmeTarihi { get; init; }
    public DateTime? GuncellenmeTarihi { get; init; }
    public List<KomisyonKuraliDto> Kurallar { get; init; } = new();
    public List<KomisyonGrubuUyesiDto> Uyeler { get; init; } = new();
    public List<KomisyonGrubuSubesiDto> Subeler { get; init; } = new();
}

/// <summary>
/// Komisyon kuralı DTO
/// </summary>
public record KomisyonKuraliDto
{
    public int Id { get; init; }
    public int SigortaSirketiId { get; init; }
    public string? SigortaSirketiAdi { get; init; }
    public int BransId { get; init; }
    public string? BransAdi { get; init; }
    public string KosulAlani { get; init; } = "NetPrim";
    public string Operator { get; init; } = ">";
    public decimal EsikDeger { get; init; }
    public byte KomisyonOrani { get; init; }
    public int OncelikPuani { get; init; }
}

/// <summary>
/// Komisyon grubu üyesi DTO
/// </summary>
public record KomisyonGrubuUyesiDto
{
    public int Id { get; init; }
    public int UyeId { get; init; }
    public string? UyeAdi { get; init; }
    public DateTime EklenmeTarihi { get; init; }
}

/// <summary>
/// Grup oluşturma/güncelleme isteği
/// </summary>
public record KomisyonGrubuRequest
{
    public string? GrupAdi { get; init; }
    public string? Aciklama { get; init; }
    public bool Aktif { get; init; } = true;
}

/// <summary>
/// Kural oluşturma/güncelleme isteği
/// </summary>
public record KomisyonKuraliRequest
{
    public int SigortaSirketiId { get; init; } = 9999; // 9999 = Varsayılan
    public int BransId { get; init; } = 9999;          // 9999 = Varsayılan
    public string KosulAlani { get; init; } = "NetPrim";
    public string Operator { get; init; } = ">";
    public decimal EsikDeger { get; init; }
    public byte KomisyonOrani { get; init; }
}

/// <summary>
/// Üye ekleme isteği
/// </summary>
public record KomisyonGrubuUyeEkleRequest
{
    public int UyeId { get; init; }
}

/// <summary>
/// Komisyon grubu şubesi DTO
/// </summary>
public record KomisyonGrubuSubesiDto
{
    public int Id { get; init; }
    public int SubeId { get; init; }
    public string? SubeAdi { get; init; }
    public DateTime EklenmeTarihi { get; init; }
}

/// <summary>
/// Şube ekleme isteği
/// </summary>
public record KomisyonGrubuSubeEkleRequest
{
    public int SubeId { get; init; }
}
