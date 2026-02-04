using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("drive_upload_logs")]
public class DriveUploadLog
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("FirmaId")]
    public int FirmaId { get; set; }

    [Column("FileName")]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Column("OriginalFileName")]
    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    [Column("DriveFileId")]
    [MaxLength(100)]
    public string? DriveFileId { get; set; }

    [Column("DriveWebViewLink")]
    [MaxLength(500)]
    public string? DriveWebViewLink { get; set; }

    [Column("DriveFolderPath")]
    [MaxLength(500)]
    public string? DriveFolderPath { get; set; }

    [Column("FileSizeBytes")]
    public long FileSizeBytes { get; set; }

    [Column("UploadStatus")]
    public UploadStatus UploadStatus { get; set; } = UploadStatus.Pending;

    [Column("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [Column("UploadedByUserId")]
    public int? UploadedByUserId { get; set; }

    [Column("UploadedAt")]
    public DateTime UploadedAt { get; set; }

    [Column("PoliceId")]
    [MaxLength(50)]
    public string? PoliceId { get; set; }
}

public enum UploadStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2
}
