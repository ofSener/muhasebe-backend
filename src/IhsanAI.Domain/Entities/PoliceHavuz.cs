using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_policehavuz")]
public class PoliceHavuz
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("PoliceTipi")]
    [MaxLength(50)]
    public string PoliceTipi { get; set; } = string.Empty;

    [Column("PoliceNo")]
    [MaxLength(50)]
    public string PoliceNo { get; set; } = string.Empty;

    [Column("Plaka")]
    [MaxLength(11)]
    public string Plaka { get; set; } = string.Empty;

    [Column("ZeyilNo")]
    public int ZeyilNo { get; set; }

    [Column("YenilemeNo")]
    public sbyte? YenilemeNo { get; set; }

    [Column("SigortaSirketiId")]
    public int SigortaSirketiId { get; set; }

    [Column("TanzimTarihi")]
    public DateTime TanzimTarihi { get; set; }

    [Column("BaslangicTarihi")]
    public DateTime BaslangicTarihi { get; set; }

    [Column("BitisTarihi")]
    public DateTime BitisTarihi { get; set; }

    [Column("SigortaEttirenId")]
    public int? SigortaEttirenId { get; set; }

    [Column("BrutPrim")]
    public decimal BrutPrim { get; set; }

    [Column("NetPrim")]
    public decimal NetPrim { get; set; }

    [Column("Vergi")]
    public decimal Vergi { get; set; }

    [Column("Komisyon")]
    public decimal Komisyon { get; set; }

    [Column("BransId")]
    public int BransId { get; set; }

    [Column("DisPolice")]
    public sbyte DisPolice { get; set; }

    [Column("MusteriID")]
    public int? MusteriId { get; set; }

    [Column("TcKimlikNo")]
    [MaxLength(11)]
    public string? TcKimlikNo { get; set; }

    [Column("VergiNo")]
    [MaxLength(10)]
    public string? VergiNo { get; set; }

    [Column("PoliceTespitKaynakId")]
    public int PoliceTespitKaynakId { get; set; }

    [Column("IsOrtagiFirmaId")]
    public int IsOrtagiFirmaId { get; set; }

    [Column("IsOrtagiSubeId")]
    public int IsOrtagiSubeId { get; set; }

    [Column("IsOrtagiUyeId")]
    public int IsOrtagiUyeId { get; set; }

    [Column("IsOrtagiKomisyonOrani")]
    public decimal IsOrtagiKomisyonOrani { get; set; }

    [Column("IsOrtagiKomisyon")]
    public decimal IsOrtagiKomisyon { get; set; }

    [Column("IsOrtagiEslestirmeKriteri")]
    public int IsOrtagiEslestirmeKriteri { get; set; }

    [Column("IsOrtagiOnayDurumu")]
    public bool? IsOrtagiOnayDurumu { get; set; }

    [Column("KaynakDosyaID")]
    public int KaynakDosyaId { get; set; }

    [Column("KayitDurumu")]
    public sbyte KayitDurumu { get; set; }

    [Column("EklenmeTarihi")]
    public DateTime EklenmeTarihi { get; set; }

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    [Column("GuncelleyenKullaniciId")]
    public int GuncelleyenKullaniciId { get; set; }

    [Column("Kur")]
    public decimal? Kur { get; set; }

    [Column("Aciklama")]
    public string? Aciklama { get; set; }

    [Column("TahsilatAciklamasi")]
    public string? TahsilatAciklamasi { get; set; }

    [Column("PoliceKesenPersonel")]
    [MaxLength(255)]
    public string? PoliceKesenPersonel { get; set; }

    [Column("Sube")]
    [MaxLength(255)]
    public string? Sube { get; set; }

    [Column("YenilemeDurumu")]
    [MaxLength(100)]
    public string? YenilemeDurumu { get; set; }

    [Column("UretimTuru")]
    [MaxLength(100)]
    public string? UretimTuru { get; set; }

    [Column("KayitSekli")]
    [MaxLength(100)]
    public string? KayitSekli { get; set; }

    [Column("TaksitDurumu")]
    [MaxLength(100)]
    public string? TaksitDurumu { get; set; }

    [Column("TaksitSayisi")]
    public int? TaksitSayisi { get; set; }

    [Column("OdemeTipi")]
    [MaxLength(100)]
    public string? OdemeTipi { get; set; }

    [Column("MptsDurumu")]
    [MaxLength(100)]
    public string? MptsDurumu { get; set; }

    [Column("Mutabakat")]
    [MaxLength(100)]
    public string? Mutabakat { get; set; }

    [Column("NetKazanc")]
    public decimal? NetKazanc { get; set; }

    [Column("Iskonto")]
    public decimal? Iskonto { get; set; }
}
