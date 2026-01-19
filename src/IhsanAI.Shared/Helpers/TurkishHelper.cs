namespace IhsanAI.Shared.Helpers;

public static class TurkishHelper
{
    private static readonly Dictionary<char, char> TurkishCharacterMap = new()
    {
        { 'ç', 'c' },
        { 'Ç', 'C' },
        { 'ğ', 'g' },
        { 'Ğ', 'G' },
        { 'ı', 'i' },
        { 'İ', 'I' },
        { 'ö', 'o' },
        { 'Ö', 'O' },
        { 'ş', 's' },
        { 'Ş', 'S' },
        { 'ü', 'u' },
        { 'Ü', 'U' }
    };

    public static string ReplaceTurkishCharacters(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new char[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            result[i] = TurkishCharacterMap.TryGetValue(input[i], out var replacement)
                ? replacement
                : input[i];
        }

        return new string(result);
    }

    public static string ToTurkishLower(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace('I', 'ı')
            .Replace('İ', 'i')
            .ToLowerInvariant();
    }

    public static string ToTurkishUpper(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace('i', 'İ')
            .Replace('ı', 'I')
            .ToUpperInvariant();
    }

    public static int CompareTurkish(string x, string y)
    {
        return string.Compare(x, y, StringComparison.CurrentCulture);
    }
}
