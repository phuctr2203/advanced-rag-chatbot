using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Services.Ingestion.Chunking;

public class TextChunkerService(IOptions<IngestionOptions> options)
{
    private readonly IngestionOptions _options = options.Value;

    public IReadOnlyList<ParsedChunk> Chunk(IReadOnlyList<ParsedChunk> chunks)
    {
        return _options.ChunkingStrategy switch
        {
            var strategy when strategy.Equals("FixedSize", StringComparison.OrdinalIgnoreCase) => ChunkFixedSize(chunks),
            var strategy when strategy.Equals("ParagraphBoundary", StringComparison.OrdinalIgnoreCase) => ChunkParagraphBoundary(chunks),
            var strategy when strategy.Equals("SentenceWindow", StringComparison.OrdinalIgnoreCase) => ChunkSentenceWindow(chunks),
            var strategy when strategy.Equals("RecursiveBoundary", StringComparison.OrdinalIgnoreCase) => ChunkRecursiveBoundary(chunks),
            _ => ChunkParagraphBoundary(chunks)
        };
    }

    public IReadOnlyList<ParsedChunk> ChunkFixedSize(IReadOnlyList<ParsedChunk> chunks)
    {
        var output = new List<ParsedChunk>();
        var chunkIndex = 0;

        foreach (var chunk in chunks)
        {
            if (ShouldPreserveChunk(chunk))
            {
                output.Add(CloneChunk(chunk, chunk.Text, chunkIndex++));
                continue;
            }

            AddFixedSizeChunks(chunk, chunk.Text, output, ref chunkIndex);
        }

        return output;
    }

    public IReadOnlyList<ParsedChunk> ChunkParagraphBoundary(IReadOnlyList<ParsedChunk> chunks)
    {
        var output = new List<ParsedChunk>();
        var chunkIndex = 0;

        foreach (var chunk in chunks)
        {
            if (ShouldPreserveChunk(chunk))
            {
                output.Add(CloneChunk(chunk, chunk.Text, chunkIndex++));
                continue;
            }

            var paragraphs = SplitParagraphsAndHeadings(chunk.Text);
            var currentParagraphs = new List<string>();
            var currentWordCount = 0;

            foreach (var paragraph in paragraphs)
            {
                var paragraphWordCount = CountWords(paragraph);
                if (paragraphWordCount == 0)
                {
                    continue;
                }

                if (paragraphWordCount > _options.ChunkSizeWords)
                {
                    FlushParagraphChunk(chunk, currentParagraphs, ref currentWordCount, output, ref chunkIndex);
                    AddFixedSizeChunks(chunk, paragraph, output, ref chunkIndex);

                    currentParagraphs = [TakeLastWords(paragraph, _options.ChunkOverlapWords)];
                    currentWordCount = CountWords(currentParagraphs[0]);
                    continue;
                }

                if (currentWordCount > 0 && currentWordCount + paragraphWordCount > _options.ChunkSizeWords)
                {
                    var overlap = BuildParagraphOverlap(currentParagraphs);
                    FlushParagraphChunk(chunk, currentParagraphs, ref currentWordCount, output, ref chunkIndex);

                    currentParagraphs = overlap.Length == 0 ? [] : [overlap];
                    currentWordCount = CountWords(overlap);
                }

                currentParagraphs.Add(paragraph);
                currentWordCount += paragraphWordCount;
            }

            FlushParagraphChunk(chunk, currentParagraphs, ref currentWordCount, output, ref chunkIndex);
        }

        return output;
    }

