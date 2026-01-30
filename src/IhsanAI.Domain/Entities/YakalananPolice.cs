using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_yakalananpoliceler")]
public class YakalananPolice
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("SigortaSirketi")]
    public int SigortaSirketi { get; set; }

    [Column("PoliceTuru")]
    public int PoliceTuru { get; set; }

    [Column("PoliceNumarasi")]
    [MaxLength(25)]
    public string PoliceNumarasi { get; set; } = string.Empty;

    [Column("Plaka")]
    [MaxLength(30)]
    public string Plaka { get; set; } = string.Empty;

    [Column("TanzimTarihi")]
    public DateTime TanzimTarihi { get; set; }

    [Column("BaslangicTarihi")]
    public DateTime BaslangicTarihi { get; set; }

    [Column("BitisTarihi")]
    public DateTime BitisTarihi { get; set; }

    [Column("BrutPrim")]
    public float BrutPrim { get; set; }

    [Column("NetPrim")]
    public float NetPrim { get; set; }

    [Column("SigortaliAdi")]
    [MaxLength(70)]
    public string? SigortaliAdi { get; set; }

    [Column("ProduktorID")]
    public int ProduktorId { get; set; }

    [Column("ProduktorSubeID")]
    public int ProduktorSubeId { get; set; }

    [Column("UyeID")]
    public int UyeId { get; set; }

    [Column("SubeID")]
    public int SubeId { get; set; }

    [Column("FirmaID")]
    public int FirmaId { get; set; }

    [Column("MusteriID")]
    public int? MusteriId { get; set; }

    [Column("CepTelefonu")]
    public int? CepTelefonu { get; set; }

    [Column("GuncelleyenUyeID")]
    public int? GuncelleyenUyeId { get; set; }

    [Column("DisPolice")]
    public sbyte? DisPolice { get; set; }

    [Column("AcenteAdi")]
    [MaxLength(70)]
    public string? AcenteAdi { get; set; }

    [Column("AcenteNo")]
    [MaxLength(20)]
    public string AcenteNo { get; set; } = string.Empty;

    [Column("EklenmeTarihi")]
    public DateTime EklenmeTarihi { get; set; }

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    [Column("Aciklama")]
    [MaxLength(150)]
    public string? Aciklama { get; set; }

    [Column("isDeleted")]
    public sbyte? IsDeleted { get; set; }
}
