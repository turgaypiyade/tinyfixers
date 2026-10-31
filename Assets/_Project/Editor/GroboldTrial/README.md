# Grobold local visual trial

Source: https://www.dafont.com/grobold.font (Guy Buhry, GROBOLD.ttf).
Public download is marked **Free for personal use**. This is a local visual
evaluation, not the purchased commercial font or a production font change.

All files are under `Editor`; no font or generated material is referenced by a
production prefab. The trial creates transient TMP resources in Play Mode and
restores/releases them on disable, exit or script reload.

The Grobold arc trial is **enabled by default** for the current comparison.
The production prefab retains Paytone One
for the title and Continue/Retry button, with the existing warm-white fill,
dark outline and hard shadow. Grobold is applied automatically in Play Mode by the editor-only trial.
It can also be selected via:
`TinyFixers > Fonts > PreLevel Font Trial > Grobold (evaluation)`.
Toggle it off using `Enabled (Play Mode Only)` to restore the prefab font.

The downloaded font's cmap was checked: `ç Ç Ü` exist, while `ş Ş ğ Ğ ı İ ö Ö ü`
are absent. Missing glyphs use the previous Baloo 2 font; their appearance is
not evidence of Grobold Turkish coverage. ASCII labels such as `Seviye` and
`Oyna` can be evaluated entirely in Grobold.

The designer explicitly lists Turkish (Windows 1254) in the commercial
**Groboldov** version:
https://www.dafont.com/font-comment.php?file=grobold (author, 2012-09-11).
Commercial editions/contact: https://sites.google.com/view/guybuhry/grobold

## Free alternatives (2026-10-27)

Nerko One was downloaded unmodified from the official Google Fonts repository:
https://github.com/google/fonts/tree/main/ofl/nerkoone
Source and full OFL licence: `Assets/_Project/Fonts/NerkoOne/`.
Nerko One's cmap was checked for every character in `çÇşŞğĞıİöÖüÜ`;
all are present, with no Turkish fallback required. It uses SIL OFL 1.1.
Lilita One was also checked but rejected because its downloaded file lacks
`ş Ş ğ Ğ İ`.

User feedback (2026-10-27):
- DynaPuff is reserved for overlays and hints; do not use it in popups.
- Nerko One is definitively rejected: the user says it is not even close.
- Grobold's appearance is liked, but a free alternative is preferred.
- Latest direction: return to Baloo and show it again. Font trials are now
  disabled by default, with Nerko and DynaPuff removed from the trial menu.

Latest direction (2026-10-27): try Paytone One from Google Fonts on PreLevelPopup.
The title and Continue/Retry button now reference the dedicated Paytone One TMP
font and material presets; the event Join button inherits the Continue style.
The font source and complete OFL licence are under `Assets/_Project/Fonts/PaytoneOne/`.
All `çÇşŞğĞıİöÖüÜ` glyphs were verified in the downloaded cmap.
The user runs visual tests; do not operate the computer UI for font testing.

Paytone follow-up (2026-10-27): the user sees a difference between the Google
Fonts Regular 400 preview and the popup. Both labels explicitly use Normal/400.
PreLevel's scene-only title arc is disabled, the outline is reduced to 0.10,
and shadow dilation/offset are reduced to retain the font's own letter shapes.
Title: 100 pt; Continue: 90 pt. Both fit width only. Scene size overrides were
removed so MainMenu and in-game retry use the same prefab typography.
No computer UI or visual test was run; the user tests the appearance.

Current comparison (2026-10-27): the user requests Grobold with its previous arc.
The editor-only Grobold trial is enabled by default under a fresh session key.
Both MainMenu and in-game retry title arcs are enabled again (height 10, letter
rotation on). Title size stays 100; Continue returns to the previous 74 pt.
The trial reuses its original warm-white, dark outline and hard-shadow treatment.
No UI automation, build or visual test was performed.
