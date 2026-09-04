# Xbox Library Integration

**Status:** ⬜ pending — planning complete, 2 research spikes open before Phase 3.
**Target version:** v2.1.0 (branch `v2.1.0`)

## Context

EnigmaLauncher currently only knows about Steam via the `IGameStore`/`IAccountStore` abstraction
(see `docs/architecture.md` § Store abstraction). The store layer was built to allow more stores
without touching the UI or launch flow — this is the first one to land.

Scope: scan games installed through the Xbox app (Xbox/Game Pass PC titles), show them alongside
Steam games with a platform filter, and reuse existing per-game features (shortcuts, display
routing) where the OS allows it. Two things Steam has that Xbox structurally cannot:

- **Account switching.** Steam can silently patch registry/VDF to switch the logged-in account.
  There is no equivalent for the Xbox app / Microsoft account — decided **out of scope**.
  `XboxStore` implements `IGameStore` only, not `IAccountStore`. Owner is shown read-only
  (signed-in MS account name), no switch operation.
- **Per-game language selection.** Steam exposes a language dropdown per game. The Xbox app has
  no such UI. This is the main MVP-defining problem for this feature — see § Language override.

## Requirements

- Scan installed Xbox app / Game Pass titles, expose as `GameInfo` with `StoreId = "xbox"`.
- New platform filter row in the UI: `All / Steam / Xbox`, ANDed with the existing per-account
  filter (account filter only applies to Steam).
- Xbox cards get: Play, desktop shortcut, "create shortcut to folder", per-game display routing.
  Not carried over: "Open in Steam Library" (no confirmed Xbox equivalent deep link).
- Xbox cards get a language-override control, tiered by what's actually available per title
  (see below) — no single mechanism covers all titles, so this must degrade gracefully.
- No hardcoded paths; no admin elevation requirement for the common path.

## Design decisions (confirmed with user)

1. **No Xbox account switching.** `XboxStore : IGameStore` only.
2. **Language override must try multiple strategies per title, not one.** Rejected: shipping
   only a "deep link to Windows Settings" MVP — user wants the tiered engine described below
   researched thoroughly before implementation, since it's the feature's main value prop.

## Research findings so far

No single documented, scriptable API changes an individual Xbox/UWP game's language. Confirmed
via web research (2026-07):

- `ApplicationLanguages.PrimaryLanguageOverride` is a WinRT API the **app itself** calls at
  startup from inside its own process — not settable from an external process for another
  package.
- Windows Settings → Apps → *(installed app)* → Advanced options → **App language** dropdown is
  real, and only appears for packages that declare multiple languages in `Package.Languages`.
  Its backing store isn't publicly documented — needs a spike (see below).
- The Xbox app on PC has an undocumented per-title "custom launch arguments" feature (proven to
  exist by community workaround guides), which is how power users already force engine flags
  (e.g. `-dx12`) into Game Pass titles. Exact storage location undocumented — needs a spike.
  Whether it takes an arbitrary `-culture=xx` / `-language=xx` flag depends entirely on the
  game's engine.
- Confirmed, low-risk, always-available fallback: Windows' **preferred UI language list order**
  (Settings → Time & Language → Language) determines which language Xbox-launched titles use.
  Reordering it is global (affects every Xbox title at once, not per-game) but requires no
  reverse engineering — can be done today via `ms-settings:regionlanguage` deep link.

Sources: see chat history for this plan — Microsoft Learn (`ApplicationLanguages` docs), Xbox
community threads (ResetEra, ElevenForum, TroubleChute launch-options guide), AddictiveTips
per-app language article.

## Language override engine — tiered strategy

Evaluated in order per title, first applicable tier wins; capability shown as a badge on the
Xbox game card.

