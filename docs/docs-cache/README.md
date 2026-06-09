# Dalamud Docs Cache

This directory contains local, AI-friendly snapshots of official Dalamud documentation used by Achievement Tracker development.

Rules:

- Treat cached docs as reference material, not as a permanent substitute for latest official docs.
- Run `python3 scripts/refresh-dalamud-docs.py --check-latest` before implementation work that touches Dalamud APIs.
- Each cached file includes the source URL, fetched timestamp, and content hash.
- If the refresh reports changed content, review the changed cached docs before coding.

No generated cached docs should contain secrets or local game files.
