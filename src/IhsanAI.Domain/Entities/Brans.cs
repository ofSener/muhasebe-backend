using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_brans")]
public class Brans
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("Kod")]
    [MaxLength(10)]
    public string Kod { get; set; } = string.Empty;

    [Column("Ad")]
    [MaxLength(100)]
    public string Ad { get; set; } = string.Empty;

    [Column("GercekAd")]
    [MaxLength(100)]
    public string GercekAd { get; set; } = string.Empty;

    [Column("BransGrupId")]
    public int BransGrupId { get; set; }

    [Column("EkBilgiAracMi")]
    public bool EkBilgiAracMi { get; set; }

    [Column("EkBilgiRizikoAdresiMi")]
    public bool EkBilgiRizikoAdresiMi { get; set; }

    [Column("KayitDurumu")]
    public sbyte KayitDurumu { get; set; }

    [Column("EklenmeTarihi")]
    public DateTime EklenmeTarihi { get; set; }

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    [Column("GuncelleyenKullaniciId")]
    public int GuncelleyenKullaniciId { get; set; }
}