| Tier | Mechanism | Scope | Confidence |
|---|---|---|---|
| 1 | Inject engine-specific launch argument (`-culture=`, `-language=`, etc.) via whatever storage the Xbox app's custom-launch-arguments feature uses | Per-game | Needs spike to find storage; syntax varies per engine — seed a small JSON knowledge base, not universal |
| 2 | OS per-package "App language" override (same one Settings' Advanced options dropdown drives) | Per-game, only for packages with multiple declared languages | Needs spike to find backing store |
| 3 | Reorder Windows' global preferred UI language list, or deep-link `ms-settings:regionlanguage` for the user to do it | All Xbox titles at once | Confirmed works, no reverse engineering needed, but blunt |
| 4 | No override available — inform user the title has its own in-game language menu (if any) | N/A | Always-available floor |

`data\xbox-language-map.json` — new file, ships with a handful of known titles seeded manually
(`{aumid: {tier, argTemplate}}`), extensible later without a code change.

`LanguageOverrideSettings` — new record in `data\settings.json`, keyed `"storeId:gameId"` same
pattern as `GameDisplaySettings` (`docs/architecture.md` § Per-game display settings): stores
chosen `Tier` and `LanguageTag`, applied by `XboxStore.BuildLaunchOperation` before activation.

## Implementation phases

### Phase 0 — Research spikes (blocking Phase 3)

1. Process Monitor / API Monitor trace of Settings → Apps → Advanced options → App language
   dropdown, to find the write path Tier 2 needs to replicate.
2. Reverse the Xbox app's per-title custom-launch-arguments storage (Tier 1) — confirm it's
   read at launch time and whether it's writable from outside the Xbox app process.
3. Confirm `HKLM\SOFTWARE\Microsoft\GamingServices\PackageRepository\Package\*` schema is stable
   enough across Xbox app versions to rely on for scanning (unofficial key).

### Phase 1 — `XboxStore` scaffolding (Tier 3/4 language only)

- `Stores/Xbox/XboxStore.cs` implementing `IGameStore`.
- `Stores/Xbox/XboxLibraryScanner.cs` — enumerate installed titles via `PackageManager`
  (Windows.Management.Deployment), cross-check against `GamingServices\PackageRepository` to
  filter to actual games and resolve install root.
- Launch via `IApplicationActivationManager::ActivateApplication(aumid)` (COM interop, no new
  package dependency — same interface Start Menu tiles use).
- Artwork from `Package.Current`/app list entry logo (`Square150x150Logo` etc.), cached under
  `data\cache\xbox\<id>\` matching the existing `data\cache\` convention.
- Register in `StoreRegistry` (the `// Future stores` comment already anticipates this slot).
- Ship with language Tier 3 (global reorder deep link) and Tier 4 only — no per-game override
  yet, gated behind Phase 0 spikes.

### Phase 2 — Platform filter UI

- Extend `MainWindow.xaml` filter bar with a platform pill row (`All / Steam / Xbox`).
- `ApplyFilter()` in `MainWindow.xaml.cs` (`docs/architecture.md` mentions this as part of
  `MainWindow`'s "game grid, filter bar" responsibility) becomes AND of platform + account.
- Account filter pills (Steam accounts) hide entirely whenever the Steam pill is off — i.e. no
  Steam games are shown (`Xbox`-only selection). Visible again the moment `All` or `Steam` is
  selected. Xbox has no switchable accounts, so there is never an account row for it.

### Phase 3 — Language override engine, Tier 1 + Tier 2 (blocked on Phase 0)

- `XboxLanguageOverride.cs` — applies whichever tier is available for the title, using
  `xbox-language-map.json` for Tier 1.
- Per-game language badge + picker popup on Xbox cards, same interaction pattern as the
  existing per-game display-settings popup.
- Persist to `LanguageOverrideSettings` in `SettingsStore`.

### Phase 4 — Shortcuts + docs

- Confirm `ShortcutCreator` handles AUMID-style `GameId` (string, no spaces) as `--launch`
  argument — already string-based (`App.xaml.cs`), expected to need no change.
- Update `docs/architecture.md` (Xbox store section, language override section),
  `README.md`, `CHANGELOG.md` per `CLAUDE.md`'s documentation rule.

## Critical files

| Area | File | Change |
|---|---|---|
| Store | `EnigmaLauncher/Stores/Xbox/XboxStore.cs` | NEW — `IGameStore` implementation |
| Store | `EnigmaLauncher/Stores/Xbox/XboxLibraryScanner.cs` | NEW — package enumeration + registry cross-check |
| Store | `EnigmaLauncher/Stores/Xbox/XboxLanguageOverride.cs` | NEW — tiered language engine |
| Store | `EnigmaLauncher/Stores/StoreRegistry.cs` | Register `XboxStore.TryCreate()` |
| Settings | `EnigmaLauncher/Settings/LanguageOverrideSettings.cs` | NEW — per-game language choice record |
| Settings | `EnigmaLauncher/Settings/SettingsStore.cs` | Read/write `LanguageOverrideSettings` |
| UI | `EnigmaLauncher/UI/MainWindow.xaml` | Platform filter row |
| UI | `EnigmaLauncher/UI/MainWindow.xaml.cs` | `ApplyFilter()` two-axis filtering |
| UI | Xbox card language badge/popup (new or extend `GameCard`) | Language tier UI |
| Data | `data\xbox-language-map.json` | NEW — seeded per-title Tier 1 knowledge base |
| Docs | `docs/architecture.md`, `README.md`, `CHANGELOG.md` | Updated on ship, not before |

## Verification

- Manual: install a free Xbox/Game Pass title, confirm it appears as a card with `StoreId = "xbox"`,
  correct artwork, launches via `ActivateApplication`.
- Manual: platform filter correctly isolates Steam-only / Xbox-only / All.
- Manual: desktop shortcut created for an Xbox game launches it via `EnigmaLauncher.exe --launch <aumid>`
  without Steam ever being touched.
- Manual: language badge shows correct tier per test title (one with known Tier 1 support, one
  with only Tier 4).
- No admin/elevation prompt during normal scan/launch/shortcut flow.

## Open questions for next planning pass

- Xbox library deep-link equivalent of "Open in Steam Library" — worth a follow-up spike or drop
  permanently?
- Multi-user Windows machines: does `PackageManager` enumeration need `PackageManager(user)`
  overload, or is current-user scope always correct for this app's use case?
