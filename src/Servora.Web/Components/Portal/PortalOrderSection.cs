using Servora.Application.Catalog;

namespace Servora.Web.Components.Portal;

/// <summary>
/// A numbered section card of the portal order page: an order form section with its variables. Variables without a
/// (known) section are collected in a general section – the first card when the item has no sections at all ("Your
/// request"), otherwise an additional "Further details" card after the configured sections.
/// </summary>
/// <param name="Key">A stable render key.</param>
/// <param name="Title">The section title.</param>
/// <param name="Description">The optional description.</param>
/// <param name="Variables">The variables in display order.</param>
public sealed record PortalOrderSection(string Key, string Title, string? Description, IReadOnlyList<CatalogVariableDefinition> Variables)
{
    /// <summary>Groups the variables of an order form into section cards.</summary>
    /// <param name="form">The order form.</param>
    /// <returns>The sections; always at least one (the first card also hosts the requested-for field).</returns>
    public static IReadOnlyList<PortalOrderSection> Build(CatalogOrderForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var known = form.Sections.Where(s => s.Id is not null).Select(s => s.Id!.Value).ToHashSet();
        var result = form.Sections
            .Where(s => s.Id is not null)
            .Select(s => new PortalOrderSection(s.Id!.Value.ToString("N"), s.Title, s.Description, form.Variables.Where(v => v.SectionId == s.Id).OrderBy(v => v.Order).ToList()))
            .ToList();
        var general = form.Variables.Where(v => v.SectionId is not { } id || !known.Contains(id)).OrderBy(v => v.Order).ToList();
        if (result.Count == 0)
        {
            return [new PortalOrderSection("general", "Your request", null, general)];
        }

        if (general.Count > 0)
        {
            result.Add(new PortalOrderSection("general", "Further details", null, general));
        }

        return result;
    }
}
