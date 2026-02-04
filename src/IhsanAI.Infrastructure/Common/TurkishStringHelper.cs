namespace IhsanAI.Infrastructure.Common;

/// <summary>
/// Türkçe karakter normalizasyonu için yardımcı sınıf.
/// Dosya adı ve pattern eşleştirmelerinde kullanılır.
/// </summary>
public static class TurkishStringHelper
{
    /// <summary>
    /// Türkçe karakterleri ASCII karşılıklarına dönüştürür ve küçük harfe çevirir.
    /// Örn: "QUİCK" -> "quick", "quıck" -> "quick"
    /// </summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        return input
            // Büyük Türkçe karakterler
            .Replace("İ", "i")  // Türkçe büyük İ (U+0130)
            .Replace("I", "i")  // ASCII büyük I
            .Replace("Ğ", "g")
            .Replace("Ü", "u")
            .Replace("Ş", "s")
            .Replace("Ö", "o")
            .Replace("Ç", "c")
            // Küçük harfe çevir
            .ToLowerInvariant()
            // Küçük Türkçe karakterler
            .Replace("ı", "i")  // Türkçe küçük ı (U+0131)
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ö", "o")
            .Replace("ç", "c");
    }

    /// <summary>
    /// Normalize edilmiş string'lerde contains kontrolü yapar.
    /// </summary>
    public static bool ContainsNormalized(string text, string pattern)
    {
        return Normalize(text).Contains(Normalize(pattern));
    }
}
