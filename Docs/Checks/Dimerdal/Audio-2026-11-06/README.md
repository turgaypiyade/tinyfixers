# Dimerdal opening and diamond flight audio

User supplied `kepenkac.wav` and `flyingdiamond.wav`, requesting a fuller, clearly audible sound. Original files are unchanged; exact copies are in `Sources/`. Pre-edit code/library snapshots are in `Before/`.

- `ShutterOpen.wav`: 0.25 s; retained the active opening sound and removed its near-silent tail. Mono RMS −19.10 dBFS, sample peak −3.01 dBFS.
- `DiamondFlight.wav`: 1.00 s; original timing retained. Mono RMS −20.23 dBFS, sample peak −3.00 dBFS.
- Offline low-mid EQ, gentle high-frequency reduction, compression, gain and soft peak control. 48 kHz mono 16-bit PCM, preloaded; runtime mixing volume 1.0. Exact filters, hashes and measured levels are in `processing.json`. Fullness and in-game mix still need the user's listening check.

All twelve colored/size shutter definitions use `coverRemovedSound` for each accepted opening step and `releaseSound` for a successful diamond launch. Inspector tooltips describe both uses. Wrong-color/rejected hits and failed launches remain silent. Flight retries do not replay the launch cue; producing a diamond does not play the opening cue.

Playback uses `board.Audio.PlayOneShotClip`, the existing shared board source, SoundEnabled gating, immediate setting-based muting and same-clip 60 ms deduplication. No local sources, loops, delayed playback, subscriptions, per-hit allocations or new board scans. BoardAudioDirector owns source teardown; the service schedules no pending sound jobs.

Read-only verification: C# syntax parses for the three code files changed in this session, `git diff --check`, 24 audio references across 12 shutters, PCM metadata, unchanged original WAV hashes, unchanged non-audio properties/all other obstacle definitions, and hashes for all 468 existing level/catalog files. Results are in `validation.json`.

LevelEditor palette, names, sprite references, 1x1/2x2/3x3 footprints, placement/erase/resize paths, per-box quota controls and Goals/PreLevel references were reviewed; their data and behavior are unchanged. No Unity UI, Play mode, build, gameplay simulation or audio playback was run. No CPU/GC/FPS measurements were made.
