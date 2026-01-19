using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_acentekodlari")]
public class AcenteKodu
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("SigortaSirketiId")]
    public int SigortaSirketiId { get; set; }

    [Column("AcenteKodu")]
    [MaxLength(15)]
    public string AcenteKoduDeger { get; set; } = string.Empty;

    [Column("AcenteAdi")]
    [MaxLength(70)]
    public string AcenteAdi { get; set; } = string.Empty;

    [Column("DisAcente")]
    public sbyte DisAcente { get; set; }

    [Column("FirmaId")]
    public int FirmaId { get; set; }

    [Column("UyeId")]
    public int UyeId { get; set; }

    [Column("GuncelleyenUyeId")]
    public int GuncelleyenUyeId { get; set; }

    [Column("EklenmeTarihi")]
    public DateTime? EklenmeTarihi { get; set; }

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    [Column("OtomatikEklendi")]
    [MaxLength(1)]
    public string? OtomatikEklendi { get; set; }
}
