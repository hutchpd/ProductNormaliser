using System.Text;

namespace ProductNormaliser.Web.Services;

internal static class CsvExportBuilder
{
    public static byte[] Build(params IReadOnlyList<string>[] rows)
    {
        return Build(rows.AsEnumerable());
    }

    public static byte[] Build(IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(Escape)));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"'))
        {
            text = text.Replace("\"", "\"\"");
        }

        if (text.IndexOfAny([',', '"', '\r', '\n']) >= 0)
        {
            return $"\"{text}\"";
        }

        return text;
    }
}