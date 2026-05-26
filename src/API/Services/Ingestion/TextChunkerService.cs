using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Services.Ingestion;

public class TextChunkerService(IOptions<IngestionOptions> options)
{
    private readonly IngestionOptions _options = options.Value;

    public IReadOnlyList<ParsedChunk> Chunk(IReadOnlyList<ParsedChunk> chunks)
    {
        return _options.ChunkingStrategy.Equals("FixedSize", StringComparison.OrdinalIgnoreCase)
            ? ChunkFixedSize(chunks)
            : chunks;
    }

    public IReadOnlyList<ParsedChunk> ChunkFixedSize(IReadOnlyList<ParsedChunk> chunks)
    {
        var output = new List<ParsedChunk>();
        var chunkIndex = 0;

        foreach (var chunk in chunks)
        {
            if (chunk.ChunkType.Equals("image_caption", StringComparison.OrdinalIgnoreCase))
            {
                output.Add(CloneChunk(chunk, chunk.Text, chunkIndex++));
                continue;
            }

            var words = Regex.Matches(chunk.Text, @"\S+").Select(match => match.Value).ToList();
            if (words.Count < _options.MinimumChunkWords)
            {
                continue;
            }

            var step = Math.Max(1, _options.ChunkSizeWords - _options.ChunkOverlapWords);
            for (var start = 0; start < words.Count; start += step)
            {
                var text = string.Join(' ', words.Skip(start).Take(_options.ChunkSizeWords));
                var wordCount = Regex.Matches(text, @"\S+").Count;
                if (wordCount < _options.MinimumChunkWords)
                {
                    continue;
                }

                output.Add(CloneChunk(chunk, text, chunkIndex++));

                if (start + _options.ChunkSizeWords >= words.Count)
                {
                    break;
                }
            }
        }

        return output;
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
            ImagePaths = [.. source.ImagePaths]
        };
    }
}
