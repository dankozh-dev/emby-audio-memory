using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Session;

namespace Emby.AudioMemory
{
    public sealed class Plugin : BasePlugin<Configuration>, IHasWebPages, IDisposable
    {
        public static Plugin? Instance { get; private set; }
        public override Guid Id => new Guid("b7a7922e-5271-4d47-87ec-698aa4718c46");
        public override string Name => "Audio Memory";
        public override string Description => "Remembers explicitly selected audio by user and season.";
        private readonly ILibraryManager library;
        private readonly IUserManager users;
        private readonly IUserDataManager userdata;
        private readonly ISessionManager sessions;
        private readonly IJsonSerializer json;
        private readonly ILogger log;
        private readonly string statePath;
        private readonly object gate = new object();
        private readonly List<EventRecord> events = new List<EventRecord>();
        private State state;
        private string? error;
        private bool disposed;
        private long revision;

        public Plugin(IApplicationPaths paths, IXmlSerializer xml, IJsonSerializer json,
            ILibraryManager library, IUserManager users, IUserDataManager userdata,
            ISessionManager sessions, ILogManager logs) : base(paths, xml)
        {
            this.library = library; this.users = users; this.userdata = userdata;
            this.sessions = sessions; this.json = json; log = logs.GetLogger("AudioMemory");
            statePath = Path.Combine(paths.DataPath, "audio-memory", "state.json");
            state = new State();
            try
            {
                if (File.Exists(statePath)) state = json.DeserializeFromString<State>(File.ReadAllText(statePath));
                if (state == null || state.SchemaVersion != 1 || state.Preferences == null || state.Backups == null)
                    throw new InvalidDataException("Unsupported or invalid state file.");
                revision = state.Preferences.Select(p => p.Revision).DefaultIfEmpty(0).Max();
            }
            catch (Exception ex) { error = "State could not be loaded; writes disabled."; log.ErrorException(error, ex); }
            Instance = this;
            sessions.PlaybackProgress += OnProgress;
            library.ItemAdded += OnItemChanged;
            library.ItemUpdated += OnItemChanged;
        }

        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {
            new PluginPageInfo { Name = "AudioMemory", EmbeddedResourcePath = "Emby.AudioMemory.Configuration.html" },
            new PluginPageInfo { Name = "AudioMemoryJs", EmbeddedResourcePath = "Emby.AudioMemory.Configuration.js" }
        };

        private bool Allowed(string userId) => Configuration.Enable &&
            !string.IsNullOrEmpty(TrackMatch.UserKey(userId)) &&
            (Configuration.EnableForAllUsers ||
             (Configuration.AllowedUserIds ?? Array.Empty<string>()).Any(x => TrackMatch.UserKey(x) == TrackMatch.UserKey(userId)));

        private void EnsureWritable()
        {
            if (disposed) throw new ObjectDisposedException(nameof(Plugin));
            if (error != null) throw new InvalidOperationException(error);
            if (!Configuration.Enable) throw new InvalidOperationException("Audio Memory is disabled.");
        }

