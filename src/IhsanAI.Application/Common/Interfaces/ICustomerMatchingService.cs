namespace IhsanAI.Application.Common.Interfaces;

/// <summary>
/// Çoklu sinyal bazlı müşteri eşleştirme servisi.
/// Öncelik sırası: TC Kimlik > Vergi No > Plaka > İsim
/// </summary>
public interface ICustomerMatchingService
{
    /// <summary>
    /// Tek bir kayıt için en iyi müşteri eşleşmesini bulur.
    /// </summary>
    Task<CustomerMatchResult> FindBestMatchAsync(CustomerMatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Toplu import için batch eşleştirme yapar. Firma bazında müşteri listesini önbelleğe alır.
    /// </summary>
    Task<Dictionary<int, CustomerMatchResult>> BatchMatchAsync(
        List<CustomerMatchRequest> requests, int firmaId, CancellationToken ct = default);
}

public record CustomerMatchRequest
{
    public int RowIndex { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
    public string? SigortaliAdi { get; init; }
    public string? Plaka { get; init; }
    public int FirmaId { get; init; }
}

public record CustomerMatchResult
{
    public int? MusteriId { get; init; }
    public MatchConfidence Confidence { get; init; }
    public MatchSignal MatchedBy { get; init; }
    public bool AutoCreated { get; init; }
    public List<CustomerCandidate> Candidates { get; init; } = new();
}

public record CustomerCandidate
{
    public int MusteriId { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
    public MatchConfidence Confidence { get; init; }
    public MatchSignal MatchedBy { get; init; }
}

public enum MatchConfidence
{
    None = 0,
    Low = 1,      // İsim eşleşmesi, birden fazla aday
    Medium = 2,   // Plaka eşleşmesi veya tek isim eşleşmesi
    High = 3,     // VKN eşleşmesi
    Exact = 4     // TC Kimlik eşleşmesi
}

public enum MatchSignal
{
    None = 0,
    Name = 1,
    Plaka = 2,
    VergiNo = 3,
    TcKimlikNo = 4
}
