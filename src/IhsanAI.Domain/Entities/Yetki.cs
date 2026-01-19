using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_yetkiler")]
public class Yetki
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("FirmaId")]
    public int? FirmaId { get; set; }

    [Column("EkleyenUyeID")]
    public int EkleyenUyeId { get; set; }

    [Column("YetkiAdi")]
    [MaxLength(150)]
    public string? YetkiAdi { get; set; }

    [Column("KayitTarihi")]
    public DateTime? KayitTarihi { get; set; }

    [Column("GuncellemeTarihi")]
    public DateTime? GuncellemeTarihi { get; set; }

    [Column("GorebilecegiPolicelerveKartlar")]
    [MaxLength(1)]
    public string? GorebilecegiPolicelerveKartlar { get; set; }

    [Column("PoliceYakalamaSecenekleri")]
    [MaxLength(1)]
    public string? PoliceYakalamaSecenekleri { get; set; }

    [Column("ProduktorleriGorebilsin")]
    [MaxLength(1)]
    public string? ProduktorleriGorebilsin { get; set; }

    [Column("PoliceDuzenleyebilsin")]
    [MaxLength(1)]
    public string? PoliceDuzenleyebilsin { get; set; }

    [Column("PoliceDosyalarinaErisebilsin")]
    [MaxLength(1)]
    public string? PoliceDosyalarinaErisebilsin { get; set; }

    [Column("PoliceAktarabilsin")]
    [MaxLength(1)]
    public string? PoliceAktarabilsin { get; set; }

    [Column("PoliceHavuzunuGorebilsin")]
    [MaxLength(1)]
    public string? PoliceHavuzunuGorebilsin { get; set; }

    [Column("YetkilerSayfasindaIslemYapabilsin")]
    [MaxLength(1)]
    public string? YetkilerSayfasindaIslemYapabilsin { get; set; }

    [Column("AcenteliklerSayfasindaIslemYapabilsin")]
    [MaxLength(1)]
    public string? AcenteliklerSayfasindaIslemYapabilsin { get; set; }

    [Column("KomisyonOranlariniDuzenleyebilsin")]
    [MaxLength(1)]
    public string? KomisyonOranlariniDuzenleyebilsin { get; set; }

    [Column("AcenteliklereGorePoliceYakalansin")]
    [MaxLength(1)]
    public string? AcenteliklereGorePoliceYakalansin { get; set; }
}
