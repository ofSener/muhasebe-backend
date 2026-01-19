using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("sigortapoliceturleri")]
public class PoliceTuru
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("Turu")]
    [MaxLength(20)]
    public string? Turu { get; set; }
}
