using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

/// <summary>
/// Komisyon grupları - Çalışanları gruplar ve her gruba kurallar atanır
/// </summary>
[Table("muhasebe_komisyongruplari")]
public class KomisyonGrubu
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("FirmaID")]
    public int FirmaId { get; set; }

    [Column("GrupAdi")]
    [MaxLength(100)]
    public string? GrupAdi { get; set; }

    [Column("Aciklama")]
    [MaxLength(255)]
    public string? Aciklama { get; set; }

    [Column("Aktif")]
    public bool Aktif { get; set; } = true;

    [Column("EkleyenUyeID")]
    public int? EkleyenUyeId { get; set; }

    [Column("GuncelleyenUyeID")]
    public int? GuncelleyenUyeId { get; set; }

    [Column("EklenmeTarihi")]
    public DateTime EklenmeTarihi { get; set; } = DateTime.Now;

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    // Navigation properties
    public virtual ICollection<KomisyonKurali> Kurallar { get; set; } = new List<KomisyonKurali>();
    public virtual ICollection<KomisyonGrubuUyesi> Uyeler { get; set; } = new List<KomisyonGrubuUyesi>();
    public virtual ICollection<KomisyonGrubuSubesi> Subeler { get; set; } = new List<KomisyonGrubuSubesi>();
}
