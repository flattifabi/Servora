using Servora.Application.Knowledge;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>Tests for <see cref="KnowledgeRanker"/>.</summary>
public sealed class KnowledgeRankerTests
{
    [Fact]
    public void Tokenize_RemovesStopWordsAndDuplicates()
    {
        Assert.Equal(["reset", "password", "vpn"], KnowledgeRanker.Tokenize("How do I reset my Password? password, VPN!"));
        Assert.Empty(KnowledgeRanker.Tokenize("   "));
    }

    [Fact]
    public void Score_TitleMatchesBeatBodyMatches()
    {
        var terms = KnowledgeRanker.Tokenize("password reset");
        var title = KnowledgeRanker.Score(new KnowledgeRankInput("How to reset your password", null, null, "Short body."), terms);
        var body = KnowledgeRanker.Score(new KnowledgeRankInput("Account basics", null, null, "You can reset the password in the portal."), terms);
        var none = KnowledgeRanker.Score(new KnowledgeRankInput("VPN", null, null, "Connect to the VPN."), terms);

        Assert.True(title > body);
        Assert.True(body > 0);
        Assert.Equal(0, none);
    }

    [Fact]
    public void Score_FullCoverageBeatsPartialMatch()
    {
        var terms = KnowledgeRanker.Tokenize("outlook signature");
        var both = KnowledgeRanker.Score(new KnowledgeRankInput("Outlook setup", "Signature and mailboxes", null, "text"), terms);
        var one = KnowledgeRanker.Score(new KnowledgeRankInput("Outlook outlook outlook", null, null, "outlook outlook"), terms);

        Assert.True(both > one);
    }

    [Fact]
    public void Score_PopularityBreaksTies()
    {
        var terms = KnowledgeRanker.Tokenize("vpn");
        var popular = KnowledgeRanker.Score(new KnowledgeRankInput("VPN guide", null, null, string.Empty, ViewCount: 1000, HelpfulCount: 90, NotHelpfulCount: 10), terms);
        var fresh = KnowledgeRanker.Score(new KnowledgeRankInput("VPN guide", null, null, string.Empty), terms);

        Assert.True(popular > fresh);
    }

    [Fact]
    public void Snippet_CentersOnFirstMatch()
    {
        var text = string.Join(' ', Enumerable.Repeat("filler", 80)) + " the vpn client must be installed " + string.Join(' ', Enumerable.Repeat("tail", 80));

        var snippet = KnowledgeRanker.Snippet(text, ["vpn"], 100);

        Assert.Contains("vpn", snippet, StringComparison.Ordinal);
        Assert.StartsWith("…", snippet, StringComparison.Ordinal);
        Assert.EndsWith("…", snippet, StringComparison.Ordinal);
    }
}
