using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_musteriler")]
public class Musteri
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("SAHIPTURU")]
    public sbyte? SahipTuru { get; set; }

    [Column("TCKIMLIKNO")]
    [MaxLength(11)]
    public string? TcKimlikNo { get; set; }

    [Column("VERGINO")]
    [MaxLength(10)]
    public string? VergiNo { get; set; }

    [Column("TCVERGINO")]
    [MaxLength(11)]
    public string? TcVergiNo { get; set; }

    [Column("ADI")]
    [MaxLength(150)]
    public string? Adi { get; set; }

    [Column("SOYADI")]
    [MaxLength(30)]
    public string? Soyadi { get; set; }

    [Column("DOGUMYERI")]
    [MaxLength(30)]
    public string? DogumYeri { get; set; }

    [Column("DOGUMTARIHI")]
    public DateTime? DogumTarihi { get; set; }

    [Column("CINSIYET")]
    [MaxLength(10)]
    public string? Cinsiyet { get; set; }

    [Column("BABAADI")]
    [MaxLength(70)]
    public string? BabaAdi { get; set; }

    [Column("GSM")]
    [MaxLength(23)]
    public string? Gsm { get; set; }

    [Column("GSM2")]
    [MaxLength(23)]
    public string? Gsm2 { get; set; }

    [Column("TELEFON")]
    [MaxLength(23)]
    public string? Telefon { get; set; }

    [Column("EMAIL")]
    [MaxLength(40)]
    public string? Email { get; set; }

    [Column("MESLEK")]
    [MaxLength(40)]
    public string? Meslek { get; set; }

    [Column("YASADIGIIL")]
    [MaxLength(20)]
    public string? YasadigiIl { get; set; }

    [Column("YASADIGIILCE")]
    [MaxLength(20)]
    public string? YasadigiIlce { get; set; }

    [Column("ADRES")]
    [MaxLength(500)]
    public string? Adres { get; set; }

    [Column("BOY")]
    public int? Boy { get; set; }

    [Column("KILO")]
    public int? Kilo { get; set; }

    [Column("EKLEYENFIRMAID")]
    public int? EkleyenFirmaId { get; set; }

    [Column("EKLEYENUYEID")]
    public int? EkleyenUyeId { get; set; }

    [Column("EKLEYENSUBEID")]
    public int? EkleyenSubeId { get; set; }

    [Column("EKLENMEZAMANI")]
    public DateTime? EklenmeZamani { get; set; }

    [Column("GUNCELLENMEZAMANI")]
    public DateTime? GuncellenmeZamani { get; set; }

    [Column("GUNCELLEYENUYEID")]
    public int? GuncelleyenUyeId { get; set; }
}
