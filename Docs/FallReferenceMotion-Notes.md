# Fall Reference Motion Notes

Current status: the reference-frame fall/spawn experiment is kept in the codebase, but it is disabled at runtime.

- `BoardController.UseReferenceFallMotion` currently returns `false`, so the old stable fall path is active.
- The reference motion attempt must not be mixed with the existing segmented/diagonal collapse runner as a partial hybrid.
- The risky cases to validate before re-enabling are diagonal slides, segmented board paths, special creation/trigger cascades, boss-duel levels, and blocked/overlay cells.
- `LevelP_0060` is a useful regression level because it has diagonal slide pressure. Add temporary specials in memory and verify that pieces never overlap, input never opens early, and `RunAfterIdle` does not stay busy.
- If revisiting, start from the current collapse path semantics first, then rebuild only the visual timing layer around that complete path.

Reason for leaving it off: the partial reference implementation caused stuck/busy states and overlap around diagonal movement. The stable production behavior is the previous fall/collapse flow.

## 2026-07-22 — Royal Kingdom kare analizi ve AKTİF profil

RoyalKingdom/Archive.zip (54 kare, 1320x2868, 60fps kabulü) ölçümü — ışın sonrası kolon-3 refill treni
piksel takibiyle (hücre ≈ 126.7px):

- Kalkış hızı v0 ≈ 30 px/f = **0.237 hücre/kare** (taş sıfırdan değil, hızlı başlıyor)
- İvme a ≈ +1.3 px/f² = **0.0103 hücre/kare²**
- Hız tavanı vmax = 48 px/f = **0.379 hücre/kare** (~16 karede ulaşıyor, sonra sabit)
- İniş: tek karede duruş + ~3px (**0.024 hücre**) mikro-yerleşme, 2-3 kare; zıplama YOK
- Tren: kolon aynı v(t) ile blok halinde düşer, taş araları açılmaz, dipten sırayla istiflenir

Uygulama (eski deneyden FARKLI, güvenli yol): `BoardController.ActiveFallProfileSettings` —
collapse/CascadeLogic'e dokunmadan yalnız görsel katman:
- `GetFallDurationForDistanceCells` süresi artık bu profilden (enabled=false → eski sabit hız).
- `GetFallProgressCurve(d)` mesafeye özel normalize progress eğrisi pişirir (çeyrek-hücre cache),
  FallAction düz-dikey dalında `MoveToGridCell`'in mevcut curve girişine takılır.
- Diagonal/L-path kayması eski davranışta (referans formu dikey düşüşe özgü).
- Settle: küçük tut — referans ölçümü 0.024 hücre / ~3 kare (inspector: FallSettle* alanları).
