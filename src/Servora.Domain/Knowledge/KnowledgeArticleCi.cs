using Servora.Domain.Common;

namespace Servora.Domain.Knowledge;

/// <summary>Links a knowledge article to a configuration item it documents.</summary>
public class KnowledgeArticleCi : Entity
{
    /// <summary>Gets or sets the article.</summary>
    [Reference(TableNames.KnowledgeArticle)]
    public Guid ArticleId { get; set; }

    /// <summary>Gets or sets the configuration item.</summary>
    [Reference(TableNames.ConfigurationItem)]
    public Guid ConfigurationItemId { get; set; }
}
