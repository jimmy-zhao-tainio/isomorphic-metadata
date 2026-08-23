using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Meta.Integration;

public static class DelimitedTextParser
{
    public static List<List<string>> ParseRows(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentCell = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                {
                    currentCell.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (!inQuotes && ch == delimiter)
            {
                AppendCell(currentRow, currentCell);
                continue;
            }

            if (!inQuotes && (ch == '\r' || ch == '\n'))
            {
                AppendCell(currentRow, currentCell);
                if (!IsRowCompletelyEmpty(currentRow))
                {
                    rows.Add(currentRow);
                }

                currentRow = new List<string>();
                if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                continue;
            }

            currentCell.Append(ch);
        }

        if (inQuotes)
        {
            throw new InvalidOperationException("Delimited text contains an unclosed quoted field.");
        }

        AppendCell(currentRow, currentCell);
        if (!IsRowCompletelyEmpty(currentRow) || rows.Count == 0)
        {
            rows.Add(currentRow);
        }

        return rows;
    }

    private static void AppendCell(ICollection<string> row, StringBuilder currentCell)
    {
        var value = currentCell
            .ToString()
            .Trim()
            .TrimStart('\uFEFF');
        row.Add(value);
        currentCell.Clear();
    }

    private static bool IsRowCompletelyEmpty(IReadOnlyCollection<string> row) =>
        row.Count == 0 || row.All(cell => string.IsNullOrWhiteSpace(cell));
}
