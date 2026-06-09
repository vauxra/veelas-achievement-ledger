#!/usr/bin/env python3
"""Refresh local AI-friendly snapshots of selected Dalamud docs.

This script fetches official documentation pages listed in docs/docs-cache/sources.json,
converts them to readable markdown-ish text, writes docs/docs-cache/dalamud/*.md, and
records fetch metadata in docs/docs-cache/latest-check.json.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import html
import json
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CACHE_ROOT = ROOT / "docs" / "docs-cache"
SOURCES_PATH = CACHE_ROOT / "sources.json"
OUTPUT_DIR = CACHE_ROOT / "dalamud"
LATEST_PATH = CACHE_ROOT / "latest-check.json"


def html_to_text(raw: str) -> str:
    raw = re.sub(r"<(script|style|svg|nav|footer|header)[\s\S]*?</\1>", " ", raw, flags=re.I)
    raw = re.sub(r"<br\s*/?>", "\n", raw, flags=re.I)
    raw = re.sub(r"</(p|li|h1|h2|h3|h4|tr|div|section|article)>", "\n", raw, flags=re.I)
    raw = re.sub(r"<[^>]+>", " ", raw)
    text = html.unescape(raw)
    text = text.replace("\r", "")
    text = re.sub(r"[ \t]+", " ", text)
    text = re.sub(r"\n[ \t]+", "\n", text)
    text = re.sub(r"\n{3,}", "\n\n", text)
    return text.strip()


def fetch(url: str, timeout: int = 30) -> tuple[int, str]:
    req = urllib.request.Request(url, headers={"User-Agent": "Hermes AchievementTracker docs cache/1.0"})
    with urllib.request.urlopen(req, timeout=timeout) as response:
        status = getattr(response, "status", 200)
        body = response.read().decode("utf-8", "ignore")
    return status, body


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check-latest", action="store_true", help="Fetch docs, write cache, and report changed hashes")
    parser.add_argument("--allow-offline", action="store_true", help="Do not fail if fetching fails and an existing cache is present")
    args = parser.parse_args()

    sources_doc = json.loads(SOURCES_PATH.read_text())
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    previous = {}
    if LATEST_PATH.exists():
        try:
            previous_doc = json.loads(LATEST_PATH.read_text())
            previous = {entry["slug"]: entry for entry in previous_doc.get("sources", [])}
        except json.JSONDecodeError:
            previous = {}

    now = dt.datetime.now(dt.timezone.utc).isoformat()
    latest_entries = []
    failures = []

    for source in sources_doc["sources"]:
        slug = source["slug"]
        url = source["url"]
        output_path = OUTPUT_DIR / f"{slug}.md"
        try:
            status, raw = fetch(url)
            text = html_to_text(raw)
            digest = hashlib.sha256(text.encode("utf-8")).hexdigest()
            old_digest = previous.get(slug, {}).get("sha256")
            changed = bool(old_digest and old_digest != digest)
            output_path.write_text(
                f"# {slug}\n\n"
                f"Source: {url}\n\n"
                f"Fetched: {now}\n\n"
                f"SHA256: `{digest}`\n\n"
                "---\n\n"
                f"{text}\n",
                encoding="utf-8",
            )
            latest_entries.append({
                "slug": slug,
                "url": url,
                "status": status,
                "sha256": digest,
                "changed": changed,
                "fetched_at": now,
                "output": str(output_path.relative_to(ROOT)),
            })
            print(f"OK {slug} changed={changed}")
        except Exception as exc:  # noqa: BLE001 - CLI should report every fetch failure.
            if args.allow_offline and output_path.exists():
                cached = output_path.read_text(encoding="utf-8", errors="ignore")
                digest = hashlib.sha256(cached.encode("utf-8")).hexdigest()
                latest_entries.append({
                    "slug": slug,
                    "url": url,
                    "status": "offline-cache-used",
                    "sha256": digest,
                    "changed": False,
                    "fetched_at": now,
                    "output": str(output_path.relative_to(ROOT)),
                    "error": str(exc),
                })
                print(f"OFFLINE {slug}: {exc}")
            else:
                failures.append({"slug": slug, "url": url, "error": str(exc)})
                print(f"ERR {slug}: {exc}", file=sys.stderr)

    LATEST_PATH.write_text(json.dumps({"checked_at": now, "sources": latest_entries, "failures": failures}, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {LATEST_PATH.relative_to(ROOT)}")

    if failures:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
