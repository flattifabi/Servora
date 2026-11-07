namespace Servora.Application.Automation.Services;

/// <summary>A data pill offered by the designer's pill picker.</summary>
/// <param name="Path">The data path, inserted as <c>{{path}}</c>.</param>
/// <param name="Label">The display label.</param>
/// <param name="Group">The group (Trigger, Inputs, Variables or the step name).</param>
public sealed record FlowDataPill(string Path, string Label, string Group)
{
    /// <summary>Gets the template expression for the pill.</summary>
    public string Expression => "{{" + Path + "}}";
}
