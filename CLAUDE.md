# Don't Trust Your Luck! — Claude Code Project Instructions

## 1. Project identity

- **Game:** Don't Trust Your Luck!
- **Genre:** competitive multiplayer party board game.
- **Engine:** Unity 6, project editor version `6000.6.8f1` (use the version recorded in `ProjectSettings/ProjectVersion.txt` if it differs).
- **Language:** C#.
- **Template / rendering:** Universal 3D / URP.
- **Primary release target:** **Unity Web (WebGL)**, playable in a desktop browser and intended for itch.io.
- **Target presentation:** 16:9, reference resolution 1280×720; responsive browser UI.
- **Development goal:** a polished, stable Unity Game Jam 2026 vertical slice before optional features.

## 2. Source of truth and decision status

Read `docs/GDD.md` before proposing or implementing gameplay features. Read other relevant documents in `docs/` when present.

The GDD distinguishes **defined**, **proposed**, and **open** decisions. **Do not silently treat proposals as approved requirements.** If a task depends on an unresolved rule, explain the trade-off and ask for a decision before implementing it. If a small reversible assumption is needed to make progress, state it explicitly and keep it configurable.

Priority when instructions conflict:
1. The user's latest explicit instruction.
2. This `CLAUDE.md` for engineering workflow and platform constraints.
3. Approved decisions in `docs/GDD.md` for gameplay and creative direction.
4. Other project documentation and existing code conventions.

Never overwrite an approved decision without calling out the change.

## 3. Core game — current design direction

Players control 2D illustrated animal tokens on a numbered 3D board. A **lucky number** is selected at match start and is the exact landing target. An **unlucky number** changes each round; ending a movement on that number can eliminate a player. Players roll two dice and select a result, and can use action cards to manipulate outcomes or opponents.

These details are **not all finalized**: exact board length, lucky-number range, overshoot/rebound behavior, card timing, turn-based versus simultaneous resolution, player count, elimination edge cases, and online networking. Treat them as design questions, not settled implementation contracts.

First vertical slice: one board, placeholder pieces, dice choice, movement, lucky/unlucky-number display, win/loss handling, and a simple restart flow. Add cards, bots, and visual polish incrementally after the basic loop is validated.

## 4. Visual and creative direction

- **Tropical Folk Diorama:** stylized 3D city dioramas with pixel-art textures, warm lighting, lush regional vegetation, and **2D animal cutout characters** resembling aged printed lottery cards.
- UI uses legible vintage paper/card motifs; gameplay information must stand out from detailed environments.
- Long-term board concept: each map represents a real Latin American city and draws broad visual inspiration from an artist of that country. Fortaleza / Aldemir Martins is a proposed first direction, **not an asset-completion requirement**.
- Use original assets. Do not reproduce protected artwork, imply artist endorsement, or mix cultural symbols indiscriminately.
- **Do not generate complex final art before the mechanics are playable.** Use placeholders for early milestones.

## 5. WebGL-first engineering rules

Every system must be designed to run in a Unity Web build, not just the Editor.

- Do not rely on native desktop plugins, arbitrary filesystem access, unsupported sockets, or OS-specific APIs.
- Do not require conventional background threads for core gameplay; check Web platform support before adding concurrency or packages.
- Keep allocations, texture sizes, draw calls, shader complexity, realtime lights, and memory usage modest.
- Prefer baked/static lighting where suitable, material sharing, atlases, pooled transient effects, and lightweight UI.
- Avoid blocking work on the main thread. Avoid expensive per-frame searches and unnecessary `Update()` methods.
- Keep input and UI usable in browser windows at different sizes. Do not require mouse lock or exclusive fullscreen.
- Keep gameplay rules deterministic and separated from presentation and networking so that future online multiplayer is possible.
- Never claim WebGL compatibility based only on an Editor test. Request or perform a real browser build/test at milestones when the environment supports it.
- Do not add third-party dependencies without explaining their Web compatibility, license, and necessity.

## 6. Architecture principles

