namespace Servora.Application.Automation.Actions;

/// <summary>Editor type of a step configuration field in the Flow Designer.</summary>
public enum FlowActionFieldKind
{
    /// <summary>Single-line text with data pills.</summary>
    Text = 0,

    /// <summary>Multi-line text with data pills.</summary>
    Multiline = 1,

    /// <summary>Number.</summary>
    Number = 2,

    /// <summary>Fixed list of options.</summary>
    Select = 3,

    /// <summary>Table picker.</summary>
    Table = 4,

    /// <summary>Field assignments for the table chosen in <see cref="FlowActionField.TableFieldKey"/> (JSON array of field values).</summary>
    RecordValues = 5,

    /// <summary>Data pill condition (JSON <c>FlowCondition</c>).</summary>
    Condition = 6,

    /// <summary>Record filter on the table chosen in <see cref="FlowActionField.TableFieldKey"/> (JSON <c>ConditionGroup</c>; values may contain pills).</summary>
    RecordCondition = 7,

    /// <summary>Data source picker.</summary>
    DataSource = 8,

    /// <summary>Endpoint picker of the data source chosen in <see cref="FlowActionField.TableFieldKey"/>.</summary>
    DataSourceEndpoint = 9,

    /// <summary>Name / value pairs with data pills (JSON object).</summary>
    KeyValues = 10,

    /// <summary>Flow picker (sub-flows).</summary>
    Flow = 11,
}
