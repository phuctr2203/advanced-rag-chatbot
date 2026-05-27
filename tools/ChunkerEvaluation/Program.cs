using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Ingestion;
using PolicyBot.Api.Services.Ingestion.Parsers;
using PolicyBot.Api.Services.Shared;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var dataDir = Path.Combine(repoRoot, "data", "test");
var reportDir = Path.Combine(repoRoot, "docs", "evaluations");
Directory.CreateDirectory(reportDir);

var documents = new[]
{
    Path.Combine(dataDir, "Employees Laptop Buy- Back Scheme.pdf"),
    Path.Combine(dataDir, "How to Refer a Candidate on Oracle.docx"),
    Path.Combine(dataDir, "Overtime Form.xlsx")
};

var questions = new[]
{
    "What is the employee laptop buy-back scheme?",
    "What conditions or steps must employees follow for laptop buy-back?",
    "How can an employee refer a candidate on Oracle?",
    "What information is captured by the overtime form?",
    "Which source document should be cited for candidate referral steps?"
};

var strategies = new[]
{
    new Strategy("FixedSize", "policy_docs_fixed"),
    new Strategy("ParagraphBoundary", "policy_docs_para"),
    new Strategy("SentenceWindow", "policy_docs_sentence")
};

var ingestionOptions = new IngestionOptions
{
    ImageStorePath = "../../data/images",
    TempPath = "../../data/temp",
    ChunkSizeWords = 400,
    ChunkOverlapWords = 80,
    MinimumChunkWords = 30
};

var fakeVision = new DecorativeOnlyVisionProvider();
var captioning = new ImageCaptioningService(fakeVision, NullLogger<ImageCaptioningService>.Instance);
var pdfParser = new PdfParserService(Options.Create(ingestionOptions), captioning, NullLogger<PdfParserService>.Instance);
var docxParser = new DocxParserService(captioning, Options.Create(ingestionOptions));
var xlsxParser = new XlsxParserService();
var embeddingProvider = new EmbeddingService(new HttpClient(), Options.Create(new EmbeddingOptions
{
    BaseUrl = "http://localhost:8080",
    BatchSize = 32
}));

var parsedChunks = new List<ParsedChunk>();
foreach (var document in documents)
{
    if (!File.Exists(document))
    {
        Console.WriteLine($"Skipping missing document: {document}");
        continue;
    }

    var sourceFile = Path.GetFileName(document);
    var extension = Path.GetExtension(document).ToLowerInvariant();
    IReadOnlyList<ParsedChunk> chunks = extension switch
    {
        ".pdf" => await pdfParser.ParseAsync(document, sourceFile),
        ".docx" => await docxParser.ParseAsync(document, sourceFile),
        ".xlsx" => await xlsxParser.ParseAsync(document, sourceFile),
        _ => []
    };

    parsedChunks.AddRange(chunks.Where(chunk => chunk.ChunkType.Equals("text", StringComparison.OrdinalIgnoreCase)));
}

var report = new List<string>
{
    "# Chunker Evaluation",
    "",
    $"Date: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}",
    "",
    "Documents:"
};
report.AddRange(documents.Select(path => $"- {Path.GetFileName(path)}"));
report.Add("");
report.Add("Questions:");
report.AddRange(questions.Select(question => $"- {question}"));
report.Add("");

var summaries = new List<StrategySummary>();
foreach (var strategy in strategies)
{
    Console.WriteLine($"Evaluating {strategy.Name} -> {strategy.CollectionName}");

    var chunkerOptions = ingestionOptions.WithStrategy(strategy.Name);
    var chunker = new TextChunkerService(Options.Create(chunkerOptions));
    var chunks = chunker.Chunk(parsedChunks);
    var vectors = await embeddingProvider.EmbedAsync(chunks.Select(chunk => chunk.Text).ToList());
    var vectorStore = new VectorStoreService(Options.Create(new QdrantOptions
    {
        Host = "localhost",
        Port = 6333,
        GrpcPort = 6334,
        CollectionName = strategy.CollectionName
    }));

    await vectorStore.EnsureCollectionAsync();
    await vectorStore.UpsertAsync(chunks, vectors);

    var questionResults = new List<QuestionResult>();
    foreach (var question in questions)
    {
        var queryVector = (await embeddingProvider.EmbedAsync([question]))[0];
        var results = await vectorStore.SearchAsync(queryVector, agent: null, limit: 5);
        questionResults.Add(new QuestionResult(question, Score(question, results), results));
    }

    summaries.Add(new StrategySummary(strategy.Name, strategy.CollectionName, chunks.Count, questionResults));
    AppendStrategyReport(report, summaries[^1]);
}

