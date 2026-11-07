using System.Text;

namespace Servora.Application.Knowledge;

/// <summary>
/// Pure term-based relevance ranking for knowledge search: weighted term frequency over title, keywords, summary and body,
/// a phrase bonus, term coverage and a small popularity boost.
/// </summary>
public static class KnowledgeRanker
{
    /// <summary>The maximum number of terms taken from a query.</summary>
    public const int MaxTerms = 8;

    private const double TitleWeight = 10;
    private const double TitleWordBonus = 4;
    private const double KeywordWeight = 6;
    private const double SummaryWeight = 4;
    private const double BodyWeight = 1;
    private const int BodyOccurrenceCap = 6;

    private static readonly HashSet<string> s_stopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "for", "from", "how", "i", "in", "is", "it", "my",
        "of", "on", "or", "the", "to", "what", "when", "where", "which", "who", "why", "with", "you", "your", "me", "we", "not",
    };

    /// <summary>Splits a query into distinct lower-case terms without stop words.</summary>
    /// <param name="query">The query.</param>
    /// <returns>Up to <see cref="MaxTerms"/> terms.</returns>
    public static IReadOnlyList<string> Tokenize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var terms = new List<string>();
        var current = new StringBuilder();
        foreach (var c in query.ToLowerInvariant().Append(' '))
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
                continue;
            }

            if (current.Length > 0)
            {
                var term = current.ToString();
                current.Clear();
                if ((term.Length >= 2 || char.IsDigit(term[0])) && !s_stopWords.Contains(term) && !terms.Contains(term))
                {
                    terms.Add(term.Length > 50 ? term[..50] : term);
                }
            }

            if (terms.Count >= MaxTerms)
            {
                break;
            }
        }

        return terms;
    }

    /// <summary>Computes the relevance score of an article for a set of terms.</summary>
    /// <param name="document">The article text and signals.</param>
    /// <param name="terms">The query terms (see <see cref="Tokenize"/>).</param>
    /// <returns>The score; 0 when no term matches.</returns>
    public static double Score(KnowledgeRankInput document, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return 0;
        }

        var title = document.Title.ToLowerInvariant();
        var summary = document.Summary?.ToLowerInvariant() ?? string.Empty;
        var keywords = document.Keywords?.ToLowerInvariant() ?? string.Empty;
        var body = document.PlainBody.ToLowerInvariant();

        double score = 0;
        var matched = 0;
        foreach (var term in terms)
        {
            var termScore = 0d;
            if (CountOccurrences(title, term, 1) > 0)
            {
                termScore += TitleWeight;
                if (StartsWord(title, term))
                {
                    termScore += TitleWordBonus;
                }
            }

            termScore += KeywordWeight * Math.Min(1, CountOccurrences(keywords, term, 1));
            termScore += SummaryWeight * CountOccurrences(summary, term, 3);
            termScore += BodyWeight * CountOccurrences(body, term, BodyOccurrenceCap);
            if (termScore > 0)
            {
                matched++;
                score += termScore;
            }
        }

        if (matched == 0)
        {
            return 0;
        }

        if (terms.Count > 1)
        {
            var phrase = string.Join(' ', terms);
            if (title.Contains(phrase, StringComparison.Ordinal))
            {
                score += 15;
            }
            else if (summary.Contains(phrase, StringComparison.Ordinal) || body.Contains(phrase, StringComparison.Ordinal))
            {
                score += 5;
            }
        }

        // Articles that match all terms rank above articles that match only some of them.
        var coverage = matched / (double)terms.Count;
        score *= 0.25 + (0.75 * coverage);

        // Small popularity boost: views (logarithmic) and helpfulness.
        score += Math.Log10(1 + Math.Max(0, document.ViewCount)) * 1.5;
        var votes = document.HelpfulCount + document.NotHelpfulCount;
        if (votes > 0)
        {
            score += 2.0 * document.HelpfulCount / votes;
        }

        return Math.Round(score, 3);
    }

    /// <summary>Builds a plain-text snippet around the first occurrence of any term.</summary>
    /// <param name="plainText">The plain text.</param>
    /// <param name="terms">The terms.</param>
    /// <param name="length">The approximate snippet length.</param>
    /// <returns>The snippet (with ellipses when cut).</returns>
    public static string Snippet(string plainText, IReadOnlyList<string> terms, int length = 180)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var lower = plainText.ToLowerInvariant();
        var first = terms.Select(t => lower.IndexOf(t, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, first - (length / 3));
        if (start > 0)
        {
            var space = plainText.IndexOf(' ', start);
            start = space >= 0 && space < first ? space + 1 : start;
        }

        var end = Math.Min(plainText.Length, start + length);
        if (end < plainText.Length)
        {
            var space = plainText.LastIndexOf(' ', end);
            end = space > start ? space : end;
        }

        var snippet = plainText[start..end].Trim();
        return (start > 0 ? "… " : string.Empty) + snippet + (end < plainText.Length ? " …" : string.Empty);
    }

    private static int CountOccurrences(string text, string term, int cap)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var count = 0;
        var index = text.IndexOf(term, StringComparison.Ordinal);
        while (index >= 0 && count < cap)
        {
            count++;
            index = text.IndexOf(term, index + term.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static bool StartsWord(string text, string term)
    {
        var index = text.IndexOf(term, StringComparison.Ordinal);
        while (index >= 0)
        {
            if (index == 0 || !char.IsLetterOrDigit(text[index - 1]))
            {
                return true;
            }

            index = text.IndexOf(term, index + 1, StringComparison.Ordinal);
        }

        return false;
    }
}
