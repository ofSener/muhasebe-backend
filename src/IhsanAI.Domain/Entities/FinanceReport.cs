using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("muhasebe_finansraporlari")]
public class FinanceReport
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("FirmaID")]
    public int FirmaId { get; set; }

    [Column("CreatedByUserID")]
    public int CreatedByUserId { get; set; }

    [Column("ReportType")]
    [MaxLength(50)]
    public string ReportType { get; set; } = string.Empty;

    [Column("PeriodType")]
    [MaxLength(30)]
    public string PeriodType { get; set; } = string.Empty;

    [Column("StartDate")]
    public DateTime StartDate { get; set; }

    [Column("EndDate")]
    public DateTime EndDate { get; set; }

    [Column("Format")]
    [MaxLength(10)]
    public string Format { get; set; } = string.Empty;

    [Column("DetailLevel")]
    [MaxLength(20)]
    public string DetailLevel { get; set; } = string.Empty;

    [Column("FiltersJson", TypeName = "longtext")]
    public string FiltersJson { get; set; } = "{}";

    [Column("FileName")]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Column("FilePath")]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Column("ContentType")]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Column("FileSizeBytes")]
    public long FileSizeBytes { get; set; }

    [Column("Status")]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Column("ErrorMessage", TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("DeletedAt")]
    public DateTime? DeletedAt { get; set; }

    [Column("DeletedByUserID")]
    public int? DeletedByUserId { get; set; }
}