    public IReadOnlyList<ParsedChunk> ChunkSentenceWindow(IReadOnlyList<ParsedChunk> chunks)
    {
        var output = new List<ParsedChunk>();
        var chunkIndex = 0;

        foreach (var chunk in chunks)
        {
            if (ShouldPreserveChunk(chunk))
            {
                output.Add(CloneChunk(chunk, chunk.Text, chunkIndex++));
                continue;
            }

            var sentences = SplitSentences(chunk.Text);
            var currentSentences = new List<string>();
            var currentWordCount = 0;

            foreach (var sentence in sentences)
            {
                var sentenceWordCount = CountWords(sentence);
                if (sentenceWordCount == 0)
                {
                    continue;
                }

                if (sentenceWordCount > _options.ChunkSizeWords)
                {
                    FlushSentenceChunk(chunk, currentSentences, ref currentWordCount, output, ref chunkIndex);
                    AddFixedSizeChunks(chunk, sentence, output, ref chunkIndex);
                    currentSentences.Clear();
                    currentWordCount = 0;
                    continue;
                }

                if (currentWordCount > 0 && currentWordCount + sentenceWordCount > _options.ChunkSizeWords)
                {
                    var overlap = BuildSentenceOverlap(currentSentences);
                    FlushSentenceChunk(chunk, currentSentences, ref currentWordCount, output, ref chunkIndex);

                    currentSentences = overlap;
                    currentWordCount = currentSentences.Sum(CountWords);
                }

                currentSentences.Add(sentence);
                currentWordCount += sentenceWordCount;
            }

            FlushSentenceChunk(chunk, currentSentences, ref currentWordCount, output, ref chunkIndex);
        }

        return output;
    }

    public IReadOnlyList<ParsedChunk> ChunkRecursiveBoundary(IReadOnlyList<ParsedChunk> chunks)
    {
        var output = new List<ParsedChunk>();
        var chunkIndex = 0;

        foreach (var chunk in chunks)
        {
            if (ShouldPreserveChunk(chunk))
            {
                output.Add(CloneChunk(chunk, chunk.Text, chunkIndex++));
                continue;
            }

            var pieces = SplitRecursive(chunk.Text);
            foreach (var piece in pieces)
            {
                var wordCount = CountWords(piece);
                if (wordCount < _options.MinimumChunkWords)
                {
                    if (ShouldKeepShortChunk(chunk, wordCount))
                    {
                        output.Add(CloneChunk(chunk, piece, chunkIndex++));
                    }

                    continue;
                }

                output.Add(CloneChunk(chunk, piece, chunkIndex++));
            }
        }

        return output;
    }

    private void AddFixedSizeChunks(ParsedChunk source, string text, List<ParsedChunk> output, ref int chunkIndex)
    {
        var words = GetWords(text);
        if (words.Count < _options.MinimumChunkWords)
        {
            if (ShouldKeepShortChunk(source, words.Count))
            {
                output.Add(CloneChunk(source, text, chunkIndex++));
            }

            return;
        }

        var step = Math.Max(1, _options.ChunkSizeWords - _options.ChunkOverlapWords);
        for (var start = 0; start < words.Count; start += step)
        {
            var chunkWords = words.Skip(start).Take(_options.ChunkSizeWords).ToList();
            if (chunkWords.Count < _options.MinimumChunkWords)
            {
                if (ShouldKeepShortChunk(source, chunkWords.Count))
                {
                    output.Add(CloneChunk(source, string.Join(' ', chunkWords), chunkIndex++));
                }

                continue;
            }

            output.Add(CloneChunk(source, string.Join(' ', chunkWords), chunkIndex++));

            if (start + _options.ChunkSizeWords >= words.Count)
            {
                break;
            }
        }
    }

    private void FlushParagraphChunk(ParsedChunk source, List<string> paragraphs, ref int currentWordCount, List<ParsedChunk> output, ref int chunkIndex)
    {
        if (paragraphs.Count == 0)
        {
            return;
        }

        if (currentWordCount < _options.MinimumChunkWords)
        {
            if (ShouldKeepShortChunk(source, currentWordCount))
            {
                output.Add(CloneChunk(source, string.Join($"{Environment.NewLine}{Environment.NewLine}", paragraphs), chunkIndex++));
            }

            paragraphs.Clear();
            currentWordCount = 0;
            return;
        }

        output.Add(CloneChunk(source, string.Join($"{Environment.NewLine}{Environment.NewLine}", paragraphs), chunkIndex++));
        paragraphs.Clear();
        currentWordCount = 0;
    }

    private void FlushSentenceChunk(ParsedChunk source, List<string> sentences, ref int currentWordCount, List<ParsedChunk> output, ref int chunkIndex)
    {
        if (sentences.Count == 0)
        {
            return;
        }

        if (currentWordCount < _options.MinimumChunkWords)
        {
            if (ShouldKeepShortChunk(source, currentWordCount))
            {
                output.Add(CloneChunk(source, string.Join(' ', sentences), chunkIndex++));
            }

            sentences.Clear();
            currentWordCount = 0;
            return;
        }

        output.Add(CloneChunk(source, string.Join(' ', sentences), chunkIndex++));
        sentences.Clear();
        currentWordCount = 0;
    }

