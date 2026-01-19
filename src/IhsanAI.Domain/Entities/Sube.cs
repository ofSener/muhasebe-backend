using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("sigortasubeler")]
public class Sube
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("FirmaId")]
    public int? FirmaId { get; set; }

    [Column("Tur")]
    public sbyte? Tur { get; set; }

    [Column("SubeAdi")]
    [MaxLength(150)]
    public string? SubeAdi { get; set; }

    [Column("IlIlce")]
    [MaxLength(150)]
    public string? IlIlce { get; set; }

    [Column("KayitTarihi")]
    public DateTime? KayitTarihi { get; set; }

    [Column("BitisTarihi")]
    public DateTime? BitisTarihi { get; set; }

    [Column("GuncellemeTarihi")]
    public DateTime? GuncellemeTarihi { get; set; }

    [Column("YetkiliAdiSoyadi")]
    [MaxLength(50)]
    public string? YetkiliAdiSoyadi { get; set; }

    [Column("Telefon")]
    [MaxLength(50)]
    public string? Telefon { get; set; }

    [Column("GsmNo")]
    [MaxLength(50)]
    public string? GsmNo { get; set; }

    [Column("Onay")]
    public sbyte? Onay { get; set; }

    [Column("CaptchaKontor")]
    public int? CaptchaKontor { get; set; }

    [Column("SubeLogo")]
    [MaxLength(80)]
    public string? SubeLogo { get; set; }

    [Column("Silinmismi")]
    public sbyte? Silinmismi { get; set; }

    [Column("SilinmeTarihi")]
    public DateTime? SilinmeTarihi { get; set; }

    [Column("SilenUyeId")]
    public int? SilenUyeId { get; set; }
}
