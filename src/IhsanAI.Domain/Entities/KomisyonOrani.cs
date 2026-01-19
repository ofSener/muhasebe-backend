using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_komisyonoranlari")]
public class KomisyonOrani
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("EkleyenUyeID")]
    public int EkleyenUyeId { get; set; }

    [Column("GuncelleyenUyeID")]
    public int GuncelleyenUyeId { get; set; }

    [Column("FirmaID")]
    public int FirmaId { get; set; }

    [Column("EklenmeTarihi")]
    public DateTime EklenmeTarihi { get; set; }

    [Column("GuncellenmeTarihi")]
    public DateTime? GuncellenmeTarihi { get; set; }

    // Acn Türk
    [Column("AcnTurkTrafikKomisyon")]
    public sbyte? AcnTurkTrafikKomisyon { get; set; }
    [Column("AcnTurkKaskoKomisyon")]
    public sbyte? AcnTurkKaskoKomisyon { get; set; }
    [Column("AcnTurkDaskKomisyon")]
    public sbyte? AcnTurkDaskKomisyon { get; set; }
    [Column("AcnTurkFerdiKazaKomisyon")]
    public sbyte? AcnTurkFerdiKazaKomisyon { get; set; }
    [Column("AcnTurkKoltukKomisyon")]
    public sbyte? AcnTurkKoltukKomisyon { get; set; }
    [Column("AcnTurkKonutKomisyon")]
    public sbyte? AcnTurkKonutKomisyon { get; set; }
    [Column("AcnTurkNakliyatKomisyon")]
    public sbyte? AcnTurkNakliyatKomisyon { get; set; }
    [Column("AcnTurkSeyahatSaglikKomisyon")]
    public sbyte? AcnTurkSeyahatSaglikKomisyon { get; set; }
    [Column("AcnTurkTamamlayiciSaglikKomisyon")]
    public sbyte? AcnTurkTamamlayiciSaglikKomisyon { get; set; }
    [Column("AcnTurkYabanciSaglikKomisyon")]
    public sbyte? AcnTurkYabanciSaglikKomisyon { get; set; }
    [Column("AcnTurkImmKomisyon")]
    public sbyte? AcnTurkImmKomisyon { get; set; }

    // Ak Sigorta
    [Column("AkTrafikKomisyon")]
    public sbyte? AkTrafikKomisyon { get; set; }
    [Column("AkKaskoKomisyon")]
    public sbyte? AkKaskoKomisyon { get; set; }
    [Column("AkDaskKomisyon")]
    public sbyte? AkDaskKomisyon { get; set; }
    [Column("AkFerdiKazaKomisyon")]
    public sbyte? AkFerdiKazaKomisyon { get; set; }
    [Column("AkKoltukKomisyon")]
    public sbyte? AkKoltukKomisyon { get; set; }
    [Column("AkKonutKomisyon")]
    public sbyte? AkKonutKomisyon { get; set; }
    [Column("AkNakliyatKomisyon")]
    public sbyte? AkNakliyatKomisyon { get; set; }
    [Column("AkSeyahatSaglikKomisyon")]
    public sbyte? AkSeyahatSaglikKomisyon { get; set; }
    [Column("AkTamamlayiciSaglikKomisyon")]
    public sbyte? AkTamamlayiciSaglikKomisyon { get; set; }
    [Column("AkYabanciSaglikKomisyon")]
    public sbyte? AkYabanciSaglikKomisyon { get; set; }
    [Column("AkImmKomisyon")]
    public sbyte? AkImmKomisyon { get; set; }

    // Allianz
    [Column("AllianzTrafikKomisyon")]
    public sbyte? AllianzTrafikKomisyon { get; set; }
    [Column("AllianzKaskoKomisyon")]
    public sbyte? AllianzKaskoKomisyon { get; set; }
    [Column("AllianzDaskKomisyon")]
    public sbyte? AllianzDaskKomisyon { get; set; }
    [Column("AllianzFerdiKazaKomisyon")]
    public sbyte? AllianzFerdiKazaKomisyon { get; set; }
    [Column("AllianzKoltukKomisyon")]
    public sbyte? AllianzKoltukKomisyon { get; set; }
    [Column("AllianzKonutKomisyon")]
    public sbyte? AllianzKonutKomisyon { get; set; }
    [Column("AllianzNakliyatKomisyon")]
    public sbyte? AllianzNakliyatKomisyon { get; set; }
    [Column("AllianzSeyahatSaglikKomisyon")]
    public sbyte? AllianzSeyahatSaglikKomisyon { get; set; }
    [Column("AllianzTamamlayiciSaglikKomisyon")]
    public sbyte? AllianzTamamlayiciSaglikKomisyon { get; set; }
    [Column("AllianzYabanciSaglikKomisyon")]
    public sbyte? AllianzYabanciSaglikKomisyon { get; set; }
    [Column("AllianzImmKomisyon")]
    public sbyte? AllianzImmKomisyon { get; set; }

    // Ana Sigorta
    [Column("AnaTrafikKomisyon")]
    public sbyte? AnaTrafikKomisyon { get; set; }
    [Column("AnaKaskoKomisyon")]
    public sbyte? AnaKaskoKomisyon { get; set; }
    [Column("AnaDaskKomisyon")]
    public sbyte? AnaDaskKomisyon { get; set; }
    [Column("AnaFerdiKazaKomisyon")]
    public sbyte? AnaFerdiKazaKomisyon { get; set; }
    [Column("AnaKoltukKomisyon")]
    public sbyte? AnaKoltukKomisyon { get; set; }
    [Column("AnaKonutKomisyon")]
    public sbyte? AnaKonutKomisyon { get; set; }
    [Column("AnaNakliyatKomisyon")]
    public sbyte? AnaNakliyatKomisyon { get; set; }
    [Column("AnaSeyahatSaglikKomisyon")]
    public sbyte? AnaSeyahatSaglikKomisyon { get; set; }
    [Column("AnaTamamlayiciSaglikKomisyon")]
    public sbyte? AnaTamamlayiciSaglikKomisyon { get; set; }
    [Column("AnaYabanciSaglikKomisyon")]
    public sbyte? AnaYabanciSaglikKomisyon { get; set; }
    [Column("AnaImmKomisyon")]
    public sbyte? AnaImmKomisyon { get; set; }

    // Anadolu Sigorta
    [Column("AnadoluTrafikKomisyon")]
    public sbyte? AnadoluTrafikKomisyon { get; set; }
    [Column("AnadoluKaskoKomisyon")]
    public sbyte? AnadoluKaskoKomisyon { get; set; }
    [Column("AnadoluDaskKomisyon")]
    public sbyte? AnadoluDaskKomisyon { get; set; }
    [Column("AnadoluFerdiKazaKomisyon")]
    public sbyte? AnadoluFerdiKazaKomisyon { get; set; }
    [Column("AnadoluKoltukKomisyon")]
    public sbyte? AnadoluKoltukKomisyon { get; set; }
    [Column("AnadoluKonutKomisyon")]
    public sbyte? AnadoluKonutKomisyon { get; set; }
    [Column("AnadoluNakliyatKomisyon")]
    public sbyte? AnadoluNakliyatKomisyon { get; set; }
    [Column("AnadoluSeyahatSaglikKomisyon")]
    public sbyte? AnadoluSeyahatSaglikKomisyon { get; set; }
    [Column("AnadoluTamamlayiciSaglikKomisyon")]
    public sbyte? AnadoluTamamlayiciSaglikKomisyon { get; set; }
    [Column("AnadoluYabanciSaglikKomisyon")]
    public sbyte? AnadoluYabanciSaglikKomisyon { get; set; }
    [Column("AnadoluImmKomisyon")]
    public sbyte? AnadoluImmKomisyon { get; set; }

    // Ankara Sigorta
    [Column("AnkaraTrafikKomisyon")]
    public sbyte? AnkaraTrafikKomisyon { get; set; }
    [Column("AnkaraKaskoKomisyon")]
    public sbyte? AnkaraKaskoKomisyon { get; set; }
    [Column("AnkaraDaskKomisyon")]
    public sbyte? AnkaraDaskKomisyon { get; set; }
    [Column("AnkaraFerdiKazaKomisyon")]
    public sbyte? AnkaraFerdiKazaKomisyon { get; set; }
    [Column("AnkaraKoltukKomisyon")]
    public sbyte? AnkaraKoltukKomisyon { get; set; }
    [Column("AnkaraKonutKomisyon")]
    public sbyte? AnkaraKonutKomisyon { get; set; }
    [Column("AnkaraNakliyatKomisyon")]
    public sbyte? AnkaraNakliyatKomisyon { get; set; }
    [Column("AnkaraSeyahatSaglikKomisyon")]
    public sbyte? AnkaraSeyahatSaglikKomisyon { get; set; }
    [Column("AnkaraTamamlayiciSaglikKomisyon")]
    public sbyte? AnkaraTamamlayiciSaglikKomisyon { get; set; }
    [Column("AnkaraYabanciSaglikKomisyon")]
    public sbyte? AnkaraYabanciSaglikKomisyon { get; set; }
    [Column("AnkaraImmKomisyon")]
    public sbyte? AnkaraImmKomisyon { get; set; }

    // Axa Sigorta
    [Column("AxaTrafikKomisyon")]
    public sbyte? AxaTrafikKomisyon { get; set; }
    [Column("AxaKaskoKomisyon")]
    public sbyte? AxaKaskoKomisyon { get; set; }
    [Column("AxaDaskKomisyon")]
    public sbyte? AxaDaskKomisyon { get; set; }
    [Column("AxaFerdiKazaKomisyon")]
    public sbyte? AxaFerdiKazaKomisyon { get; set; }
    [Column("AxaKoltukKomisyon")]
    public sbyte? AxaKoltukKomisyon { get; set; }
    [Column("AxaKonutKomisyon")]
    public sbyte? AxaKonutKomisyon { get; set; }
    [Column("AxaNakliyatKomisyon")]
    public sbyte? AxaNakliyatKomisyon { get; set; }
    [Column("AxaSeyahatSaglikKomisyon")]
    public sbyte? AxaSeyahatSaglikKomisyon { get; set; }
    [Column("AxaTamamlayiciSaglikKomisyon")]
    public sbyte? AxaTamamlayiciSaglikKomisyon { get; set; }
    [Column("AxaYabanciSaglikKomisyon")]
    public sbyte? AxaYabanciSaglikKomisyon { get; set; }
    [Column("AxaImmKomisyon")]
    public sbyte? AxaImmKomisyon { get; set; }

    // Bereket Sigorta
    [Column("BereketTrafikKomisyon")]
    public sbyte? BereketTrafikKomisyon { get; set; }
    [Column("BereketKaskoKomisyon")]
    public sbyte? BereketKaskoKomisyon { get; set; }
    [Column("BereketDaskKomisyon")]
    public sbyte? BereketDaskKomisyon { get; set; }
    [Column("BereketFerdiKazaKomisyon")]
    public sbyte? BereketFerdiKazaKomisyon { get; set; }
    [Column("BereketKoltukKomisyon")]
    public sbyte? BereketKoltukKomisyon { get; set; }
    [Column("BereketKonutKomisyon")]
    public sbyte? BereketKonutKomisyon { get; set; }
    [Column("BereketNakliyatKomisyon")]
    public sbyte? BereketNakliyatKomisyon { get; set; }
    [Column("BereketSeyahatSaglikKomisyon")]
    public sbyte? BereketSeyahatSaglikKomisyon { get; set; }
    [Column("BereketTamamlayiciSaglikKomisyon")]
    public sbyte? BereketTamamlayiciSaglikKomisyon { get; set; }
    [Column("BereketYabanciSaglikKomisyon")]
    public sbyte? BereketYabanciSaglikKomisyon { get; set; }
    [Column("BereketImmKomisyon")]
    public sbyte? BereketImmKomisyon { get; set; }

    // Doga Sigorta
    [Column("DogaTrafikKomisyon")]
    public sbyte? DogaTrafikKomisyon { get; set; }
    [Column("DogaKaskoKomisyon")]
    public sbyte? DogaKaskoKomisyon { get; set; }
    [Column("DogaDaskKomisyon")]
    public sbyte? DogaDaskKomisyon { get; set; }
    [Column("DogaFerdiKazaKomisyon")]
    public sbyte? DogaFerdiKazaKomisyon { get; set; }
    [Column("DogaKoltukKomisyon")]
    public sbyte? DogaKoltukKomisyon { get; set; }
    [Column("DogaKonutKomisyon")]
    public sbyte? DogaKonutKomisyon { get; set; }
    [Column("DogaNakliyatKomisyon")]
    public sbyte? DogaNakliyatKomisyon { get; set; }
    [Column("DogaSeyahatSaglikKomisyon")]
    public sbyte? DogaSeyahatSaglikKomisyon { get; set; }
    [Column("DogaTamamlayiciSaglikKomisyon")]
    public sbyte? DogaTamamlayiciSaglikKomisyon { get; set; }
    [Column("DogaYabanciSaglikKomisyon")]
    public sbyte? DogaYabanciSaglikKomisyon { get; set; }
    [Column("DogaImmKomisyon")]
    public sbyte? DogaImmKomisyon { get; set; }

    // Eureko Sigorta
    [Column("EurekoTrafikKomisyon")]
    public sbyte? EurekoTrafikKomisyon { get; set; }
    [Column("EurekoKaskoKomisyon")]
    public sbyte? EurekoKaskoKomisyon { get; set; }
    [Column("EurekoDaskKomisyon")]
    public sbyte? EurekoDaskKomisyon { get; set; }
    [Column("EurekoFerdiKazaKomisyon")]
    public sbyte? EurekoFerdiKazaKomisyon { get; set; }
    [Column("EurekoKoltukKomisyon")]
    public sbyte? EurekoKoltukKomisyon { get; set; }
    [Column("EurekoKonutKomisyon")]
    public sbyte? EurekoKonutKomisyon { get; set; }
    [Column("EurekoNakliyatKomisyon")]
    public sbyte? EurekoNakliyatKomisyon { get; set; }
    [Column("EurekoSeyahatSaglikKomisyon")]
    public sbyte? EurekoSeyahatSaglikKomisyon { get; set; }
    [Column("EurekoTamamlayiciSaglikKomisyon")]
    public sbyte? EurekoTamamlayiciSaglikKomisyon { get; set; }
    [Column("EurekoYabanciSaglikKomisyon")]
    public sbyte? EurekoYabanciSaglikKomisyon { get; set; }
    [Column("EurekoImmKomisyon")]
    public sbyte? EurekoImmKomisyon { get; set; }

    // Groupama Sigorta
    [Column("GroupamaTrafikKomisyon")]
    public sbyte? GroupamaTrafikKomisyon { get; set; }
    [Column("GroupamaKaskoKomisyon")]
    public sbyte? GroupamaKaskoKomisyon { get; set; }
    [Column("GroupamaDaskKomisyon")]
    public sbyte? GroupamaDaskKomisyon { get; set; }
    [Column("GroupamaFerdiKazaKomisyon")]
    public sbyte? GroupamaFerdiKazaKomisyon { get; set; }
    [Column("GroupamaKoltukKomisyon")]
    public sbyte? GroupamaKoltukKomisyon { get; set; }
    [Column("GroupamaKonutKomisyon")]
    public sbyte? GroupamaKonutKomisyon { get; set; }
    [Column("GroupamaNakliyatKomisyon")]
    public sbyte? GroupamaNakliyatKomisyon { get; set; }
    [Column("GroupamaSeyahatSaglikKomisyon")]
    public sbyte? GroupamaSeyahatSaglikKomisyon { get; set; }
    [Column("GroupamaTamamlayiciSaglikKomisyon")]
    public sbyte? GroupamaTamamlayiciSaglikKomisyon { get; set; }
    [Column("GroupamaYabanciSaglikKomisyon")]
    public sbyte? GroupamaYabanciSaglikKomisyon { get; set; }
    [Column("GroupamaImmKomisyon")]
    public sbyte? GroupamaImmKomisyon { get; set; }

    // HDI Sigorta
    [Column("HDITrafikKomisyon")]
    public sbyte? HdiTrafikKomisyon { get; set; }
    [Column("HDIKaskoKomisyon")]
    public sbyte? HdiKaskoKomisyon { get; set; }
    [Column("HDIDaskKomisyon")]
    public sbyte? HdiDaskKomisyon { get; set; }
    [Column("HDIFerdiKazaKomisyon")]
    public sbyte? HdiFerdiKazaKomisyon { get; set; }
    [Column("HDIKoltukKomisyon")]
    public sbyte? HdiKoltukKomisyon { get; set; }
    [Column("HDIKonutKomisyon")]
    public sbyte? HdiKonutKomisyon { get; set; }
    [Column("HDINakliyatKomisyon")]
    public sbyte? HdiNakliyatKomisyon { get; set; }
    [Column("HDISeyahatSaglikKomisyon")]
    public sbyte? HdiSeyahatSaglikKomisyon { get; set; }
    [Column("HDITamamlayiciSaglikKomisyon")]
    public sbyte? HdiTamamlayiciSaglikKomisyon { get; set; }
    [Column("HDIYabanciSaglikKomisyon")]
    public sbyte? HdiYabanciSaglikKomisyon { get; set; }
    [Column("HDIImmKomisyon")]
    public sbyte? HdiImmKomisyon { get; set; }

    // Hepiyi Sigorta
    [Column("HepiyiTrafikKomisyon")]
    public sbyte? HepiyiTrafikKomisyon { get; set; }
    [Column("HepiyiKaskoKomisyon")]
    public sbyte? HepiyiKaskoKomisyon { get; set; }
    [Column("HepiyiDaskKomisyon")]
    public sbyte? HepiyiDaskKomisyon { get; set; }
    [Column("HepiyiFerdiKazaKomisyon")]
    public sbyte? HepiyiFerdiKazaKomisyon { get; set; }
    [Column("HepiyiKoltukKomisyon")]
    public sbyte? HepiyiKoltukKomisyon { get; set; }
    [Column("HepiyiKonutKomisyon")]
    public sbyte? HepiyiKonutKomisyon { get; set; }
    [Column("HepiyiNakliyatKomisyon")]
    public sbyte? HepiyiNakliyatKomisyon { get; set; }
    [Column("HepiyiSeyahatSaglikKomisyon")]
    public sbyte? HepiyiSeyahatSaglikKomisyon { get; set; }
    [Column("HepiyiTamamlayiciSaglikKomisyon")]
    public sbyte? HepiyiTamamlayiciSaglikKomisyon { get; set; }
    [Column("HepiyiYabanciSaglikKomisyon")]
    public sbyte? HepiyiYabanciSaglikKomisyon { get; set; }
    [Column("HepiyiImmKomisyon")]
    public sbyte? HepiyiImmKomisyon { get; set; }

    // Mapfre Sigorta
    [Column("MapfreTrafikKomisyon")]
    public sbyte? MapfreTrafikKomisyon { get; set; }
    [Column("MapfreKaskoKomisyon")]
    public sbyte? MapfreKaskoKomisyon { get; set; }
    [Column("MapfreDaskKomisyon")]
    public sbyte? MapfreDaskKomisyon { get; set; }
    [Column("MapfreFerdiKazaKomisyon")]
    public sbyte? MapfreFerdiKazaKomisyon { get; set; }
    [Column("MapfreKoltukKomisyon")]
    public sbyte? MapfreKoltukKomisyon { get; set; }
    [Column("MapfreKonutKomisyon")]
    public sbyte? MapfreKonutKomisyon { get; set; }
    [Column("MapfreNakliyatKomisyon")]
    public sbyte? MapfreNakliyatKomisyon { get; set; }
    [Column("MapfreSeyahatSaglikKomisyon")]
    public sbyte? MapfreSeyahatSaglikKomisyon { get; set; }
    [Column("MapfreTamamlayiciSaglikKomisyon")]
    public sbyte? MapfreTamamlayiciSaglikKomisyon { get; set; }
    [Column("MapfreYabanciSaglikKomisyon")]
    public sbyte? MapfreYabanciSaglikKomisyon { get; set; }
    [Column("MapfreImmKomisyon")]
    public sbyte? MapfreImmKomisyon { get; set; }

    // Neova Sigorta
    [Column("NeovaTrafikKomisyon")]
    public sbyte? NeovaTrafikKomisyon { get; set; }
    [Column("NeovaKaskoKomisyon")]
    public sbyte? NeovaKaskoKomisyon { get; set; }
    [Column("NeovaDaskKomisyon")]
    public sbyte? NeovaDaskKomisyon { get; set; }
    [Column("NeovaFerdiKazaKomisyon")]
    public sbyte? NeovaFerdiKazaKomisyon { get; set; }
    [Column("NeovaKoltukKomisyon")]
    public sbyte? NeovaKoltukKomisyon { get; set; }
    [Column("NeovaKonutKomisyon")]
    public sbyte? NeovaKonutKomisyon { get; set; }
    [Column("NeovaNakliyatKomisyon")]
    public sbyte? NeovaNakliyatKomisyon { get; set; }
    [Column("NeovaSeyahatSaglikKomisyon")]
    public sbyte? NeovaSeyahatSaglikKomisyon { get; set; }
    [Column("NeovaTamamlayiciSaglikKomisyon")]
    public sbyte? NeovaTamamlayiciSaglikKomisyon { get; set; }
    [Column("NeovaYabanciSaglikKomisyon")]
    public sbyte? NeovaYabanciSaglikKomisyon { get; set; }
    [Column("NeovaImmKomisyon")]
    public sbyte? NeovaImmKomisyon { get; set; }

    // Sompo Sigorta
    [Column("SompoTrafikKomisyon")]
    public sbyte? SompoTrafikKomisyon { get; set; }
    [Column("SompoKaskoKomisyon")]
    public sbyte? SompoKaskoKomisyon { get; set; }
    [Column("SompoDaskKomisyon")]
    public sbyte? SompoDaskKomisyon { get; set; }
    [Column("SompoFerdiKazaKomisyon")]
    public sbyte? SompoFerdiKazaKomisyon { get; set; }
    [Column("SompoKoltukKomisyon")]
    public sbyte? SompoKoltukKomisyon { get; set; }
    [Column("SompoKonutKomisyon")]
    public sbyte? SompoKonutKomisyon { get; set; }
    [Column("SompoNakliyatKomisyon")]
    public sbyte? SompoNakliyatKomisyon { get; set; }
    [Column("SompoSeyahatSaglikKomisyon")]
    public sbyte? SompoSeyahatSaglikKomisyon { get; set; }
    [Column("SompoTamamlayiciSaglikKomisyon")]
    public sbyte? SompoTamamlayiciSaglikKomisyon { get; set; }
    [Column("SompoYabanciSaglikKomisyon")]
    public sbyte? SompoYabanciSaglikKomisyon { get; set; }
    [Column("SompoImmKomisyon")]
    public sbyte? SompoImmKomisyon { get; set; }

    // Turkiye Sigorta
    [Column("TurkiyeTrafikKomisyon")]
    public sbyte? TurkiyeTrafikKomisyon { get; set; }
    [Column("TurkiyeKaskoKomisyon")]
    public sbyte? TurkiyeKaskoKomisyon { get; set; }
    [Column("TurkiyeDaskKomisyon")]
    public sbyte? TurkiyeDaskKomisyon { get; set; }
    [Column("TurkiyeFerdiKazaKomisyon")]
    public sbyte? TurkiyeFerdiKazaKomisyon { get; set; }
    [Column("TurkiyeKoltukKomisyon")]
    public sbyte? TurkiyeKoltukKomisyon { get; set; }
    [Column("TurkiyeKonutKomisyon")]
    public sbyte? TurkiyeKonutKomisyon { get; set; }
    [Column("TurkiyeNakliyatKomisyon")]
    public sbyte? TurkiyeNakliyatKomisyon { get; set; }
    [Column("TurkiyeSeyahatSaglikKomisyon")]
    public sbyte? TurkiyeSeyahatSaglikKomisyon { get; set; }
    [Column("TurkiyeTamamlayiciSaglikKomisyon")]
    public sbyte? TurkiyeTamamlayiciSaglikKomisyon { get; set; }
    [Column("TurkiyeYabanciSaglikKomisyon")]
    public sbyte? TurkiyeYabanciSaglikKomisyon { get; set; }
    [Column("TurkiyeImmKomisyon")]
    public sbyte? TurkiyeImmKomisyon { get; set; }

    // Zurich Sigorta
    [Column("ZurichTrafikKomisyon")]
    public sbyte? ZurichTrafikKomisyon { get; set; }
    [Column("ZurichKaskoKomisyon")]
    public sbyte? ZurichKaskoKomisyon { get; set; }
    [Column("ZurichDaskKomisyon")]
    public sbyte? ZurichDaskKomisyon { get; set; }
    [Column("ZurichFerdiKazaKomisyon")]
    public sbyte? ZurichFerdiKazaKomisyon { get; set; }
    [Column("ZurichKoltukKomisyon")]
    public sbyte? ZurichKoltukKomisyon { get; set; }
    [Column("ZurichKonutKomisyon")]
    public sbyte? ZurichKonutKomisyon { get; set; }
    [Column("ZurichNakliyatKomisyon")]
    public sbyte? ZurichNakliyatKomisyon { get; set; }
    [Column("ZurichSeyahatSaglikKomisyon")]
    public sbyte? ZurichSeyahatSaglikKomisyon { get; set; }
    [Column("ZurichTamamlayiciSaglikKomisyon")]
    public sbyte? ZurichTamamlayiciSaglikKomisyon { get; set; }
    [Column("ZurichYabanciSaglikKomisyon")]
    public sbyte? ZurichYabanciSaglikKomisyon { get; set; }
    [Column("ZurichImmKomisyon")]
    public sbyte? ZurichImmKomisyon { get; set; }
}