    private string BuildParagraphOverlap(List<string> paragraphs)
    {
        if (paragraphs.Count == 0 || _options.ChunkOverlapWords <= 0)
        {
            return string.Empty;
        }

        var lastParagraph = paragraphs[^1];
        var lastParagraphWordCount = CountWords(lastParagraph);
        return lastParagraphWordCount <= _options.ChunkOverlapWords
            ? lastParagraph
            : TakeLastWords(lastParagraph, _options.ChunkOverlapWords);
    }

    private List<string> BuildSentenceOverlap(List<string> sentences)
    {
        if (sentences.Count == 0)
        {
            return [];
        }

        var lastSentence = sentences[^1];
        if (sentences.Count == 1)
        {
            return [lastSentence];
        }

        var secondLastSentence = sentences[^2];
        var overlapWords = CountWords(secondLastSentence) + CountWords(lastSentence);
        return overlapWords <= _options.ChunkOverlapWords
            ? [secondLastSentence, lastSentence]
            : [lastSentence];
    }

    private static List<string> SplitParagraphsAndHeadings(string text)
    {
        var paragraphs = new List<string>();
        var currentLines = new List<string>();

        foreach (var rawLine in Regex.Split(text, @"\r?\n"))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                AddParagraph(paragraphs, currentLines);
                continue;
            }

            if (IsHeadingLike(line) && currentLines.Count > 0)
            {
                AddParagraph(paragraphs, currentLines);
            }

            currentLines.Add(line);

