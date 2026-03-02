using FuzzySharp;
using FuzzySharp.SimilarityRatio;
using FuzzySharp.SimilarityRatio.Scorer.StrategySensitive;
using TimeshiitCli.Data;

namespace TimeshiitCli.Commands.Timelogs;

public class JobMatcher
{
    private readonly List<JobItem> _items;

    // Composite search key per item: "projectName | jobName"
    private readonly List<string> _keys;

    // Cache: composite query string → best JobItem
    private readonly Dictionary<string, JobItem> _cache = new(StringComparer.OrdinalIgnoreCase);

    public JobMatcher(List<JobItem> items)
    {
        _items = items;
        _keys = items
            .Select(i => $"{i.ProjectName} | {i.JobName}")
            .ToList();
    }

    public JobItem FindBest(string projectName, string taskName)
    {
        var query = $"{projectName.Trim()} {taskName.Trim()}";

        if (_cache.TryGetValue(query, out var cached))
            return cached;

        var result = Process.ExtractOne(
            query,
            _keys,
            scorer: ScorerCache.Get<TokenSetScorer>());

        var best = _items[result.Index];
        _cache[query] = best;
        return best;
    }
}