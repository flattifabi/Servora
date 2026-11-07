using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// Includes the database provider in the EF Core model cache key, because the model contains provider-specific value
/// conversions (for example DateTimeOffset handling on SQLite).
/// </summary>
public sealed class ProviderAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <inheritdoc />
    public object Create(DbContext context, bool designTime) => (context.GetType(), context.Database.ProviderName, designTime);
}
