using ClosedXML.Excel;
using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Ingestion.Parsers;

public class XlsxParserService
{
    public Task<IReadOnlyList<ParsedChunk>> ParseAsync(string filePath, string? sourceFile = null, string agent = "ELCA_GENERAL", CancellationToken ct = default)
    {
        var chunks = new List<ParsedChunk>();
        var fileName = sourceFile ?? Path.GetFileName(filePath);
        var chunkIndex = 0;

        using var workbook = new XLWorkbook(filePath);
        var pageNumber = 1;
        foreach (var worksheet in workbook.Worksheets)
        {
            ct.ThrowIfCancellationRequested();

            var rowTexts = new List<string>();
            var rows = worksheet.RangeUsed()?.RowsUsed();
            if (rows is null)
            {
                pageNumber++;
                continue;
            }

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();

                var cells = row.CellsUsed()
                    .Select(cell => cell.GetFormattedString().Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();

                if (cells.Count == 0)
                {
                    continue;
                }

                rowTexts.Add(ToReadableRow(cells));
            }

            if (rowTexts.Count > 0)
            {
                chunks.Add(new ParsedChunk
                {
                    Text = $"Sheet: {worksheet.Name}. " + string.Join(' ', rowTexts),
                    SourceFile = fileName,
                    PageNumber = pageNumber,
                    ChunkIndex = chunkIndex++,
                    ChunkType = "text",
                    FileType = "xlsx",
                    Agent = agent
                });
            }

            pageNumber++;
        }

        return Task.FromResult<IReadOnlyList<ParsedChunk>>(chunks);
    }

    private static string ToReadableRow(IReadOnlyList<string> cells)
    {
        return cells.Count switch
        {
            1 => $"Field: {cells[0]}.",
            2 => $"Field: {cells[0]} — Value: {cells[1]}.",
            _ => $"Field: {cells[0]} — Value: {cells[1]} — Description: {string.Join(" — ", cells.Skip(2))}."
        };
    }
}
