# GitHub Wiki regeneration notes

Version: `v0.2.0.33`

These notes preserve the current architecture wiki structure and the exact publishing workflow so the wiki can be regenerated later without breaking links.

## Current structure

The repo keeps two documentation shapes:

- `map/` — source-oriented architecture docs committed in the main repository. These use normal repository-relative markdown links such as `./02-function-call-map.md`.
- `wiki-export/` — the GitHub Wiki payload. These files are copied directly into the separate `.wiki.git` repository and must use GitHub Wiki page-name links such as `Function-call-map`, not `./02-function-call-map.md`.

Do not collapse these two shapes together unless GitHub Wiki link behavior is re-tested. GitHub Wiki treats repo-style `./file.md` links as missing wiki pages and can send readers to “create new page” flows.

## Page mapping

When regenerating `wiki-export/` from `map/`, use this mapping:

- `map/README.md` → `wiki-export/Home.md`
- `map/00-whole-plugin-hierarchy.md` → `wiki-export/Whole-plugin-hierarchy.md`
- `map/01-big-picture.md` → `wiki-export/Big-picture.md`
- `map/02-function-call-map.md` → `wiki-export/Function-call-map.md`
- `map/03-cosmic-cache-flow.md` → `wiki-export/Cosmic-Class-cache-flow.md`
- `map/04-ui-window-map.md` → `wiki-export/UI-window-map.md`
- `map/05-data-model-map.md` → `wiki-export/Data-model-map.md`
- `map/06-safety-map.md` → `wiki-export/Safety-map.md`
- `map/07-file-index.md` → `wiki-export/File-index.md`
- `map/08-csharp-for-python-readers.md` → `wiki-export/CSharp-for-Python-readers.md`
- `map/09-dalamud-layer-model.md` → `wiki-export/Dalamud-layer-model.md`
- `map/_Sidebar.md` → `wiki-export/_Sidebar.md`

## Required wiki link rewrite

After copying, rewrite links inside `wiki-export/`:

- `./README.md` → `Home`
- `./00-whole-plugin-hierarchy.md` → `Whole-plugin-hierarchy`
- `./01-big-picture.md` → `Big-picture`
- `./02-function-call-map.md` → `Function-call-map`
- `./03-cosmic-cache-flow.md` → `Cosmic-Class-cache-flow`
- `./04-ui-window-map.md` → `UI-window-map`
- `./05-data-model-map.md` → `Data-model-map`
- `./06-safety-map.md` → `Safety-map`
- `./07-file-index.md` → `File-index`
- `./08-csharp-for-python-readers.md` → `CSharp-for-Python-readers`
- `./09-dalamud-layer-model.md` → `Dalamud-layer-model`

Anchor links may remain attached to the wiki page name, for example:

```markdown
[Big picture native open path](Big-picture#native-achievement-open-path)
```

## Regenerate `wiki-export/`

From the repository root:

```bash
python3 - <<'PY'
from pathlib import Path

root = Path('.')
mapping = {
    'README.md': 'Home.md',
    '00-whole-plugin-hierarchy.md': 'Whole-plugin-hierarchy.md',
    '01-big-picture.md': 'Big-picture.md',
    '02-function-call-map.md': 'Function-call-map.md',
    '03-cosmic-cache-flow.md': 'Cosmic-Class-cache-flow.md',
    '04-ui-window-map.md': 'UI-window-map.md',
    '05-data-model-map.md': 'Data-model-map.md',
    '06-safety-map.md': 'Safety-map.md',
    '07-file-index.md': 'File-index.md',
    '08-csharp-for-python-readers.md': 'CSharp-for-Python-readers.md',
    '09-dalamud-layer-model.md': 'Dalamud-layer-model.md',
    '_Sidebar.md': '_Sidebar.md',
}

link_rewrites = {
    './README.md': 'Home',
    './00-whole-plugin-hierarchy.md': 'Whole-plugin-hierarchy',
    './01-big-picture.md': 'Big-picture',
    './02-function-call-map.md': 'Function-call-map',
    './03-cosmic-cache-flow.md': 'Cosmic-Class-cache-flow',
    './04-ui-window-map.md': 'UI-window-map',
    './05-data-model-map.md': 'Data-model-map',
    './06-safety-map.md': 'Safety-map',
    './07-file-index.md': 'File-index',
    './08-csharp-for-python-readers.md': 'CSharp-for-Python-readers',
    './09-dalamud-layer-model.md': 'Dalamud-layer-model',
}

(root / 'wiki-export').mkdir(exist_ok=True)
for source_name, dest_name in mapping.items():
    text = (root / 'map' / source_name).read_text()
    for old, new in link_rewrites.items():
        text = text.replace(old, new)
    (root / 'wiki-export' / dest_name).write_text(text)
PY
```

