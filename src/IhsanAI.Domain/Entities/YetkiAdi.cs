using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_yetkiadlari")]
public class YetkiAdi
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("YetkiSirasi")]
    public short? YetkiSirasi { get; set; }

    [Column("YetkiAdi")]
    [MaxLength(70)]
    public string? YetkiAdiMetni { get; set; }

    [Column("YetkiAciklamasi")]
    [MaxLength(250)]
    public string? YetkiAciklamasi { get; set; }
}
