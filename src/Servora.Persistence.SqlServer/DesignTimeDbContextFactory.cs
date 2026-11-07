using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Servora.Infrastructure.Persistence;

namespace Servora.Persistence.SqlServer;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c> to build the Microsoft SQL Server model. The connection string is
/// only used for tooling; set <c>SERVORA_DESIGN_CONNECTION</c> to override it.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ServoraDbContext>
{
    /// <inheritdoc />
    public ServoraDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ServoraDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("SERVORA_DESIGN_CONNECTION") ?? "Server=localhost;Database=ServoraDesign;Trusted_Connection=True;TrustServerCertificate=True";
        new SqlServerDatabaseProvider().Configure(builder, connectionString);
        return new ServoraDbContext(builder.Options);
    }
}