## Local validation before publishing

```bash
# wiki-export should not contain repo-style markdown page links.
! grep -RInE '\[[^]]+\]\([^)]*\.md\)|\]\(\.\/|\]\(map/' wiki-export

# Basic markdown fence sanity.
python3 - <<'PY'
from pathlib import Path
fence = chr(96) * 3
bad = [str(p) for p in Path('wiki-export').glob('*.md') if p.read_text().count(fence) % 2]
if bad:
    raise SystemExit(f'Unbalanced markdown fences: {bad}')
print('wiki-export markdown fence check OK')
PY
```

## Publish to GitHub Wiki

The GitHub Wiki is a separate git repository:

```text
https://github.com/vauxra/veelas-achievement-ledger.wiki.git
```

Publish the current payload:

```bash
TMP=$(mktemp -d)
git clone https://github.com/vauxra/veelas-achievement-ledger.wiki.git "$TMP/wiki"
cp wiki-export/*.md "$TMP/wiki/"
cd "$TMP/wiki"
git add *.md
git commit -m "Update architecture wiki"
git push origin HEAD
```

If there are no changes, `git commit` will say there is nothing to commit; that is fine.

## Post-publish verification

```bash
# Check every expected page returns HTTP 200.
for page in \
  Home \
  Whole-plugin-hierarchy \
  Big-picture \
  Function-call-map \
  Cosmic-Class-cache-flow \
  UI-window-map \
  Data-model-map \
  Safety-map \
  File-index \
  CSharp-for-Python-readers \
  Dalamud-layer-model; do
  curl -fsSL -o /tmp/wiki-page.html -w "$page %{http_code}\n" \
    "https://github.com/vauxra/veelas-achievement-ledger/wiki/$page"
done

# Check Home does not contain create-new-page or repo-path links.
curl -fsSL https://github.com/vauxra/veelas-achievement-ledger/wiki/Home -o /tmp/val-wiki-home.html
! grep -E '/wiki/[^" ]*/_new|action=new|README\.md|00-whole-plugin-hierarchy\.md|01-big-picture\.md|02-function-call-map\.md' /tmp/val-wiki-home.html

# Confirm the expected rendered wiki links exist.
grep -oE 'href="[^"]+/wiki/(Whole-plugin-hierarchy|Big-picture|Function-call-map|Cosmic-Class-cache-flow|UI-window-map|Data-model-map|Safety-map|File-index|CSharp-for-Python-readers|Dalamud-layer-model)[^"]*"' /tmp/val-wiki-home.html

# Confirm wiki git HEAD.
git ls-remote https://github.com/vauxra/veelas-achievement-ledger.wiki.git HEAD
```

## Current preferred structure

Keep the current ordered reading list and `_Sidebar.md` navigation order unless there is an explicit design change request. The order is:

1. Home
2. Whole plugin hierarchy
3. Big picture
4. Function call map
5. Cosmic Class cache flow
6. UI/window map
7. Data model map
8. Safety map
9. File index
10. C# primer for Python readers
11. Dalamud layer model
