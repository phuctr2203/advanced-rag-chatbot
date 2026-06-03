using PolicyBot.Api.Options;

namespace PolicyBot.Api.Services.Ingestion.Ocr;

public sealed record PdfTextQuality(int PageCount, int WordCount, int CharacterCount)
{
    public int AverageWordsPerPage => PageCount == 0 ? 0 : WordCount / PageCount;

    public bool NeedsOcr(IngestionOptions options)
    {
        return CharacterCount < options.PdfOcrMinimumTextCharacters
            || AverageWordsPerPage < options.PdfOcrMinimumAverageWordsPerPage;
    }
}
