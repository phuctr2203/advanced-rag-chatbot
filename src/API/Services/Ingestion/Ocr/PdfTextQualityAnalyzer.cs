using UglyToad.PdfPig;

namespace PolicyBot.Api.Services.Ingestion.Ocr;

public class PdfTextQualityAnalyzer
{
    public PdfTextQuality Analyze(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var pageCount = 0;
        var wordCount = 0;
        var characterCount = 0;

        foreach (var page in document.GetPages())
        {
            pageCount++;
            var words = page.GetWords()
                .Select(word => word.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            wordCount += words.Count;
            characterCount += words.Sum(word => word.Length);
        }

        return new PdfTextQuality(pageCount, wordCount, characterCount);
    }
}