var best = summaries
    .OrderByDescending(summary => summary.AverageScore)
    .ThenBy(summary => StrategyPreference(summary.Name))
    .ThenBy(summary => summary.ChunkCount)
    .First();

report.Add("## Decision");
report.Add("");
report.Add($"Selected strategy: **{best.Name}**");
report.Add("");
report.Add($"Reason: highest average retrieval score ({best.AverageScore:0.00}/5) across the five representative questions. When scores tie, ParagraphBoundary is preferred because it keeps policy paragraphs and heading-adjacent context intact, which improves answer completeness and citation readability.");
report.Add("");

var reportPath = Path.Combine(reportDir, "chunker-evaluation-2026-05-27.md");
await File.WriteAllLinesAsync(reportPath, report);

Console.WriteLine($"Report written to {reportPath}");
Console.WriteLine($"Selected strategy: {best.Name} ({best.AverageScore:0.00}/5)");

static string FindRepoRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate repository root.");
}

static void AppendStrategyReport(List<string> report, StrategySummary summary)
{
    report.Add($"## {summary.Name}");
    report.Add("");
    report.Add($"Collection: `{summary.CollectionName}`");
    report.Add($"Chunks: {summary.ChunkCount}");
    report.Add($"Average score: {summary.AverageScore:0.00}/5");
    report.Add("");
    report.Add("| Question | Score | Top citation | Top score |");
    report.Add("|---|---:|---|---:|");

    foreach (var result in summary.Results)
    {
        var top = result.Results.FirstOrDefault();
        var citation = top is null ? "" : $"{top.Chunk.SourceFile}, page {top.Chunk.PageNumber}, chunk {top.Chunk.ChunkIndex}";
        var topScore = top is null ? "" : top.Score.ToString("0.000");
        report.Add($"| {Escape(result.Question)} | {result.Score} | {Escape(citation)} | {topScore} |");
    }

    report.Add("");
}

static string Escape(string value)
{
    return value.Replace("|", "\\|");
}

static int Score(string question, IReadOnlyList<VectorSearchResult> results)
{
    if (results.Count == 0)
    {
        return 1;
    }

    var expected = ExpectedTerms(question);
    var combined = string.Join(' ', results.Take(3).Select(result => $"{result.Chunk.SourceFile} {result.Chunk.Text}")).ToLowerInvariant();
    var matches = expected.Count(term => combined.Contains(term, StringComparison.OrdinalIgnoreCase));

    return matches switch
    {
        0 => 1,
        1 => 2,
        2 => 3,
        3 => 4,
        _ => 5
    };
}

static int StrategyPreference(string strategy)
{
    return strategy switch
    {
        "ParagraphBoundary" => 0,
        "SentenceWindow" => 1,
        "FixedSize" => 2,
        _ => 3
    };
}

static IReadOnlyList<string> ExpectedTerms(string question)
{
    if (question.Contains("buy-back", StringComparison.OrdinalIgnoreCase))
    {
        return ["laptop", "buy", "scheme", "employee"];
    }

    if (question.Contains("refer", StringComparison.OrdinalIgnoreCase) || question.Contains("candidate", StringComparison.OrdinalIgnoreCase))
    {
        return ["refer", "candidate", "oracle", "referral"];
    }

    if (question.Contains("overtime", StringComparison.OrdinalIgnoreCase))
    {
        return ["overtime", "employee", "date", "hour"];
    }

    return ["candidate", "oracle", "refer", "source"];
}

internal sealed record Strategy(string Name, string CollectionName);

internal sealed record StrategySummary(string Name, string CollectionName, int ChunkCount, IReadOnlyList<QuestionResult> Results)
{
    public double AverageScore => Results.Average(result => result.Score);
}

internal sealed record QuestionResult(string Question, int Score, IReadOnlyList<VectorSearchResult> Results);

internal sealed class DecorativeOnlyVisionProvider : IVisionProvider
{
    public Task<string> DescribeImageAsync(byte[] imageBytes, string prompt, int maxTokens = 300, string mimeType = "image/png", CancellationToken ct = default)
    {
        return Task.FromResult("DECORATIVE");
    }
}

file static class IngestionOptionsExtensions
{
    public static IngestionOptions WithStrategy(this IngestionOptions options, string strategy)
    {
        return new IngestionOptions
        {
            ImageStorePath = options.ImageStorePath,
            TempPath = options.TempPath,
            ChunkingStrategy = strategy,
            ChunkSizeWords = options.ChunkSizeWords,
            ChunkOverlapWords = options.ChunkOverlapWords,
            MinimumChunkWords = options.MinimumChunkWords
        };
    }
}
