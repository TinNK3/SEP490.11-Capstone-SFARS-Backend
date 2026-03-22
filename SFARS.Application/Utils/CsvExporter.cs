using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SFARS.Application.Utils;

public static class CsvExporter
{
    public static string ToCsv(object? data)
    {
        if (data == null) return string.Empty;

        if (data is System.Collections.IEnumerable enumerable && data is not string)
        {
            var rows = new List<object?>();
            foreach (var item in enumerable) rows.Add(item);
            return ToCsvFromList(rows);
        }

        return ToCsvFromSingle(data);
    }

    private static string ToCsvFromSingle(object row)
    {
        var props = row.GetType().GetProperties()
            .Where(p => p.CanRead)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", props.Select(p => Escape(p.Name))));
        sb.AppendLine(string.Join(",", props.Select(p => Escape(FormatValue(p.GetValue(row))))));
        return sb.ToString();
    }

    private static string ToCsvFromList(List<object?> rows)
    {
        if (rows.Count == 0) return string.Empty;
        var first = rows.FirstOrDefault(r => r != null);
        if (first == null) return string.Empty;

        var props = first.GetType().GetProperties()
            .Where(p => p.CanRead)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", props.Select(p => Escape(p.Name))));

        foreach (var row in rows)
        {
            if (row == null) continue;
            sb.AppendLine(string.Join(",", props.Select(p => Escape(FormatValue(p.GetValue(row))))));
        }

        return sb.ToString();
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return string.Empty;
        if (value is DateTime dt) return dt.ToString("O", CultureInfo.InvariantCulture);
        if (value is DateTimeOffset dto) return dto.ToString("O", CultureInfo.InvariantCulture);
        if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
        return value.ToString() ?? string.Empty;
    }

    private static string Escape(string s)
    {
        if (s.Contains('"')) s = s.Replace("\"", "\"\"");
        if (s.Contains(',') || s.Contains('\n') || s.Contains('\r') || s.Contains('"'))
        {
            return $"\"{s}\"";
        }
        return s;
    }
}

