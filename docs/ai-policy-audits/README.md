# AI and Dalamud Policy Audits

Achievement Tracker uses this folder to keep project-specific audit guidance and reports for official Dalamud policy compliance.

Run before commits and before any official submission:

```bash
python3 scripts/audit-ai-policy.py --diff HEAD
python3 scripts/adversarial-code-review.py --diff HEAD
```

This local script pair is a tripwire, not a replacement for human review. If either warns or fails, read the official cached docs under `docs/docs-cache/dalamud/` and fix or document the risk before continuing.

For an independent fresh-context review, use `adversarial-code-review-agent.md` as the reviewer prompt and provide the diff plus both script outputs.
