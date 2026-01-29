using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

/// <summary>
/// Muhasebe uygulamasına özel kullanıcı token yönetimi
/// </summary>
[Table("muhasebe_kullanici_tokens")]
public class MuhasebeKullaniciToken
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    /// <summary>
    /// Kullanıcı referansı (sigortakullanicilist.ID)
    /// </summary>
    [Column("KullaniciId")]
    public int KullaniciId { get; set; }

    /// <summary>
    /// JWT Access Token
    /// </summary>
    [Column("AccessToken")]
    [MaxLength(1000)]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Access token son kullanma tarihi
    /// </summary>
    [Column("AccessTokenExpiry")]
    public DateTime AccessTokenExpiry { get; set; }

    /// <summary>
    /// Refresh Token (uzun ömürlü)
    /// </summary>
    [Column("RefreshToken")]
    [MaxLength(255)]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token son kullanma tarihi
    /// </summary>
    [Column("RefreshTokenExpiry")]
    public DateTime RefreshTokenExpiry { get; set; }

    /// <summary>
    /// Token hangi cihazdan oluşturuldu (Browser User-Agent)
    /// </summary>
    [Column("DeviceInfo")]
    [MaxLength(500)]
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Token oluşturulduğu IP adresi
    /// </summary>
    [Column("IpAddress")]
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Token aktif mi?
    /// </summary>
    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Token revoke edildi mi? (Güvenlik için)
    /// </summary>
    [Column("IsRevoked")]
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// Revoke edilme nedeni
    /// </summary>
    [Column("RevokeReason")]
    [MaxLength(255)]
    public string? RevokeReason { get; set; }

    /// <summary>
    /// Token oluşturulma zamanı
    /// </summary>
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Son kullanım zamanı (her API isteğinde güncellenebilir)
    /// </summary>
    [Column("LastUsedAt")]
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Token iptal edilme zamanı
    /// </summary>
    [Column("RevokedAt")]
    public DateTime? RevokedAt { get; set; }

    // Navigation property
    [ForeignKey("KullaniciId")]
    public virtual Kullanici? Kullanici { get; set; }
}
