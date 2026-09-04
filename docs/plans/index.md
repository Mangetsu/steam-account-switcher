# Feature Plans

Working plans for features not yet shipped. Once a feature ships, fold the relevant behavior
into `docs/architecture.md` / `README.md` / `CHANGELOG.md` and mark the plan file `✅ done` here
(keep the file for history — do not delete).

Plans are draft/working documents, not source of truth. If a plan disagrees with shipped code,
the code and the always-current docs (`architecture.md`, `README.md`) win.

## How to resume a plan

1. Read the plan file for the feature.
2. Check the phase table for the first unchecked step.
3. Read that phase's detail section before writing code.
4. Update the phase table (`⬜ pending` → `🔄 in progress` → `✅ done`) as you go.

## Active Plans

| Feature | Plan | Target Version | Status | Notes |
|---|---|---|---|---|
| Xbox library integration | [xbox-library-integration.md](xbox-library-integration.md) | v2.1.0 | ⬜ pending | New `XboxStore` (read-only, no account switch), platform filter (Steam/Xbox), tiered language-override engine. Needs 2 research spikes before Phase 3 starts. |

## Completed Plans

_None yet._
