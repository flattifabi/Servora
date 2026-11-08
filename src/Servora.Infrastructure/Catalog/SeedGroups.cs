namespace Servora.Infrastructure.Catalog;

/// <summary>Identifiers of the groups the catalog seeder assigns to items.</summary>
/// <param name="ServiceDesk">The service desk.</param>
/// <param name="Desktop">Desktop support.</param>
/// <param name="Identity">Identity and access management.</param>
/// <param name="Network">Network operations.</param>
/// <param name="Messaging">Messaging and collaboration.</param>
/// <param name="Approvers">The IT approvers group.</param>
internal sealed record SeedGroups(Guid ServiceDesk, Guid Desktop, Guid Identity, Guid Network, Guid Messaging, Guid Approvers);
