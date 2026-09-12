using System.Text.RegularExpressions;

namespace VideoShelf.TransferHost;

internal static class TorrentVideoSelection
{
    static readonly Regex EpisodePattern = new(@"(?ix)(?:\bS(?<s>\d{1,2})[ ._-]*E(?<e>\d{1,3})\b|\b(?<s2>\d{1,2})x(?<e2>\d{1,3})\b|\bSeason[ ._-]*(?<s3>\d{1,2}).{0,12}?Episode[ ._-]*(?<e3>\d{1,3})\b)", RegexOptions.Compiled);
    static readonly Regex SeasonPattern = new(@"(?ix)(?:\bS(?<s>\d{1,2})\b|\bSeason[ ._-]*(?<s2>\d{1,2})\b)", RegexOptions.Compiled);
    static readonly string[] AuxiliaryMarkers =
    {
        "sample", "trailer", "preview", "featurette", "behind the scenes", "behind.the.scenes",
        "deleted scene", "deleted.scenes", "special feature", "special.features", "bonus", "extras/", "extras\\"
    };

    public static readonly IComparer<string> NaturalPathComparer = Comparer<string>.Create(CompareNatural);

    public static int ChooseDefaultIndex(string releaseTitle, IReadOnlyList<string> naturallyOrderedPaths)
    {
        if (naturallyOrderedPaths == null || naturallyOrderedPaths.Count == 0) return -1;
        if (naturallyOrderedPaths.Count == 1) return 0;

        bool hasReleaseEpisode = TryEpisode(releaseTitle, out int releaseSeason, out int releaseEpisode);
        int? releaseSeasonOnly = hasReleaseEpisode ? releaseSeason : TrySeason(releaseTitle, out int season) ? season : null;
        string[] titleTokens = MeaningfulTokens(releaseTitle);

        int bestIndex = 0;
        int bestScore = int.MinValue;
        for (int i = 0; i < naturallyOrderedPaths.Count; i++)
        {
            string path = naturallyOrderedPaths[i] ?? string.Empty;
            int score = 0;

            if (IsAuxiliary(path)) score -= 100000;

            bool hasFileEpisode = TryEpisode(path, out int fileSeason, out int fileEpisode);
            if (hasReleaseEpisode)
            {
                if (hasFileEpisode && fileSeason == releaseSeason && fileEpisode == releaseEpisode) score += 200000;
                else if (hasFileEpisode && fileSeason == releaseSeason) score += 10000;
                else if (hasFileEpisode) score -= 5000;
            }
            else if (releaseSeasonOnly.HasValue && hasFileEpisode)
            {
                score += fileSeason == releaseSeasonOnly.Value ? 50000 : -3000;
            }

            string normalizedPath = Normalize(path);
            foreach (string token in titleTokens)
                if (normalizedPath.Contains(token, StringComparison.OrdinalIgnoreCase)) score += 250;

            // Natural order is already earliest-episode-first. Keep a small preference for earlier entries
            // so complete-series and season packs start at E01 rather than whichever file is largest.
            score -= i;

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    static bool IsAuxiliary(string path)
    {
        string value = (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
        return AuxiliaryMarkers.Any(marker => value.Contains(marker.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
    }

    static bool TryEpisode(string value, out int season, out int episode)
    {
        season = 0; episode = 0;
        Match match = EpisodePattern.Match(value ?? string.Empty);
        if (!match.Success) return false;
        string s = match.Groups["s"].Success ? match.Groups["s"].Value : match.Groups["s2"].Success ? match.Groups["s2"].Value : match.Groups["s3"].Value;
        string e = match.Groups["e"].Success ? match.Groups["e"].Value : match.Groups["e2"].Success ? match.Groups["e2"].Value : match.Groups["e3"].Value;
        return int.TryParse(s, out season) && int.TryParse(e, out episode);
    }

    static bool TrySeason(string value, out int season)
    {
        season = 0;
        Match match = SeasonPattern.Match(value ?? string.Empty);
        if (!match.Success) return false;
        string s = match.Groups["s"].Success ? match.Groups["s"].Value : match.Groups["s2"].Value;
        return int.TryParse(s, out season);
    }

    static string[] MeaningfulTokens(string value)
    {
        string[] stop = { "the", "and", "season", "complete", "series", "episode", "1080p", "720p", "2160p", "webrip", "web", "bluray", "x264", "x265", "h264", "h265", "hevc", "aac", "ddp", "proper", "repack" };
        return Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length >= 4 && !stop.Contains(x, StringComparer.OrdinalIgnoreCase) && !Regex.IsMatch(x, @"^s\d{1,2}(?:e\d{1,3})?$", RegexOptions.IgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static string Normalize(string value) => Regex.Replace((value ?? string.Empty).ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

    static int CompareNatural(string? left, string? right)
    {
        left ??= string.Empty; right ??= string.Empty;
        int i = 0, j = 0;
        while (i < left.Length && j < right.Length)
        {
            if (char.IsDigit(left[i]) && char.IsDigit(right[j]))
            {
                int i0 = i, j0 = j;
                while (i < left.Length && char.IsDigit(left[i])) i++;
                while (j < right.Length && char.IsDigit(right[j])) j++;
                string a = left.Substring(i0, i - i0).TrimStart('0');
                string b = right.Substring(j0, j - j0).TrimStart('0');
                if (a.Length == 0) a = "0";
                if (b.Length == 0) b = "0";
                int lengthCompare = a.Length.CompareTo(b.Length);
                if (lengthCompare != 0) return lengthCompare;
                int numericCompare = string.Compare(a, b, StringComparison.Ordinal);
                if (numericCompare != 0) return numericCompare;
                int rawLengthCompare = (i - i0).CompareTo(j - j0);
                if (rawLengthCompare != 0) return rawLengthCompare;
                continue;
            }

            int charCompare = char.ToUpperInvariant(left[i]).CompareTo(char.ToUpperInvariant(right[j]));
            if (charCompare != 0) return charCompare;
            i++; j++;
        }
        return (left.Length - i).CompareTo(right.Length - j);
    }

    public static void SelfTest()
    {
        string[] unordered =
        {
            "Season 03/Breaking.Bad.S03E10.1080p.mkv",
            "Sample/Breaking.Bad.sample.mkv",
            "Season 03/Breaking.Bad.S03E02.1080p.mkv",
            "Season 03/Breaking.Bad.S03E05.1080p.mkv",
            "Season 03/Breaking.Bad.S03E01.1080p.mkv"
        };
        string[] ordered = unordered.OrderBy(x => x, NaturalPathComparer).ToArray();
        if (Array.IndexOf(ordered, "Season 03/Breaking.Bad.S03E02.1080p.mkv") > Array.IndexOf(ordered, "Season 03/Breaking.Bad.S03E10.1080p.mkv"))
            throw new InvalidOperationException("Natural torrent video ordering regressed.");

        int exact = ChooseDefaultIndex("Breaking Bad S03E05 1080p", ordered);
        if (exact < 0 || !ordered[exact].Contains("S03E05", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Episode-aware torrent selection failed the Breaking Bad S03E05 regression case.");

        string[] complete = new[]
        {
            "Extras/Trailer.mkv",
            "Season 02/Breaking.Bad.S02E01.mkv",
            "Season 01/Breaking.Bad.S01E02.mkv",
            "Season 01/Breaking.Bad.S01E01.mkv"
        }.OrderBy(x => x, NaturalPathComparer).ToArray();
        int first = ChooseDefaultIndex("Breaking Bad Complete Series S01-S05", complete);
        if (first < 0 || !complete[first].Contains("S01E01", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Complete-series torrent selection did not start at the first real episode.");

        string[] seasonPack = new[]
        {
            "Season 01/Breaking.Bad.S01E01.mkv",
            "Season 02/Breaking.Bad.S02E02.mkv",
            "Season 02/Breaking.Bad.S02E01.mkv"
        }.OrderBy(x => x, NaturalPathComparer).ToArray();
        int seasonTwo = ChooseDefaultIndex("Breaking Bad Season 2 Complete 1080p", seasonPack);
        if (seasonTwo < 0 || !seasonPack[seasonTwo].Contains("S02E01", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Season-pack torrent selection did not choose the first matching season episode.");
    }
}
