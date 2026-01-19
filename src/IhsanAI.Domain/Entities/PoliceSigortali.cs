using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_policesigortali")]
public class PoliceSigortali
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("PoliceId")]
    public int PoliceId { get; set; }

    [Column("SigortaSahipId")]
    public int SigortaSahipId { get; set; }

    [Column("KayitDurumu")]
    public sbyte KayitDurumu { get; set; }

    [Column("EklenmeTarihi")]
    public DateTime EklenmeTarihi { get; set; }

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    [Column("GuncelleyenKullaniciId")]
    public int GuncelleyenKullaniciId { get; set; }
}
