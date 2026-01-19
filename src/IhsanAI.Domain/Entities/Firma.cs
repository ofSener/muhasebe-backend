using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("sigortafirmalist")]
public class Firma
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("UstId")]
    public int? UstId { get; set; }

    [Column("Tur")]
    public sbyte? Tur { get; set; }

    [Column("FirmaAdi")]
    [MaxLength(150)]
    public string? FirmaAdi { get; set; }

    [Column("FirmaAciklamasi")]
    [MaxLength(150)]
    public string? FirmaAciklamasi { get; set; }

    [Column("WebDomain")]
    [MaxLength(50)]
    public string? WebDomain { get; set; }

    [Column("Ililce")]
    [MaxLength(150)]
    public string? IlIlce { get; set; }

    [Column("KayitTarihi")]
    public DateTime? KayitTarihi { get; set; }

    [Column("BitisTarihi")]
    public DateTime? BitisTarihi { get; set; }

    [Column("GuncellemeTarihi")]
    public DateTime? GuncellemeTarihi { get; set; }

    [Column("YetkiliAdiSoyadi")]
    [MaxLength(50)]
    public string? YetkiliAdiSoyadi { get; set; }

    [Column("Telefon")]
    [MaxLength(50)]
    public string? Telefon { get; set; }

    [Column("GsmNo")]
    [MaxLength(50)]
    public string? GsmNo { get; set; }

    [Column("Onay")]
    public sbyte? Onay { get; set; }

    [Column("SatinAldiMi")]
    public int SatinAldiMi { get; set; }

    [Column("SozlesmeDurumu")]
    public sbyte SozlesmeDurumu { get; set; }

    [Column("AcentaSirketi")]
    [MaxLength(80)]
    public string? AcentaSirketi { get; set; }

    [Column("AcentaKodu")]
    [MaxLength(20)]
    public string? AcentaKodu { get; set; }

    [Column("MaksimumKullaniciSayisi")]
    public int MaksimumKullaniciSayisi { get; set; }

    [Column("SatinAlinanMasaustuKullanicisi")]
    public int SatinAlinanMasaustuKullanicisi { get; set; }

    [Column("MaksimumWebKullaniciSayisi")]
    public int MaksimumWebKullaniciSayisi { get; set; }

    [Column("SatinAlinanWebKullanicisi")]
    public int SatinAlinanWebKullanicisi { get; set; }

    [Column("MaksimumSirketSayisi")]
    public int MaksimumSirketSayisi { get; set; }

    [Column("SatinAlinanTrafikSirketiSayisi")]
    public int SatinAlinanTrafikSirketiSayisi { get; set; }

    [Column("MaksimumKaskoSirketSayisi")]
    public int MaksimumKaskoSirketSayisi { get; set; }

    [Column("SatinAlinanKaskoSirketiSayisi")]
    public int SatinAlinanKaskoSirketiSayisi { get; set; }

    [Column("MaksimumSaglikSirketSayisi")]
    public int MaksimumSaglikSirketSayisi { get; set; }

    [Column("SatinAlinanSaglikSirketiSayisi")]
    public int SatinAlinanSaglikSirketiSayisi { get; set; }

    [Column("FirmaLogosu")]
    [MaxLength(50)]
    public string? FirmaLogosu { get; set; }

    [Column("CaptchaKontor")]
    public int? CaptchaKontor { get; set; }

    [Column("SMSSirketi")]
    [MaxLength(50)]
    public string? SmsSirketi { get; set; }

    [Column("SMSKullaniciAdi")]
    [MaxLength(50)]
    public string? SmsKullaniciAdi { get; set; }

    [Column("SMSParola")]
    [MaxLength(50)]
    public string? SmsParola { get; set; }

    [Column("SMSBaslik")]
    [MaxLength(50)]
    public string? SmsBaslik { get; set; }

    [Column("SMSAboneNo")]
    [MaxLength(12)]
    public string? SmsAboneNo { get; set; }

    [Column("FeedbackLink")]
    [MaxLength(20)]
    public string? FeedbackLink { get; set; }

    [Column("FeedbackMainFirmId")]
    public int? FeedbackMainFirmId { get; set; }

    [Column("VarsayilanMuhasebeYetkiId")]
    public int? VarsayilanMuhasebeYetkiId { get; set; }

    [Column("WhatsAppAPIURL")]
    [MaxLength(100)]
    public string? WhatsAppApiUrl { get; set; }

    [Column("PoliceTalepleriEmaili")]
    [MaxLength(100)]
    public string? PoliceTalepleriEmaili { get; set; }

    [Column("PoliceTalepleriGsm")]
    [MaxLength(200)]
    public string? PoliceTalepleriGsm { get; set; }
}
