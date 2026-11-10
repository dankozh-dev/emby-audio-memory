using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MediaBrowser.Model.Plugins;

namespace Emby.AudioMemory
{
    public sealed class Configuration : BasePluginConfiguration
    {
        public bool Enable { get; set; }
        public string[] AllowedUserIds { get; set; } = Array.Empty<string>();
        public bool DebugLogging { get; set; }
    }

    public sealed class Preference
    {
        public string UserId { get; set; } = "";
        public long SeasonId { get; set; }
        public string Title { get; set; } = "";
        public string Language { get; set; } = "";
        public long SourceEpisodeId { get; set; }
        public int SourceAudioStreamIndex { get; set; }
        public long Revision { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public bool Active { get; set; } = true;
    }

    public sealed class Backup
    {
        public string UserId { get; set; } = "";
        public long SeasonId { get; set; }
        public long EpisodeId { get; set; }
        public string MediaSourceId { get; set; } = "";
        public string StreamLayout { get; set; } = "";
        public int? OriginalIndex { get; set; }
        public int? LastWrittenIndex { get; set; }
        public int? PendingIndex { get; set; }
        public bool Pending { get; set; }
        public bool Restored { get; set; }
    }

    public sealed class State
    {
        public int SchemaVersion { get; set; } = 1;
        public List<Preference> Preferences { get; set; } = new List<Preference>();
        public List<Backup> Backups { get; set; } = new List<Backup>();
    }

    public sealed class PlanRow
    {
        public string UserId { get; set; } = "";
        public long SeasonId { get; set; }
        public long EpisodeId { get; set; }
        public int? Before { get; set; }
        public int? After { get; set; }
        public string Result { get; set; } = "";
    }

    public sealed class EventRecord
    {
        public DateTime Utc { get; set; }
        public string Event { get; set; } = "";
        public string UserId { get; set; } = "";
        public long UserInternalId { get; set; }
        public long EpisodeId { get; set; }
        public int? AudioStreamIndex { get; set; }
        public bool Automated { get; set; }
    }

    public static class TrackMatch
    {
        // Intentionally retain punctuation, dubbing markers, 18+, codec tags and commentary.
        public static string Normalize(string? value) => Regex.Replace(
            (value ?? "").Normalize(NormalizationForm.FormC).Trim(), @"\s+", " ").ToUpperInvariant();

        public static bool Matches(string? title, string? language, string? expectedTitle, string? expectedLanguage)
        {
            var t = Normalize(title); var l = Normalize(language);
            return t.Length > 0 && l.Length > 0 && t == Normalize(expectedTitle) && l == Normalize(expectedLanguage);
        }

        public static string UserKey(string value)
        {
            return Guid.TryParse(value, out var id) ? id.ToString("N") : "";
        }

        public static int? UniqueIndex(IEnumerable<AudioTrack> tracks, string title, string language)
        {
            var matches = tracks.Where(t => Matches(t.Title, t.Language, title, language)).Take(2).ToArray();
            return matches.Length == 1 ? matches[0].Index : (int?)null;
        }

        public static int? SelectIndex(IEnumerable<AudioTrack> tracks, string? title, string? language, int sourceIndex)
        {
            var audio = tracks.ToArray();
            var atIndex = audio.Where(t => t.Index == sourceIndex && t.Index >= 0).Take(2).ToArray();
            if (string.IsNullOrWhiteSpace(title))
                return atIndex.Length == 1 ? sourceIndex : (int?)null;

            var named = audio.Where(t => Matches(t.Title, t.Language, title, language)).Take(2).ToArray();
            if (named.Length == 1) return named[0].Index;
            if (named.Length > 1) return null;
            // Fall back only when a title is missing; conflicting non-empty names remain distinct.
            return atIndex.Length == 1 && string.IsNullOrWhiteSpace(atIndex[0].Title)
                ? sourceIndex : (int?)null;
        }
    }

    public static class BackupRules
    {
        public static bool Owns(Backup backup, int? current) =>
            (backup.LastWrittenIndex.HasValue && current == backup.LastWrittenIndex) ||
            (backup.Pending && backup.PendingIndex.HasValue && current == backup.PendingIndex);

        public static bool CanReconcile(Backup? backup, int? current) => backup == null
            ? !current.HasValue
            : Owns(backup, current) || (!current.HasValue && !backup.OriginalIndex.HasValue && !backup.LastWrittenIndex.HasValue);
    }

    public sealed class AudioTrack
    {
        public int Index { get; set; }
        public string? Title { get; set; }
        public string? Language { get; set; }
    }
}
