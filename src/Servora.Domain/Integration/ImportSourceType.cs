namespace Servora.Domain.Integration;

/// <summary>Where the rows of an import set come from.</summary>
public enum ImportSourceType
{
    /// <summary>A JSON array returned by an approved data source endpoint.</summary>
    DataSource = 0,

    /// <summary>A CSV file uploaded in the UI.</summary>
    CsvUpload = 1,
}
