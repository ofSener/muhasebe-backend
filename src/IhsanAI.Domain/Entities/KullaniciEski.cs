using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("sigortakullanicilisteskiler")]
public class KullaniciEski
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("FirmaId")]
    public int? FirmaId { get; set; }

    [Column("SubeId")]
    public int? SubeId { get; set; }

    [Column("YetkiId")]
    public int? YetkiId { get; set; }

    [Column("KullaniciTuru")]
    public int? KullaniciTuru { get; set; }

    [Column("Adi")]
    [MaxLength(150)]
    public string? Adi { get; set; }

    [Column("Soyadi")]
    [MaxLength(150)]
    public string? Soyadi { get; set; }

    [Column("Email")]
    [MaxLength(50)]
    public string? Email { get; set; }

    [Column("GsmNo")]
    [MaxLength(12)]
    public string? GsmNo { get; set; }

    [Column("SabitTel")]
    [MaxLength(12)]
    public string? SabitTel { get; set; }

    [Column("Onay")]
    public sbyte? Onay { get; set; }

    [Column("AnaYoneticimi")]
    public sbyte? AnaYoneticimi { get; set; }

    [Column("KayitTarihi")]
    public DateTime? KayitTarihi { get; set; }

    [Column("GuncellemeTarihi")]
    public DateTime? GuncellemeTarihi { get; set; }

    [Column("SonGirisZamani")]
    public DateTime? SonGirisZamani { get; set; }

    [Column("SilinmeZamani")]
    public DateTime? SilinmeZamani { get; set; }

    [Column("ProfilYolu")]
    [MaxLength(100)]
    public string? ProfilYolu { get; set; }
}
