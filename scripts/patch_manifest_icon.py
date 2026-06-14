#!/usr/bin/env python3
"""Ensure DalamudPackager output manifests include icon metadata.

DalamudPackager 15 can omit IconUrl from local build manifests even when the
MSBuild property is set. This keeps dev-plugin and release outputs consistent.
"""
from __future__ import annotations

import json
import sys
import zipfile
from pathlib import Path


def patch_manifest(path: Path, icon_url: str) -> bool:
    if not path.exists() or not icon_url:
        return False

    data = json.loads(path.read_text(encoding="utf-8"))
    changed = data.get("IconUrl") != icon_url
    data["IconUrl"] = icon_url
    # IconUrl may be a square 512x512 plugin icon. Dalamud's ImageUrls are
    # queued as screenshots and have a much wider/smaller max bounds check
    # (observed: 730x380), so reusing the square icon here logs:
    # "Plugin image1 ... was larger than the maximum allowed resolution".
    # Keep the icon as IconUrl only unless a real screenshot URL is supplied.
    if data.get("ImageUrls") == [icon_url]:
        data.pop("ImageUrls")
        changed = True

    if changed:
        path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return changed


def patch_zip(zip_path: Path, manifest_name: str, manifest_path: Path) -> None:
    if not zip_path.exists() or not manifest_path.exists():
        return

    temp_path = zip_path.with_suffix(zip_path.suffix + ".tmp")
    with zipfile.ZipFile(zip_path, "r") as source, zipfile.ZipFile(temp_path, "w", zipfile.ZIP_DEFLATED) as target:
        for item in source.infolist():
            if item.filename == manifest_name:
                target.writestr(item, manifest_path.read_bytes())
            else:
                target.writestr(item, source.read(item.filename))
    temp_path.replace(zip_path)


def main() -> int:
    if len(sys.argv) != 4:
        print("usage: patch_manifest_icon.py <manifest.json> <latest.zip> <icon-url>", file=sys.stderr)
        return 2

    manifest_path = Path(sys.argv[1])
    zip_path = Path(sys.argv[2])
    icon_url = sys.argv[3]
    patch_manifest(manifest_path, icon_url)
    patch_zip(zip_path, manifest_path.name, manifest_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
