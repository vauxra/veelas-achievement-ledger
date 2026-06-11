# Veela's Achievement Ledger Agent Guide

This repo is a Final Fantasy XIV Dalamud plugin. Treat the official Dalamud documentation and API reference as authoritative for coding, review, and release decisions:

- General docs and standards: <https://dalamud.dev/>
- API reference, currently showing API 15: <https://dalamud.dev/api/>
- Game interaction guidance: <https://dalamud.dev/plugin-development/interaction/>
- Project layout / manifest / plugin entrypoint guidance: <https://dalamud.dev/plugin-development/project-layout/>
- Publishing overview: <https://dalamud.dev/plugin-publishing/>
- Plugin restrictions: <https://dalamud.dev/plugin-publishing/restrictions>
- Custom repository/testing keys: <https://dalamud.dev/plugin-publishing/custom-repositories>
- Submission process: <https://dalamud.dev/plugin-publishing/submission>
- AI usage policy: <https://dalamud.dev/plugin-publishing/ai-policy>
- Versions/channels/API level model: <https://dalamud.dev/versions/>

## Before code changes

- Open the relevant Dalamud docs/API pages before touching any Dalamud API, plugin metadata, manifest, packaging, release, ClientStructs, hook, native-agent, or game-interaction code. Do not rely on memory if the docs can answer it.
- Use <https://dalamud.dev/api/> for exact service/interface/member names and current API-level behavior. If the API page is incomplete, inspect local Dalamud XML/DLL metadata and document the reason.
- Follow Dalamud's interaction priority from the official game-interaction docs:
  1. Prefer Dalamud-provided APIs first; they are the safest and are stable outside API bumps.
  2. Use ClientStructs only when Dalamud APIs do not expose the needed behavior; keep pointer/unsafe handling small and reviewed.
  3. **Blocker:** do not use raw memory, signatures, or low-level hooks. If a requested change appears to require any of these, stop work and inform Micheal/the user that the task is blocked by this repo policy.
- Prefer documented Dalamud services first, then Lumina/local data, then ClientStructs only when needed.
- Keep `unsafe`/ClientStructs code isolated in small adapter classes with comments naming the component, risk level, and safety boundary.
- Do not store raw pointers across frames.
- Subscribe/unsubscribe events symmetrically and dispose hooks/resources.
- Any class implementing or owning `IDisposable`, hooks, event subscriptions, windows, or native resources must have a fully functional dispose/unregister path.

## Current product shape

Veela's Achievement Ledger is intentionally small:

- `/val` opens the ledger.
- Users track up to 20 achievements.
- the row reload icon and **Update Next** open the native Achievement entry.
- Numeric progress is cached only when the native Achievement UI returns progress data.
- Tracked achievement IDs and presets persist between logouts; observed progress cache resets on login/logout.
- Cosmic Class score progress reads local WKS/Cosmic score state when available and falls back to the saved local cache.

Do not reintroduce:

- plugin-originated achievement progress requests,
- refresh queues/throttlers,
- automatic polling or background request loops,
- advanced diagnostics UI,
- packet capture/network experiments,
- backend sync, telemetry, analytics, or leaderboards.

## Dalamud coding standards to enforce

These are explicit repo rules derived from the current Dalamud docs pages listed above:

- **API grounding:** Every new or changed Dalamud service/API use must be checked against <https://dalamud.dev/api/> or local shipped XML/DLL metadata. Do not guess service names, property names, or ClientStructs fields.
- **Project layout:** Keep the primary plugin DLL/assembly name aligned with the plugin internal name. The `AssemblyName` becomes the `InternalName`; treat `InternalName` as effectively permanent once released because it controls config paths, logs, DLL naming, and submissions.
- **Manifest/package:** Keep the manifest template named for the internal name and let DalamudPackager generate critical fields such as `InternalName`, `AssemblyVersion`, and `DalamudApiLevel`. Verify the packed zip manifest before release.
- **Entrypoint:** The plugin must have exactly one `IDalamudPlugin` entrypoint. It must clean up after itself through `Dispose()`.
- **Game interaction:** Plugins should enhance the experience without radically altering gameplay. Game-server interaction must not be automatic, hidden, or broader than a human player's action. Anything that could be gameplay automation, competitive/PvP advantage, hidden server traffic, packet capture, or data collection requires explicit design review and likely rejection for mainline.
- **Custom repository/testing:** Custom repo URLs must be public HTTP GET JSON with no authentication. If a build is testing-only, keep `IsTestingExclusive`, `TestingAssemblyVersion`, `TestingDalamudApiLevel`, `TestingChangelog`, and `DownloadLinkTesting` consistent; leave normal install/update links empty when using the testing-only warning model.
- **API level/versioning:** API level is separate from channel/track. Current docs show API 15; keep `DalamudApiLevel`, `Dalamud.NET.Sdk`, packager version, and release notes aligned when bumping versions.
- **AI policy:** If AI was used beyond autocomplete for official submission work, disclose the level of AI involvement. The maintainer must understand, test, and be able to explain all AI-assisted code. Entirely AI-generated or undisclosed AI-written submissions are not acceptable.

## Pre-commit / pre-review checklist

Before committing or releasing, an agent must verify and be able to answer:

- Which Dalamud docs/API pages were consulted for changed APIs or standards?
- Did any change add or alter `unsafe`, ClientStructs, native agents, `IFramework.Update`, addon lifecycle, network calls, IPC, or plugin manifest/package behavior?
- If yes, is the code isolated, documented, disposed/unregistered, and covered by the repo's policy tripwires?
- Does the change avoid raw memory, signatures, and low-level hooks entirely? If not, block the commit/review and inform Micheal/the user.
- Does the change follow the Dalamud game-interaction priority: Dalamud API first, ClientStructs second, and no raw memory/signature/low-level-hook implementation path?
- Does the change preserve the safe public/mainline stance: native Achievement-window assisted flow, passive observation, no plugin-originated progress queues/polling/automation?
- Are `AssemblyVersion`, `TestingAssemblyVersion`, `DalamudApiLevel`, release asset name, tag, and `pluginmaster.json` consistent?
- Does the packed release zip contain the expected manifest fields, icon/image URLs, and no forbidden automation/backend strings?
- If preparing official submission materials, does the README/PR disclose AI usage as required by the Dalamud AI policy and link the due-diligence notes?

## Security review requirements

Before commit, check:

- no secrets or credentials,
- no plugin input/config passed to shell/process execution,
- no path traversal risks,
- no dynamic assembly loading or dynamic code execution,
- no unsafe deserialization,
- no network calls without explicit design/privacy review,
- no background tasks/timers that survive disposal,
- no account ID, content ID, telemetry, analytics, leaderboard, or backend collection without explicit privacy review.

## Verification pipeline

Run before committing:

```bash
./scripts/verify-local.sh HEAD
```

This runs:

- unit tests,
- Debug build,
- Release build,
- CodeQL C# security/quality scan,
- AI/Dalamud policy tripwire,
- adversarial code-review tripwire,
- `git diff --check`.

For merge/submission, also run a fresh-context reviewer using:

- `docs/ai-policy-audits/adversarial-code-review-agent.md`

## Commit hygiene

- Keep feature/policy changes small.
- Do not commit `bin/`, `obj/`, `released/`, `.hermes/`, or generated analysis output.
- If a review finds issues, fix only those issues and re-run verification.
