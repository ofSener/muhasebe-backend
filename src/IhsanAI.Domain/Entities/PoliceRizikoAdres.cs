using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_policerizikoadres")]
public class PoliceRizikoAdres
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("PoliceId")]
    public int PoliceId { get; set; }

    [Column("Adres")]
    [MaxLength(300)]
    public string? Adres { get; set; }

    [Column("Alan")]
    public decimal? Alan { get; set; }

    [Column("IlId")]
    public int? IlId { get; set; }

    [Column("IlceId")]
    public int? IlceId { get; set; }

    [Column("DainiMurtehinAdi")]
    [MaxLength(100)]
    public string? DainiMurtehinAdi { get; set; }

    [Column("BinaKodu")]
    [MaxLength(10)]
    public string? BinaKodu { get; set; }

    [Column("AdresNo")]
    [MaxLength(10)]
    public string? AdresNo { get; set; }

    [Column("KayitDurumu")]
    public sbyte KayitDurumu { get; set; }

    [Column("EklenmeTarihi")]
    public DateTime EklenmeTarihi { get; set; }

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    [Column("GuncelleyenKullaniciId")]
    public int GuncelleyenKullaniciId { get; set; }
}
