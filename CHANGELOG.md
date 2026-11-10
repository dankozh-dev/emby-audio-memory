# Changelog

## 1.2.0 — User selection

- Replace manual user IDs with named account checkboxes loaded from Emby.
- Add an explicit all-users mode, including future accounts, while keeping preferences separate per user.
- Preserve existing selections on upgrade and when switching between modes.
- Prevent saving when the user list cannot be loaded; render names as text.

## 1.1.1 — Configuration page fix

- Use the Emby 4.10 view/controller format so the dashboard can open Audio Memory settings.
- Move JavaScript into an embedded AMD module; Emby comments out inline scripts in configuration HTML.
- Show loading and saving errors, and prevent saving an unloaded form.
- Add five configuration-controller regression tests. Audio matching and stored preferences are unchanged.

## 1.1.0 — Initial public prerelease

- Remember explicit audio-track changes separately for each user and season.
- Match titled tracks by normalized title and language, including when stream indexes change.
- Reuse an audio stream index when the saved track or corresponding target track has no title; retain distinct non-empty titles and skip ambiguous matches.
- Apply choices before playback, on library changes and through a scheduled reconciliation task.
- Require an explicit user allowlist and skip actively playing episodes and multiple-source items.
- Provide an Emby configuration page and administrator-only status, seed, reconciliation and rollback APIs with dry-run support.
- Preserve original indexes in an atomic state file, track interrupted writes and restore only selections still owned by the plugin.
- Preserve other user-data fields, with fresh-read and concurrent-selection guards.
- Include portable build instructions, unit tests and a behaviour harness; verify server integration on Emby 4.10.2.0.

Real Android track changes and automatic playback of an already queued next episode remain unverified.
This prerelease does not claim Jellyfin support or compatibility with other Emby server versions.
