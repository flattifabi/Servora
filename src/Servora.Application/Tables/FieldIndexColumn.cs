namespace Servora.Application.Tables;

/// <summary>The column of a <see cref="Domain.Platform.RecordFieldValue"/> row that holds a field's comparable value.</summary>
public enum FieldIndexColumn
{
    /// <summary>Lower-case text (text, choice, reference, identifier, boolean, e-mail, URL, JSON fields).</summary>
    Text = 0,

    /// <summary>Number (integer and decimal fields).</summary>
    Number = 1,

    /// <summary>UTC date (date and date/time fields).</summary>
    Date = 2,
}
