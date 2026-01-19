using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_sigortasirketi")]
public class SigortaSirketi
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("IdEski")]
    public int? IdEski { get; set; }

    [Column("Kod")]
    [MaxLength(3)]
    public string Kod { get; set; } = "0";

    [Column("Ad")]
    [MaxLength(100)]
    public string Ad { get; set; } = string.Empty;

    [Column("SirketAdi")]
    [MaxLength(30)]
    public string? SirketAdi { get; set; }

    [Column("SirketAdiLatin")]
    [MaxLength(30)]
    public string? SirketAdiLatin { get; set; }

    [Column("KayitDurumu")]
    public sbyte KayitDurumu { get; set; }

    [Column("Faal")]
    public sbyte Faal { get; set; } = 1;

    [Column("WebServis")]
    public sbyte WebServis { get; set; }

    [Column("EklenmeTarihi")]
    public DateTime EklenmeTarihi { get; set; }

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    [Column("GuncelleyenKullaniciId")]
    public int? GuncelleyenKullaniciId { get; set; }
}