- Separate **pure gameplay/domain logic** (rules, turn resolution, dice results, victory conditions) from **Unity presentation** (MonoBehaviours, animations, audio, UI).
- Prefer small components with clear responsibilities and explicit dependencies; avoid giant managers and global singletons for every subsystem.
- Use `ScriptableObject` assets for designer-authored data such as card definitions and board metadata, but keep mutable match state in runtime objects.
- Centralize random-number generation behind an interface or service where practical, enabling reproducible tests and future authoritative networking.
- Validate player actions and resolve effects in one consistent rules layer; visuals should react to resolved state, not determine outcomes.
- Use events/callbacks thoughtfully; unsubscribe correctly and avoid hidden coupling.
- Prefer simple, understandable solutions over speculative abstraction or premature optimization.
- Preserve `.meta` files and Unity GUID references. Never manually regenerate or delete them without reason.

### Suggested code organization (adapt to the actual Unity project)

```text
Assets/
  _Game/
    Scripts/
      Core/
      Gameplay/
      Board/
      Cards/
      Players/
      UI/
      Audio/
      Editor/
      Tests/
    Scenes/
    Prefabs/
    ScriptableObjects/
    Art/
    Audio/
```

Do not create empty directories or placeholder files just to mirror this tree. Keep Unity assets inside `Assets/` and documentation in `docs/`.

## 7. Work protocol for every task

1. **Inspect:** read the relevant docs and existing code/assets before editing. Confirm actual Unity/package versions from the repository.
2. **Plan:** briefly state intended behavior, files likely to change, dependencies, and acceptance criteria. For substantial changes, wait for approval if design is unresolved.
3. **Implement:** make the smallest cohesive change. Preserve existing working features. Avoid unrelated refactors.
4. **Verify:** run available static checks/tests; if Unity or a browser build cannot run in the environment, say so explicitly and provide exact manual verification steps.
5. **Report:** summarize files changed, behavior implemented, checks actually performed, known limitations, and the next suggested step.

When modifying scenes, prefabs, materials, or serialized assets, be careful with Unity YAML and GUIDs. Prefer Unity Editor workflows when safe automation is unavailable. Never claim a scene was visually inspected unless it was.

## 8. Tests and definition of done

- Add EditMode tests for pure rules whenever feasible (dice selection, exact landing, overshoot, unlucky-tile resolution, turn changes, card effects).
- Add PlayMode tests for Unity integration where useful.
- Test edge cases: lucky/unlucky-number collisions, already-occupied unlucky tiles, movement beyond board limits, elimination of the final opponents, repeated card use, and restart/reset state.
- A feature is **done** only when acceptance criteria are met, errors are addressed, and the relevant tests/checks have been executed or explicitly marked **not run**.
- A release milestone requires a **real Web build** and browser smoke test: load, input, full match, win/loss, restart, and console errors.
- Do not invent performance measurements or test results.

## 9. Git and repository safety

- Use small, descriptive commits (e.g. `feat: implement dice choice`, `fix: prevent invalid movement`, `docs: clarify unlucky-number rules`).
- Do **not** commit generated Unity directories: `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`.
- Commit `Assets/` **with `.meta` files**, `Packages/`, `ProjectSettings/`, `docs/`, `CLAUDE.md`, and required project config.
- Never commit secrets, API keys, `.env`, or machine-specific credentials. `.env.example` may be committed if it contains placeholders only.
- Do not create branches, commit, push, delete files, install packages, or change project settings unless the task authorizes those actions.
- Never discard user changes to fix a problem; inspect diffs first.

## 10. Communication style

- Communicate with the user in **Brazilian Portuguese**. Code identifiers and commit messages may be in English.
- Be practical and concise; explain important design and technical trade-offs.
- Explicitly label **implemented**, **proposed**, **untested**, and **blocked** work.
- If a task is ambiguous, ask a focused question rather than building a large unapproved system.
- Prefer a playable, tested game over feature count. **Scope control is essential for the game jam.**
