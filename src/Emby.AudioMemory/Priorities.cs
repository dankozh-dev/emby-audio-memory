using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Emby.AudioMemory
{
    public sealed class PriorityRule
    {
        public string Kind { get; set; } = "studio";
        public string Studio { get; set; } = "";
        public bool Adult { get; set; }
    }
    public sealed class PriorityProfile
    {
        public string Language { get; set; } = "rus";
        public PriorityRule[] Rules { get; set; } = Array.Empty<PriorityRule>();
    }
    public sealed class UserPriorityProfile
    {
        public string UserId { get; set; } = "";
        public PriorityProfile Profile { get; set; } = new PriorityProfile();
    }
    public sealed class StudioDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string[] Aliases { get; set; } = Array.Empty<string>();
    }
    public static class PriorityMatch
    {
        public static readonly StudioDefinition[] Studios = {
            Studio("hdrezka", "HDRezka", "HDRezka", "HDRezka Studio", "HDr"),
            Studio("tvshows", "TVShows", "TVShows", "TVS", "TVShowsr"),
            Studio("omskbird", "OmskBird", "OmskBird", "OBird", "OmsrkBird"),
            Studio("lostfilm", "LostFilm", "LostFilm"),
            Studio("le-production", "LE-Production", "LE-Production", "LE", "LE_Proper"),
            Studio("nemahov", "Ю. Немахов", "Немахов"),
            Studio("kinomania", "Киномания", "Киномания"),
            Studio("zone-vision", "Zone Vision Studio", "Zone Vision Studio"),
            Studio("winmedia", "WinMedia", "WinMedia"),
            Studio("jaskier", "Jaskier", "Jaskier"),
            Studio("viruseproject", "ViruseProject", "ViruseProject")
        };
        private static StudioDefinition Studio(string id, string name, params string[] aliases) =>
            new StudioDefinition { Id = id, Name = name, Aliases = aliases };
        public static string Language(string? language)
        {
            switch ((language ?? "").Trim().ToLowerInvariant())
            {
                case "ru": case "rus": return "rus";
                case "en": case "eng": return "eng";
                case "uk": case "ukr": return "ukr";
                default: return (language ?? "").Trim().ToLowerInvariant();
            }
        }
        public static bool IsAdult(string? title) => Regex.IsMatch(title ?? "", @"(?<!\d)18\s*\+");
        private static bool ContainsAlias(string title, string alias) => !string.IsNullOrWhiteSpace(alias) &&
            Regex.IsMatch(TrackMatch.Normalize(title), @"(?<![\p{L}\p{N}])" + Regex.Escape(TrackMatch.Normalize(alias)) + @"(?![\p{L}\p{N}])");
        public static bool IsDub(string? title) => Regex.IsMatch(TrackMatch.Normalize(title),
            @"(?<![\p{L}\p{N}])(DUB|DUBBED|ДУБЛЯЖ|ДУБЛИРОВАН[\p{L}]*)(?![\p{L}\p{N}])|[\(\[]\s*D\s*[\)\]]");
        public static int? SelectIndex(IEnumerable<AudioTrack> tracks, PriorityProfile? profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Language)) return null;
            var audio = tracks.Where(t => t.Index >= 0 && Language(t.Language) == Language(profile.Language)).ToArray();
            foreach (var rule in profile.Rules ?? Array.Empty<PriorityRule>())
            {
                if (rule == null) continue;
                var selected = audio.Where(t => Matches(t.Title, rule))
                    .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Index).FirstOrDefault();
                if (selected != null) return selected.Index;
            }
            return null;
        }
        private static bool Matches(string? title, PriorityRule rule)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            if (rule.Kind == "adult") return IsAdult(title);
            if (IsAdult(title) != rule.Adult) return false;
            if (rule.Kind == "dub") return !rule.Adult && IsDub(title);
            if (rule.Kind == "custom") return ContainsAlias(title!, rule.Studio);
            if (rule.Kind != "studio") return false;
            var studio = Studios.FirstOrDefault(s => s.Id == rule.Studio);
            return studio != null && studio.Aliases.Any(a => ContainsAlias(title!, a));
        }
    }
}
