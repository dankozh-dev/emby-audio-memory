# Contributing

## Report a problem

Include the plugin, Emby Server and client versions; explain whether the failure occurs on a fresh start or automatic next-episode playback.
Provide a short reproduction, expected/actual audio choice, and a sanitized list of relevant streams: `Index`, `Title` and `Language`.
Mention whether the source has multiple versions and whether titles are missing.

Useful diagnostics are the relevant `AudioMemory` log lines and the affected part of `/AudioMemory/Status`.
Remove tokens, passwords, real user IDs, addresses, filesystem paths and unrelated viewing history before posting.
Do not attach media files or a complete server database.

## Build

Use the .NET 8 SDK and Bash:

```sh
bash build.sh
```

Plugin source is in `src/Emby.AudioMemory`; test programs are in `tests/Unit` and `tests/Behavior`.
The plugin targets .NET Standard 2.0 and references Emby SDK 4.9.1.90.
Keep package versions explicit; do not add machine-specific paths, private feeds or credentials.

## Make a change

Keep changes focused and add a regression test for a concrete behaviour change or bug.
Update both README versions when user-visible behaviour changes, and add a changelog entry.
Explain the trigger, resulting behaviour and validation in the pull request.

Preserve these boundaries:

- Explicit non-automated `AudioTrackChange` is the learning signal; ordinary progress and initial playback must not become preferences.
- Preferences remain isolated by user and season, and writes require the allowlist.
- Different non-empty titles, dubbing markers and commentary tracks stay distinct; ambiguous matches are skipped.
- Only `AudioStreamIndex` is intentionally changed; preserve all other user data and skip active playback.
- Record recoverable state before changing Emby; keep conditional rollback and interrupted-write recovery intact.
- Administrator-only routes remain protected, with mutation requests defaulting to dry run.

## Validate

Run both test programs through `build.sh`.
The behaviour harness uses SDK substitutes, so API, persistence or event changes also need an isolated real Emby server with synthetic media and test accounts.
Check that the next episode's playback information selects the expected audio before starting, other user-data fields stay unchanged, user/season isolation holds, and rollback preserves later manual choices.
For client-facing changes, test a real manual track switch plus both a fresh next-episode start and an automatic queued transition.

State exactly which server and client versions were tested and which scenarios remain unverified.
Do not test deployment, playback or account changes against someone else's active server without their authorization.