        private void SaveState()
        {
            // State is flushed before userdata mutation so recovery never loses the original index.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                var tmp = statePath + ".tmp";
                using (var writer = new StreamWriter(new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)))
                {
                    writer.Write(json.SerializeToString(state)); writer.Flush();
                    ((FileStream)writer.BaseStream).Flush(true);
                }
                if (File.Exists(statePath)) File.Replace(tmp, statePath, null);
                else File.Move(tmp, statePath);
            }
            catch (Exception ex)
            {
                error = "State could not be saved; writes disabled.";
                log.ErrorException(error, ex); throw;
            }
        }

        private void OnProgress(object sender, PlaybackProgressEventArgs e)
        {
            // Snapshot mutable event/session fields before waiting for another operation.
            if (!(e.Item is Episode episode) || e.Session == null) return;
            var userId = TrackMatch.UserKey(e.Session.UserId);
            if (!Allowed(userId)) return;
            var itemId = episode.InternalId;
            var sourceId = e.MediaSourceId;
            var index = e.PlaySession?.PlayState?.AudioStreamIndex;
            var eventName = e.EventName;
            var automated = e.IsAutomated;
            var userInternalId = e.Session.UserInternalId;
            var nextRevision = Interlocked.Increment(ref revision);
            try
            {
                lock (gate)
                {
                    if (disposed || error != null || !Allowed(userId)) return;
                    if (eventName == ProgressEvent.AudioTrackChange || Configuration.DebugLogging)
                    {
                        events.Add(new EventRecord { Utc = DateTime.UtcNow, Event = eventName.ToString(), UserId = userId,
                            UserInternalId = userInternalId, EpisodeId = itemId, AudioStreamIndex = index, Automated = automated });
                        if (events.Count > 80) events.RemoveAt(0);
                        log.Info("AudioMemory event={0} user={1} internal={2} item={3} audio={4} automated={5}",
                            eventName, userId, userInternalId, itemId, index, automated);
                    }
                    if (eventName != ProgressEvent.AudioTrackChange || automated || !index.HasValue) return;
                    var user = users.GetUserById(new Guid(userId));
                    var current = library.GetItemById(itemId) as Episode;
                    if (user == null || current == null) return;
                    var preference = MakePreference(user, current, index.Value, sourceId, nextRevision);
                    if (preference == null) return;
                    var old = FindPreference(userId, preference.SeasonId);
                    if (old != null && old.Revision >= nextRevision) return;
                    // The user has explicitly changed this episode after any older plugin write.
                    // Retire that old rollback claim, preserving the newly chosen track.
                    foreach (var backup in state.Backups.Where(b => b.UserId == userId && b.EpisodeId == itemId && !b.Restored))
                        backup.Restored = true;
                    SetPreference(preference);
                    SaveState();
                    ApplySeason(preference, false, true, itemId);
                }
            }
            catch (Exception ex) { log.ErrorException("AudioMemory audio selection was not applied.", ex); }
        }

        private void OnItemChanged(object sender, ItemChangeEventArgs e)
        {
            if (!(e.Item is Episode)) return;
            var itemId = e.Item.InternalId;
            try
            {
                lock (gate)
                {
                    if (disposed || error != null || !Configuration.Enable) return;
                    var episode = library.GetItemById(itemId) as Episode;
                    var seasonId = episode?.Season?.InternalId;
                    if (episode == null || !seasonId.HasValue) return;
                    foreach (var p in state.Preferences.Where(p => p.Active && p.SeasonId == seasonId && Allowed(p.UserId)).ToArray())
                    {
                        var user = users.GetUserById(new Guid(p.UserId));
                        if (user != null) ApplyEpisode(user, episode, p, false, false, 0);
                    }
                }
            }
            catch (Exception ex) { log.ErrorException("AudioMemory new episode was not updated.", ex); }
        }

        private Preference? FindPreference(string userId, long seasonId) => state.Preferences.FirstOrDefault(p => p.UserId == userId && p.SeasonId == seasonId);
        private void SetPreference(Preference p)
        {
            state.Preferences.RemoveAll(x => x.UserId == p.UserId && x.SeasonId == p.SeasonId);
            state.Preferences.Add(p);
        }

        private List<MediaSourceInfo> Sources(User user, Episode episode)
        {
            return episode.GetMediaSources(user, true, false, true, false, episode.CollectionFolders,
                episode.LibraryOptions, CancellationToken.None);
        }

        private Preference? MakePreference(User user, Episode episode, int index, string? sourceId, long rev)
        {
            var season = episode.Season;
            if (season == null) return null;
            var sources = Sources(user, episode);
            if (sources.Count != 1) return null;
            var source = sources[0];
            if (!string.IsNullOrEmpty(sourceId) && source.Id != sourceId) return null;
            var stream = source.MediaStreams.FirstOrDefault(t => t.Type == MediaStreamType.Audio && t.Index == index);
            if (stream == null || index < 0) return null;
            if (!string.IsNullOrWhiteSpace(stream.Title))
            {
                if (string.IsNullOrWhiteSpace(stream.Language)) return null;
                if (source.MediaStreams.Count(t => t.Type == MediaStreamType.Audio && TrackMatch.Matches(t.Title, t.Language, stream.Title, stream.Language)) != 1) return null;
            }
            else if (source.MediaStreams.Count(t => t.Type == MediaStreamType.Audio && t.Index == index) != 1) return null;
            return new Preference { UserId = user.Id.ToString("N"), SeasonId = season.InternalId, Title = stream.Title ?? "",
                Language = stream.Language ?? "", SourceEpisodeId = episode.InternalId, SourceAudioStreamIndex = index,
                Revision = rev, UpdatedUtc = DateTime.UtcNow, Active = true };
        }

        private bool IsPlaying(User user, Episode episode)
        {
            // Conservatively skip if any session is playing the item, even another user.
            // This also covers shared sessions and avoids racing server progress writers.
            var id = episode.InternalId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return sessions.Sessions.Any(s => s.NowPlayingItem != null &&
                (s.NowPlayingItem.Id == id || s.NowPlayingItem.Id == episode.Id.ToString("N")));
        }

        private static string Layout(MediaSourceInfo source) => string.Join("\n", source.MediaStreams
            .Where(t => t.Type == MediaStreamType.Audio).OrderBy(t => t.Index)
            .Select(t => t.Index + "|" + TrackMatch.Normalize(t.Language) + "|" + TrackMatch.Normalize(t.Title)));

        private PlanRow ApplyEpisode(User user, Episode episode, Preference p, bool dryRun, bool explicitChange, long excludedId)
        {
            var row = new PlanRow { UserId = p.UserId, SeasonId = p.SeasonId, EpisodeId = episode.InternalId };
            if (episode.Season?.InternalId != p.SeasonId) { row.Result = "different-season"; return row; }
            if (episode.InternalId == excludedId || IsPlaying(user, episode)) { row.Result = "active-playback"; return row; }
            var sources = Sources(user, episode);
            if (sources.Count != 1) { row.Result = "multiple-or-missing-sources"; return row; }
            var source = sources[0];
            var after = TrackMatch.SelectIndex(source.MediaStreams.Where(t => t.Type == MediaStreamType.Audio)
                .Select(t => new AudioTrack { Index = t.Index, Title = t.Title, Language = t.Language }), p.Title, p.Language, p.SourceAudioStreamIndex);
            if (!after.HasValue) { row.Result = "missing-or-ambiguous-match"; return row; }
            var data = userdata.GetUserData(user, episode);
            row.Before = data.AudioStreamIndex; row.After = after;
            if (data.AudioStreamIndex == after) { row.Result = "already-selected"; return row; }
            var backup = state.Backups.LastOrDefault(b => b.UserId == p.UserId && b.EpisodeId == episode.InternalId && !b.Restored);
            var layout = Layout(source);
            if (backup != null && (backup.MediaSourceId != source.Id || backup.StreamLayout != layout))
            { row.Result = "source-changed"; return row; }
            if (!explicitChange && !BackupRules.CanReconcile(backup, data.AudioStreamIndex))
            { row.Result = "selection-changed-outside-plugin"; return row; }
            if (dryRun) { row.Result = "would-write"; return row; }
            if (IsPlaying(user, episode)) { row.Result = "active-playback"; return row; }
            // Re-read immediately before saving; no playback/favourite/rating fields are invented or reset.
            data = userdata.GetUserData(user, episode);
            row.Before = data.AudioStreamIndex;
            if (!explicitChange && !BackupRules.CanReconcile(backup, data.AudioStreamIndex))
            { row.Result = "selection-changed-outside-plugin"; return row; }
            if (backup == null)
            {
                backup = new Backup { UserId = p.UserId, SeasonId = p.SeasonId, EpisodeId = episode.InternalId,
                    MediaSourceId = source.Id, StreamLayout = layout, OriginalIndex = data.AudioStreamIndex };
                state.Backups.Add(backup);
            }
            // SDK GetUserData may return a shared mutable object; retain the scalar value
            // before the durable write instead of comparing two potentially aliased records.
            var expectedBeforeWrite = data.AudioStreamIndex;
            backup.Pending = true; backup.PendingIndex = after; SaveState();
            // The state fsync may take time: check activity and audio ownership once more,
            // and clone the newest record rather than a pre-fsync playback snapshot.
            var latest = userdata.GetUserData(user, episode);
            if (IsPlaying(user, episode) || latest.AudioStreamIndex != expectedBeforeWrite)
            {
                backup.Pending = false; backup.PendingIndex = null; SaveState();
                row.Result = "concurrent-playback-or-selection-change"; return row;
            }
            data = latest;
            var updated = json.DeserializeFromString<UserItemData>(json.SerializeToString(data));
            updated.AudioStreamIndex = after;
            userdata.SaveUserData(user, episode, updated, UserDataSaveReason.Import, CancellationToken.None);
            backup.LastWrittenIndex = after; backup.Pending = false; backup.PendingIndex = null; SaveState();
            row.Result = "written";
            return row;
        }

        private List<PlanRow> ApplySeason(Preference p, bool dryRun, bool explicitChange, long excludedId = 0)
        {
            var user = users.GetUserById(new Guid(p.UserId));
            var season = library.GetItemById(p.SeasonId) as Season;
            if (user == null || season == null) throw new InvalidOperationException("User or season no longer exists.");
            return library.GetItemList(new InternalItemsQuery { Parent = season, Recursive = true, IncludeItemTypes = new[] { "Episode" } })
                .OfType<Episode>().Select(e => ApplyEpisode(user, e, p, dryRun, explicitChange, excludedId)).ToList();
        }

        public object Status()
        {
            lock (gate) return new { Enabled = Configuration.Enable, EnableForAllUsers = Configuration.EnableForAllUsers, AllowedUserIds = Configuration.AllowedUserIds,
                Error = error, StatePath = statePath,
                Preferences = json.DeserializeFromString<List<Preference>>(json.SerializeToString(state.Preferences)),
                Backups = json.DeserializeFromString<List<Backup>>(json.SerializeToString(state.Backups)), RecentEvents = events.ToArray() };
        }

        public object Seed(string userId, long episodeId, int index, bool dryRun)
        {
            lock (gate)
            {
                EnsureWritable(); userId = TrackMatch.UserKey(userId);
                if (!Allowed(userId)) throw new InvalidOperationException("User is not enabled.");
                var user = users.GetUserById(new Guid(userId));
                var episode = library.GetItemById(episodeId) as Episode;
                if (user == null || episode == null) throw new InvalidOperationException("User or episode not found.");
                var p = MakePreference(user, episode, index, null, Interlocked.Increment(ref revision));
                if (p == null) throw new InvalidOperationException("Episode needs one media source and an audio track with either a unique title/language or an empty title and valid index.");
                if (!dryRun) { SetPreference(p); SaveState(); }
                return new { DryRun = dryRun, Preference = p, Episodes = ApplySeason(p, dryRun, true) };
            }
        }

        public object Reconcile(bool dryRun)
        {
            lock (gate)
            {
                EnsureWritable();
                var rows = new List<PlanRow>();
                foreach (var p in state.Preferences.Where(p => p.Active && Allowed(p.UserId)).ToArray())
                    rows.AddRange(ApplySeason(p, dryRun, false));
                return new { DryRun = dryRun, Episodes = rows };
            }
        }

        public object Rollback(string userId, long seasonId, bool dryRun)
        {
            lock (gate)
            {
                // Rollback remains available when the plugin has been disabled in configuration.
                if (disposed || error != null) throw new InvalidOperationException(error ?? "Plugin disposed.");
                userId = TrackMatch.UserKey(userId);
                if (string.IsNullOrEmpty(userId)) throw new ArgumentException("Invalid user ID.");
                var user = users.GetUserById(new Guid(userId)) ?? throw new InvalidOperationException("User not found.");
                var rows = new List<PlanRow>();
                if (!dryRun)
                {
                    var p = FindPreference(userId, seasonId); if (p != null) p.Active = false;
                    SaveState();
                }
                foreach (var b in state.Backups.Where(b => b.UserId == userId && b.SeasonId == seasonId && !b.Restored))
                {
                    var row = new PlanRow { UserId = userId, SeasonId = seasonId, EpisodeId = b.EpisodeId, After = b.OriginalIndex };
                    rows.Add(row);
                    var episode = library.GetItemById(b.EpisodeId) as Episode;
                    if (episode == null) { row.Result = "missing-episode"; continue; }
                    if (IsPlaying(user, episode)) { row.Result = "active-playback"; continue; }
                    var sources = Sources(user, episode);
                    if (sources.Count != 1 || sources[0].Id != b.MediaSourceId || Layout(sources[0]) != b.StreamLayout)
                    { row.Result = "source-changed"; continue; }
                    var data = userdata.GetUserData(user, episode); row.Before = data.AudioStreamIndex;
                    if (data.AudioStreamIndex == b.OriginalIndex)
                    {
                        row.Result = "already-original";
                        if (!dryRun) { b.Restored = true; b.Pending = false; b.PendingIndex = null; SaveState(); }
                        continue;
                    }
                    if (!BackupRules.Owns(b, data.AudioStreamIndex)) { row.Result = "selection-changed-outside-plugin"; continue; }
                    if (dryRun) { row.Result = "would-restore"; continue; }
                    if (IsPlaying(user, episode)) { row.Result = "active-playback"; continue; }
                    data = userdata.GetUserData(user, episode);
                    if (!BackupRules.Owns(b, data.AudioStreamIndex)) { row.Result = "selection-changed-outside-plugin"; continue; }
                    var restored = json.DeserializeFromString<UserItemData>(json.SerializeToString(data));
                    restored.AudioStreamIndex = b.OriginalIndex;
                    userdata.SaveUserData(user, episode, restored, UserDataSaveReason.Import, CancellationToken.None);
                    b.Restored = true; b.Pending = false; b.PendingIndex = null; SaveState(); row.Result = "restored";
                }
                return new { DryRun = dryRun, Episodes = rows };
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                sessions.PlaybackProgress -= OnProgress;
                library.ItemAdded -= OnItemChanged;
                library.ItemUpdated -= OnItemChanged;
                if (ReferenceEquals(Instance, this)) Instance = null;
            }
        }
    }
}
