using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Query;

public class FormDownloadEnrichmentService(FormRegistryService formRegistry)
{
    public List<FormDownloadRef> FindDownloadRefs(string fullAnswerText, IReadOnlyList<SourceRef> sources)
    {
        var download = formRegistry.FindByAlias(fullAnswerText);
        if (download is null || sources.Any(source => SameForm(source.FormDownload, download)))
        {
            return [];
        }

        return [download];
    }

    private static bool SameForm(FormDownloadRef? left, FormDownloadRef right)
    {
        return left is not null
            && string.Equals(left.FormName, right.FormName, StringComparison.OrdinalIgnoreCase);
    }
}
