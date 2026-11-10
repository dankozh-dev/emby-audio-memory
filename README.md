# Audio Memory for Emby

[Русский](README.ru.md) · [Releases](https://github.com/dankozh-dev/emby-audio-memory/releases) · [Changelog](CHANGELOG.md)

Choose an audio track in one episode and reuse that choice throughout the same season, separately for each Emby user.
This is useful when a release contains several dubs in the same language.

**Experimental prerelease.** Server behaviour has been tested on **Emby Server 4.10.2.0**.
A real Android client changing tracks and automatically advancing to the next episode has **not yet been verified**.

## How it works

1. You switch audio tracks during playback.
2. The plugin receives Emby's `AudioTrackChange` event and remembers the choice for that user and season.
3. It saves a matching audio index for the other inactive episodes before they are played.
4. Newly added episodes receive the preference through library events and a scheduled task that runs every 15 minutes.

The plugin changes saved audio choices, leaving media files unchanged.
It sends no playback commands and preserves watched status, resume position, play count, ratings, favourites and subtitle choices when saving an audio selection.
It skips episodes currently playing in any server session.

### Matching rules

| Track metadata | Selection in the next episode |
| --- | --- |
| Saved track has a title | Match its title **and language**, ignoring case and repeated whitespace. |
| One matching titled track has a different index | Use that track's new index. |
| Saved track has no title | Reuse its exact Emby stream index if it is an audio track. |
| No title match, but the track at the saved index is unnamed | Reuse that index. |
| Several title/language matches, a conflicting non-empty title, or no suitable track | Skip the episode. |

Names such as `Studio A`, `Studio A (Dub)` and `Studio A 18+` stay distinct; the plugin does not guess studio aliases.
**Index fallback cannot identify a dub or verify its language.** It can select different audio when unnamed tracks change order between episodes.
Emby's stream index is the `Index` in media metadata, not the position in an audio-only menu.

## Install

1. Download `Emby.AudioMemory.dll` and `SHA256SUMS` from a [release](https://github.com/dankozh-dev/emby-audio-memory/releases).
2. Compare the DLL's SHA-256 hash with `SHA256SUMS`.
3. Stop Emby Server and copy the DLL into its `plugins` directory. For an update, move the previous DLL out of that directory first.
4. Start Emby Server and open **Dashboard → Plugins → Audio Memory**.
5. Add the intended users' public IDs, enable the plugin and save.
6. For each intended user, enable Emby's **Remember audio track selection** playback setting.
7. Start an episode and manually switch to the desired track during playback.

The plugin starts **disabled**, with an **empty user allowlist**.
An empty allowlist enables no users; it does not mean “everyone”.
The current configuration page uses Russian labels: **Включить** = Enable, **Идентификаторы пользователей через запятую** = comma-separated user IDs, **Сохранить** = Save.

Copy only the plugin DLL, not Emby SDK assemblies or build dependencies.
Use the server's persistent plugin directory; Docker installations need the corresponding persistent volume.
See Emby's [manual plugin installation instructions](https://emby.media/support/articles/Plugins.html) for locating the directory and setting permissions.

### Find the user IDs

Use an administrator token to call `GET /Users` on your own server's API.
For example, with `EMBY_ADMIN_TOKEN` already set in your shell:

```sh
EMBY_API_URL='https://emby.example.org/emby'
curl --fail --silent --show-error \
  --header "X-Emby-Token: $EMBY_ADMIN_TOKEN" \
  "$EMBY_API_URL/Users"
```

Find the intended account by `Name` and copy its `Id` into the plugin configuration.
Use the public GUID returned by the API, not the display name, an internal numeric user ID or an Emby Connect account ID.
GUIDs with or without hyphens are accepted.
Adjust the API base URL for your reverse proxy; do not put tokens in URLs or issue reports.

## Check that it works

After switching tracks, open the plugin configuration page: it should show a preference for that user and season.
Start another episode in the same season and check its selected audio.
Test both a fresh start and automatic “next episode”, since a client may have prepared its queue before the preference changed.

If nothing is remembered, check the allowlist, the user's playback setting and whether the client sends `AudioTrackChange`.
Temporarily enable detailed event logging or inspect the admin status endpoint below.
Automatic initial choices, ordinary progress updates and playback-stop events do not teach the plugin.

## Admin API

All routes require administrator authentication and are relative to the Emby API base URL.
Every POST request defaults to `DryRun: true`; send `false` explicitly to apply it.

| Method and route | Purpose |
| --- | --- |
| `GET /AudioMemory/Status` | Preferences, original-index records, recent events and state errors. |
| `POST /AudioMemory/Seed` | Initialize a preference from an existing episode and audio index. |
| `POST /AudioMemory/Reconcile` | Apply active preferences to eligible episodes. |
| `POST /AudioMemory/Rollback` | Disable one user/season preference and conditionally restore original indexes. |

Example bodies use fictional item IDs; replace every placeholder with your server's values:

```json
{"UserId":"<USER_GUID>","EpisodeId":1234,"AudioStreamIndex":3,"DryRun":true}
```

```json
{"DryRun":true}
```

```json
{"UserId":"<USER_GUID>","SeasonId":1200,"DryRun":true}
```

These are the bodies for `Seed`, `Reconcile` and `Rollback`, respectively.
`Seed` derives the season and track metadata from the specified episode; it does not accept an arbitrary studio name.
Seed and reconciliation require the plugin and user to be enabled.
Rollback is also available while the plugin is disabled.

## Disable, restore or uninstall

Disabling the plugin stops further changes but leaves already saved audio choices in Emby.
To restore a season, disable the plugin, preview `Rollback` with `DryRun: true`, then repeat with `false`.
Rollback restores only indexes still owned by the plugin and only when the media source and audio layout match its record.
It preserves later independent selections and skips active episodes; retry those after playback stops.
After any required rollback, stop Emby, remove `Emby.AudioMemory.dll` and start Emby again.

State is stored at `<Emby DataPath>/audio-memory/state.json`.
Keep this file when updating or when you may need rollback; include it in server backups.
Writes record the original index before changing Emby, and state-file errors disable further writes.

## Compatibility and limits

- Built against **Emby SDK 4.9.1.90**, targeting **.NET Standard 2.0**; server integration tested on **4.10.2.0**. Other server versions are unverified.
- A real Android track-change and queued-next-episode flow remains unverified; a client can override the server's saved choice with a cached index.
- The client must report `AudioTrackChange`. Selecting a track on an item's details page before playback may not produce that event.
- Preferences are per user and season, shared by that user's devices. The latest explicit change received wins; preferences do not carry across seasons.
- Episodes with multiple media sources/versions are skipped. Source or audio-layout changes after a plugin write are also skipped.
- Named source tracks need a language tag. Ambiguous named matches are skipped; unnamed tracks use the index fallback described above.
- Public Emby APIs save the complete user-data record. Fresh reads, copies and activity checks reduce concurrency risks but are not a server-wide transaction with playback updates.
- This is an Emby server plugin. **Jellyfin compatibility is not claimed.**

## Build and test

Install the **.NET 8 SDK** and use Bash, then run from the repository root:

```sh
bash build.sh
```

The script restores packages, builds the plugin, runs the unit and behaviour test programs, and writes `artifacts/Emby.AudioMemory.dll` with `SHA256SUMS`.
No private package feed or server credentials are needed to build.
The .NET 8 SDK is a build requirement; Emby supplies the server runtime.

To run the test programs separately:

```sh
dotnet run --project tests/Unit/Unit.csproj -c Release
dotnet run --project tests/Behavior/Behavior.csproj -c Release
```

The behaviour harness uses SDK substitutes to exercise lifecycle and failure cases; it does not replace testing on a real Emby server and client.
See [CONTRIBUTING.md](CONTRIBUTING.md) for the change and validation expectations.
