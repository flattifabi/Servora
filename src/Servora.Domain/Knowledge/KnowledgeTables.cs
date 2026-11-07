namespace Servora.Domain.Knowledge;

/// <summary>Technical table names of the Knowledge Management module.</summary>
public static class KnowledgeTables
{
    /// <summary>Knowledge bases.</summary>
    public const string KnowledgeBase = "kb_knowledge_base";

    /// <summary>Knowledge categories.</summary>
    public const string Category = "kb_category";

    /// <summary>Article versions (snapshots).</summary>
    public const string Version = "kb_version";

    /// <summary>Helpfulness votes.</summary>
    public const string Feedback = "kb_feedback";

    /// <summary>Article to configuration item links.</summary>
    public const string ArticleCi = "kb_m2m_ci";
}
