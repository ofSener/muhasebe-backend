using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IhsanAI.Domain.Entities;

[Table("firma_drive_tokens")]
public class FirmaDriveToken
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("FirmaId")]
    public int FirmaId { get; set; }

    [Column("AccessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [Column("RefreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [Column("TokenExpiresAt")]
    public DateTime TokenExpiresAt { get; set; }

    [Column("GoogleEmail")]
    [MaxLength(255)]
    public string? GoogleEmail { get; set; }

    [Column("RootFolderId")]
    [MaxLength(100)]
    public string? RootFolderId { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("CreatedBy")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [Column("UpdatedBy")]
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
}
