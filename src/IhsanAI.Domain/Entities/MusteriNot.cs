using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_musteri_notlari")]
public class MusteriNot
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("MUSTERIID")]
    public int MusteriId { get; set; }

    [Column("ICERIK")]
    [MaxLength(2000)]
    public string Icerik { get; set; } = string.Empty;

    [Column("ONEMLIMI")]
    public bool OnemliMi { get; set; }

    [Column("EKLEYENUYEID")]
    public int? EkleyenUyeId { get; set; }

    [Column("EKLEMETARIHI")]
    public DateTime EklemeTarihi { get; set; }
}
