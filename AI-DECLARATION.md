---
version: "0.1.2"
level: copilot
processes:
  design: pair
  implementation: copilot
  testing: assist
  documentation: copilot
  review: assist
  deployment: assist
components:
  AchievementTracker: copilot
  AchievementTracker.Tests: assist
  docs: copilot
  scripts: assist
---

This format is based on [AI-DECLARATION.md](https://ai-declaration.md/en/0.1.2/).

## Notes

- This project used AI assistance beyond autocomplete. The disclosure level is `copilot`: AI wrote or edited substantial portions of code and documentation while the human developer planned features, directed changes, reviewed output, tested behavior, and accepted or rejected work.
- AI-assisted work included C# implementation, Dalamud/ClientStructs/Lumina API research, tests, verification scripts, release metadata, and documentation drafts.
- The human developer performed in-game testing of the achievement update flow and provided screenshots/log observations that guided design changes.
- AI output was checked against Dalamud documentation, local Dalamud/ClientStructs API metadata, and existing plugin repository structure before publishing.
- Verification included unit tests, Debug and Release builds, release-package sanity checks, `git diff --check`, CodeQL C# security/quality scanning, local Dalamud policy/AI tripwires, and adversarial review tripwires.
- Fresh-context antagonistic/adversarial review agents were used to look for policy issues, unsafe ClientStructs usage, stale diagnostics, request-loop regressions, package-structure problems, and submission-readiness gaps.
- No AI-generated icons, images, audio, textures, or other user-facing assets are included.
