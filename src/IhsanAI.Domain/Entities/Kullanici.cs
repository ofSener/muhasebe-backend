using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("sigortakullanicilist")]
public class Kullanici
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("FirmaId")]
    public int? FirmaId { get; set; }

    [Column("SubeId")]
    public int? SubeId { get; set; }

    [Column("HesapId")]
    public int? HesapId { get; set; }

    [Column("HesapYedekId")]
    public int? HesapYedekId { get; set; }

    [Column("HesapYedek3Id")]
    public int? HesapYedek3Id { get; set; }

    [Column("HesapYedek4Id")]
    public int? HesapYedek4Id { get; set; }

    [Column("YetkiId")]
    public int? YetkiId { get; set; }

    [Column("MuhasebeYetkiID")]
    public int? MuhasebeYetkiId { get; set; }

    [Column("KomisyonOraniID")]
    public int? KomisyonOraniId { get; set; }

    [Column("KomisyonId")]
    public int? KomisyonId { get; set; }

    [Column("KuralId")]
    public int? KuralId { get; set; }

    [Column("AnaYoneticimi")]
    public sbyte? AnaYoneticimi { get; set; }

    [Column("IpIstisnasiVarmi")]
    public sbyte? IpIstisnasiVarmi { get; set; }

    [Column("GeciciIPIstisnasi")]
    public sbyte? GeciciIpIstisnasi { get; set; }

    [Column("KullaniciTuru")]
    public int? KullaniciTuru { get; set; }

    [Column("EkleyenUyeId")]
    public int? EkleyenUyeId { get; set; }

    [Column("Adi")]
    [MaxLength(150)]
    public string? Adi { get; set; }

    [Column("Soyadi")]
    [MaxLength(150)]
    public string? Soyadi { get; set; }

    [Column("Cinsiyet")]
    [MaxLength(1)]
    public string? Cinsiyet { get; set; }

    [Column("Email")]
    [MaxLength(50)]
    public string? Email { get; set; }

    [Column("Parola")]
    [MaxLength(50)]
    public string? Parola { get; set; }

    [Column("FeedbackParola")]
    [MaxLength(50)]
    public string? FeedbackParola { get; set; }

    [Column("SabitTel")]
    [MaxLength(12)]
    public string? SabitTel { get; set; }

    [Column("GsmNo")]
    [MaxLength(12)]
    public string? GsmNo { get; set; }

    [Column("GsmDogrulmaKodu")]
    public int? GsmDogrulmaKodu { get; set; }

    [Column("KayitTarihi")]
    public DateTime? KayitTarihi { get; set; }

    [Column("GuncellemeTarihi")]
    public DateTime? GuncellemeTarihi { get; set; }

    [Column("BitisTarihi")]
    public DateTime? BitisTarihi { get; set; }

    [Column("SonGirisZamani")]
    public DateTime? SonGirisZamani { get; set; }

    [Column("SonGirisUyeGuid")]
    public int? SonGirisUyeGuid { get; set; }

    [Column("IpAdresi")]
    [MaxLength(50)]
    public string? IpAdresi { get; set; }

    [Column("IpAdresiEngelle")]
    [MaxLength(30)]
    public string? IpAdresiEngelle { get; set; }

    [Column("PcIdEngeli")]
    [MaxLength(500)]
    public string? PcIdEngeli { get; set; }

    [Column("PcIdGirisDurumu")]
    public sbyte? PcIdGirisDurumu { get; set; }

    [Column("WebFirmaAdi")]
    [MaxLength(30)]
    public string? WebFirmaAdi { get; set; }

    [Column("WebIlIlce")]
    [MaxLength(30)]
    public string? WebIlIlce { get; set; }

    [Column("Onay")]
    public sbyte? Onay { get; set; }

    [Column("OnaySozlesme")]
    public sbyte? OnaySozlesme { get; set; }

    [Column("PaslasmadaListelensin")]
    public sbyte? PaslasmadaListelensin { get; set; }

    [Column("SigortaOnlineAlicisi")]
    public sbyte? SigortaOnlineAlicisi { get; set; }

    [Column("LogoYolu")]
    [MaxLength(100)]
    public string? LogoYolu { get; set; }

    [Column("ProfilYolu")]
    [MaxLength(100)]
    public string? ProfilYolu { get; set; }

    [Column("IBAN")]
    [MaxLength(27)]
    public string? Iban { get; set; }

    [Column("KomisyonOrani")]
    public int? KomisyonOrani { get; set; }

    [Column("CaptchaKontor")]
    public int? CaptchaKontor { get; set; }

    [Column("Token")]
    [MaxLength(800)]
    public string? Token { get; set; }

    [Column("TokenExpiry")]
    public DateTime? TokenExpiry { get; set; }

    [Column("RefreshToken")]
    [MaxLength(255)]
    public string? RefreshToken { get; set; }

    [Column("RefreshTokenExpiry")]
    public DateTime? RefreshTokenExpiry { get; set; }
}
