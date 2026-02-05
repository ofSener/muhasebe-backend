using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

/// <summary>
/// Komisyon grubu şubeleri - Şubeleri komisyon gruplarına atar
/// </summary>
[Table("muhasebe_komisyongrubu_subeleri")]
public class KomisyonGrubuSubesi
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("KomisyonGrupID")]
    public int KomisyonGrupId { get; set; }

    [Column("SubeID")]
    public int SubeId { get; set; }

    [Column("FirmaID")]
    public int FirmaId { get; set; }

    [Column("EkleyenUyeID")]
    public int? EkleyenUyeId { get; set; }

    [Column("GuncelleyenUyeID")]
    public int? GuncelleyenUyeId { get; set; }

    [Column("EklenmeTarihi")]
    public DateTime EklenmeTarihi { get; set; } = DateTime.Now;

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    // Navigation property
    [ForeignKey("KomisyonGrupId")]
    public virtual KomisyonGrubu? KomisyonGrubu { get; set; }
}
