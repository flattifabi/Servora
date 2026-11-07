using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Servora.Web.Components.UI.Charts;

/// <summary>
/// Renders an SVG <c>&lt;text&gt;</c> element. Needed because Razor reserves the <c>&lt;text&gt;</c> tag inside code blocks
/// (loops, conditions), where chart labels are generated.
/// </summary>
public sealed class SvgText : ComponentBase
{
    /// <summary>Gets or sets the x coordinate.</summary>
    [Parameter] public object? X { get; set; }

    /// <summary>Gets or sets the y coordinate.</summary>
    [Parameter] public object? Y { get; set; }

    /// <summary>Gets or sets the text anchor (<c>start</c>, <c>middle</c>, <c>end</c>).</summary>
    [Parameter] public string? Anchor { get; set; }

    /// <summary>Gets or sets the CSS class.</summary>
    [Parameter] public string? Class { get; set; } = "axis-label";

    /// <summary>Gets or sets the text.</summary>
    [Parameter] public string? Text { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "text");
        builder.AddAttribute(1, "class", Class);
        builder.AddAttribute(2, "x", Format(X));
        builder.AddAttribute(3, "y", Format(Y));
        builder.AddAttribute(4, "text-anchor", Anchor);
        builder.AddContent(5, Text);
        builder.CloseElement();
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        IFormattable formattable => formattable.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
