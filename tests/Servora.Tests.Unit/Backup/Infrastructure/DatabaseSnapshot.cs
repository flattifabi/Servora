using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Servora.Tests.Unit.Backup.Infrastructure;

/// <summary>Captures every row of every table as canonical text, independent of the backup code, to compare databases.</summary>
internal static class DatabaseSnapshot
{
    /// <summary>Captures the database content.</summary>
    /// <param name="db">The context.</param>
    /// <returns>Per table, the sorted rows rendered as text.</returns>
    public static async Task<SortedDictionary<string, List<string>>> CaptureAsync(DbContext db)
    {
        var result = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        try
        {
            foreach (var table in db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM \"{table.Name}\"";
                var rows = new List<string>();
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var cells = new List<string>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        cells.Add($"{reader.GetName(i)}={Render(value)}");
                    }

                    rows.Add(string.Join("|", cells.OrderBy(c => c, StringComparer.Ordinal)));
                }

                rows.Sort(StringComparer.Ordinal);
                result[table.Name] = rows;
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        return result;
    }

    /// <summary>Asserts that two snapshots are equal, with a readable message.</summary>
    /// <param name="expected">The expected snapshot.</param>
    /// <param name="actual">The actual snapshot.</param>
    public static void AssertEqual(SortedDictionary<string, List<string>> expected, SortedDictionary<string, List<string>> actual)
    {
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var (table, rows) in expected)
        {
            Assert.True(rows.SequenceEqual(actual[table]), $"Table {table} differs.\nExpected:\n{string.Join("\n", rows)}\nActual:\n{string.Join("\n", actual[table])}");
        }
    }

    private static string Render(object? value) => value switch
    {
        null or DBNull => "<null>",
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        IFormattable formattable => value.GetType().Name + ":" + formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.GetType().Name + ":" + value,
    };
}