            if (IsHeadingLike(line))
            {
                AddParagraph(paragraphs, currentLines);
            }
        }

        AddParagraph(paragraphs, currentLines);
        return paragraphs;
    }

    private static List<string> SplitSentences(string text)
    {
        return Regex
            .Matches(text, @"[^.!?\u3002]+[.!?\u3002]+|[^.!?\u3002]+$")
            .Select(match => Regex.Replace(match.Value, @"\s+", " ").Trim())
            .Where(sentence => sentence.Length > 0)
            .ToList();
    }

    private IReadOnlyList<string> SplitRecursive(string text)
    {
        var paragraphs = SplitParagraphsAndHeadings(text);
        var chunks = new List<string>();
        var currentParts = new List<string>();
        var currentWordCount = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphWordCount = CountWords(paragraph);
            if (paragraphWordCount == 0)
            {
                continue;
            }

            if (paragraphWordCount > _options.ChunkSizeWords)
            {
                FlushRecursiveParts(currentParts, ref currentWordCount, chunks);
                chunks.AddRange(SplitRecursiveSentences(paragraph));
                continue;
            }

            if (currentWordCount > 0 && currentWordCount + paragraphWordCount > _options.ChunkSizeWords)
            {
                FlushRecursiveParts(currentParts, ref currentWordCount, chunks);
            }

            currentParts.Add(paragraph);
            currentWordCount += paragraphWordCount;
        }

        FlushRecursiveParts(currentParts, ref currentWordCount, chunks);
        return chunks;
    }

    private IReadOnlyList<string> SplitRecursiveSentences(string text)
    {
        var sentences = SplitSentences(text);
        if (sentences.Count <= 1)
        {
            return SplitRecursiveWords(text);
        }

        var chunks = new List<string>();
        var currentSentences = new List<string>();
        var currentWordCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceWordCount = CountWords(sentence);
            if (sentenceWordCount == 0)
            {
                continue;
            }

            if (sentenceWordCount > _options.ChunkSizeWords)
            {
                FlushRecursiveParts(currentSentences, ref currentWordCount, chunks, separator: " ");
                chunks.AddRange(SplitRecursiveWords(sentence));
                continue;
            }

            if (currentWordCount > 0 && currentWordCount + sentenceWordCount > _options.ChunkSizeWords)
            {
                FlushRecursiveParts(currentSentences, ref currentWordCount, chunks, separator: " ");
            }

            currentSentences.Add(sentence);
            currentWordCount += sentenceWordCount;
        }

        FlushRecursiveParts(currentSentences, ref currentWordCount, chunks, separator: " ");
        return chunks;
    }

    private IReadOnlyList<string> SplitRecursiveWords(string text)
    {
        var words = GetWords(text);
        if (words.Count == 0)
        {
            return [];
        }

        if (words.Any(word => word.Length > _options.ChunkSizeWords * 8))
        {
            return SplitRecursiveCharacters(text);
        }

        var chunks = new List<string>();
        var step = Math.Max(1, _options.ChunkSizeWords - _options.ChunkOverlapWords);
        for (var start = 0; start < words.Count; start += step)
        {
            var chunkWords = words.Skip(start).Take(_options.ChunkSizeWords).ToList();
            if (chunkWords.Count == 0)
            {
                break;
            }

            chunks.Add(string.Join(' ', chunkWords));
            if (start + _options.ChunkSizeWords >= words.Count)
            {
                break;
            }
        }

        return chunks;
    }

    private IReadOnlyList<string> SplitRecursiveCharacters(string text)
    {
        var normalizedText = Regex.Replace(text, @"\s+", " ").Trim();
        if (normalizedText.Length == 0)
        {
            return [];
        }

        var maxCharacters = Math.Max(1, _options.ChunkSizeWords * 8);
        var overlapCharacters = Math.Clamp(_options.ChunkOverlapWords * 8, 0, Math.Max(0, maxCharacters - 1));
        var step = Math.Max(1, maxCharacters - overlapCharacters);
        var chunks = new List<string>();

        for (var start = 0; start < normalizedText.Length; start += step)
        {
            var length = Math.Min(maxCharacters, normalizedText.Length - start);
            chunks.Add(normalizedText.Substring(start, length).Trim());

            if (start + maxCharacters >= normalizedText.Length)
            {
                break;
            }
        }

        return chunks;
    }

    private static void FlushRecursiveParts(List<string> parts, ref int currentWordCount, List<string> chunks, string? separator = null)
    {
        if (parts.Count == 0)
        {
            return;
        }

        chunks.Add(string.Join(separator ?? $"{Environment.NewLine}{Environment.NewLine}", parts));
        parts.Clear();
        currentWordCount = 0;
    }

    private static void AddParagraph(List<string> paragraphs, List<string> currentLines)
    {
        if (currentLines.Count == 0)
        {
            return;
        }

        paragraphs.Add(string.Join(Environment.NewLine, currentLines).Trim());
        currentLines.Clear();
    }

    private static bool IsHeadingLike(string line)
    {
        var words = GetWords(line);
        if (words.Count is 0 or > 12)
        {
            return false;
        }

        if (Regex.IsMatch(line, @"^(\d+(\.\d+)*|[A-Z]|[IVXLCDM]+)[\).]?\s+\S+"))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^(Article|Section|Policy|Procedure|Purpose|Scope|Responsibilities|Definitions|Chapter|Part)\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        var hasLowercase = line.Any(char.IsLower);
        var endsWithSentencePunctuation = Regex.IsMatch(line, @"[.!?\u3002]$");
        return !hasLowercase && !endsWithSentencePunctuation && line.Any(char.IsLetter);
    }

    private static string TakeLastWords(string text, int wordCount)
    {
        if (wordCount <= 0)
        {
            return string.Empty;
        }

        var words = GetWords(text);
        return string.Join(' ', words.Skip(Math.Max(0, words.Count - wordCount)));
    }

    private static int CountWords(string text)
    {
        return GetWords(text).Count;
    }

    private static List<string> GetWords(string text)
    {
        return Regex.Matches(text, @"\S+").Select(match => match.Value).ToList();
    }

    private static bool ShouldPreserveChunk(ParsedChunk chunk)
    {
        return chunk.ChunkType.Equals("image_caption", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldKeepShortChunk(ParsedChunk source, int wordCount)
    {
        return source.IsFormTemplate && wordCount > 0;
    }

    private static ParsedChunk CloneChunk(ParsedChunk source, string text, int chunkIndex)
    {
        return new ParsedChunk
        {
            Text = text,
            SourceFile = source.SourceFile,
            PageNumber = source.PageNumber,
            ChunkIndex = chunkIndex,
            ChunkType = source.ChunkType,
            FileType = source.FileType,
            Agent = source.Agent,
            ImagePath = source.ImagePath,
            ImagePaths = [.. source.ImagePaths],
            IsFormTemplate = source.IsFormTemplate,
            TemplatePath = source.TemplatePath
        };
    }
}
