using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

/// <summary>
/// Komisyon kuralları - Her kural bir şirket+branş kombinasyonu için koşullu komisyon oranı tanımlar
/// SigortaSirketiId = 9999 → Varsayılan (tüm şirketler)
/// BransId = 9999 → Varsayılan (tüm branşlar)
/// </summary>
[Table("muhasebe_komisyonoranlari_v2")]
public class KomisyonKurali
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("KomisyonGrupID")]
    public int KomisyonGrupId { get; set; }

    [Column("FirmaID")]
    public int FirmaId { get; set; }

    /// <summary>
    /// Sigorta şirketi ID. 9999 = Varsayılan (tüm şirketler)
    /// </summary>
    [Column("SigortaSirketiId")]
    public int SigortaSirketiId { get; set; }

    /// <summary>
    /// Branş ID. 9999 = Varsayılan (tüm branşlar)
    /// </summary>
    [Column("BransId")]
    public int BransId { get; set; }

    /// <summary>
    /// Koşul alanı: BrutPrim, NetPrim, Komisyon
    /// </summary>
    [Column("KosulAlani")]
    [MaxLength(20)]
    public string KosulAlani { get; set; } = "NetPrim";

    /// <summary>
    /// Karşılaştırma operatörü: büyük, küçük, büyük-eşit, küçük-eşit, eşit
    /// </summary>
    [Column("Operator")]
    [MaxLength(5)]
    public string Operator { get; set; } = ">";

    /// <summary>
    /// Eşik değeri (örn: 10000)
    /// </summary>
    [Column("EsikDeger")]
    public decimal EsikDeger { get; set; } = 0;

    /// <summary>
    /// Komisyon oranı (0-100 arası yüzde)
    /// </summary>
    [Column("KomisyonOrani")]
    public byte KomisyonOrani { get; set; }

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

    /// <summary>
    /// Öncelik puanı hesaplar - Spesifik kurallar daha yüksek öncelik alır
    /// Puan 3: Spesifik Şirket + Spesifik Branş
    /// Puan 2: Spesifik Şirket + Varsayılan Branş
    /// Puan 1: Varsayılan Şirket + Spesifik Branş
    /// Puan 0: Varsayılan Şirket + Varsayılan Branş
    /// </summary>
    [NotMapped]
    public int OncelikPuani =>
        (SigortaSirketiId != 9999 ? 2 : 0) + (BransId != 9999 ? 1 : 0);

    /// <summary>
    /// Verilen değerlere göre koşulun sağlanıp sağlanmadığını kontrol eder
    /// </summary>
    public bool KosulSaglaniyorMu(decimal brutPrim, decimal netPrim, decimal komisyon)
    {
        var deger = KosulAlani switch
        {
            "BrutPrim" => brutPrim,
            "NetPrim" => netPrim,
            "Komisyon" => komisyon,
            _ => netPrim
        };

        return Operator switch
        {
            ">" => deger > EsikDeger,
            "<" => deger < EsikDeger,
            ">=" => deger >= EsikDeger,
            "<=" => deger <= EsikDeger,
            "=" => deger == EsikDeger,
            _ => true
        };
    }
}
