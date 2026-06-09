"""
Extract ALL simulation-related TinySea files into a FLAT folder for AI context.

Scope: anything the simulation depends on, references, or shares logic with --
plus all documentation (specs, diagrams, screenshots, xlsx). Game-only scripts
that the simulation never touches are intentionally excluded.

Coverage:
  - All Simulation/ scripts (core, bulk, UI, data structures, CSV, WebGL)
  - All ScriptableObject assets (SimulationConfig, SpeciesDatabase, RunSpeciesList)
  - Game-side scripts that the simulation references -- AUTO-DETECTED by
    scanning Simulation/*.cs for class-name occurrences (no hardcoded list).
  - WebGL plugins (jslib for downloads/ZIP -- bulk results)
  - Species data (species.csv)
  - Simulation UI prefabs (YAML) -- inspector-set wiring not visible in C#.
  - Unity package manifest + lock (dependency versions referenced by docs).
  - ProjectSettings.asset -- build target, scripting defines, layers.
  - All documentation in docs/ (markdown, diagrams, screenshots, xlsx, etc.)
  - Validation notes + test CSV fixtures from Tests/ (skipping run-output dirs)
  - Both CLAUDE.md files (parent project overview + Unity-side)
  - A generated _README_FOR_AI.md preface explaining project history + bundle scope

Intentionally excluded:
  - simulation.unity scene file -- it's pure YAML but ~200% of the Claude web
    app's context window, so it would crowd out the actual source. The prefabs
    and the C# scripts together are enough to reconstruct the wiring.

Usage:
    python extract_context.py                   # outputs to ./claude_context/
    python extract_context.py path/to/output    # outputs to specified folder
"""

import re
import sys
import shutil
from pathlib import Path

# -- Configuration ----------------------------------------------------------

UNITY_ROOT     = Path(__file__).resolve().parent      # ./tinysea/
PROJECT_ROOT   = UNITY_ROOT.parent                    # ./TinySea/  (has top-level CLAUDE.md)
OUTPUT_DEFAULT = UNITY_ROOT / "claude_context"

# Globally skipped: Unity meta files only. Everything else (PDFs, PNGs,
# xlsx in docs/) is kept because docs are part of the simulation context.
GLOBAL_SKIP_EXT = {".meta"}

# When walking source-code dirs we also drop binary noise (no docs in there).
SOURCE_SKIP_EXT = GLOBAL_SKIP_EXT | {".pdf", ".png", ".jpg", ".jpeg", ".xlsx"}

# Game scripts that the simulation does NOT reference by name but shares
# biology/logic with -- the simulation has its own ports (TemperatureCalculator,
# SimSpecies, etc.). Keep these as conceptual context for the AI.
ALWAYS_INCLUDE_GAME_SCRIPTS = {
    "ThermalCurve.cs",      # Arrhenius formula -- ported into the simulation
    "TemperatureTrend.cs",  # game seasonal model -- ported into TemperatureCalculator
    "CharacterManager.cs",  # game per-species state -- analog of SimSpecies
}


# -- Helpers ---------------------------------------------------------------

def flat_copy(src: Path, output_dir: Path, used: dict, prefix: str = "") -> bool:
    """Copy src into output_dir (flat). Returns True if copied."""
    name = f"{prefix}{src.name}" if prefix else src.name

    if name in used:
        parent = src.parent.name
        name = f"{parent}_{src.name}"

    if name in used:
        print(f"  SKIP collision: {name} (from {used[name]})")
        return False

    used[name] = str(src)
    shutil.copy2(src, output_dir / name)
    return True


def detect_referenced_game_scripts(sim_dir: Path, scripts_dir: Path) -> list[Path]:
    """
    Find game-side .cs files (top-level Assets/scripts/, NOT inside Simulation/)
    whose class name (== filename stem, by Unity convention) is mentioned
    anywhere in Simulation/*.cs source.
    """
    # Concatenate all simulation source as a single search corpus.
    sim_source = []
    for f in sim_dir.rglob("*.cs"):
        try:
            sim_source.append(f.read_text(encoding="utf-8", errors="ignore"))
        except OSError:
            pass
    corpus = "\n".join(sim_source)

    referenced = []
    seen_names = set()
    for cs in sorted(scripts_dir.glob("*.cs")):
        class_name = cs.stem
        # Skip non-PascalCase or short names to avoid matching common verbs
        # like "load" / "save" / "Pages" used in unrelated comments.
        if not class_name[:1].isupper() or len(class_name) < 5:
            continue
        # Word-boundary match so "Temperature" doesn't bleed into "TemperatureCalculator".
        if re.search(rf"\b{re.escape(class_name)}\b", corpus):
            referenced.append(cs)
            seen_names.add(cs.name)

    # Always include the conceptual-parallel scripts even if not referenced by name.
    for name in ALWAYS_INCLUDE_GAME_SCRIPTS:
        if name in seen_names:
            continue
        path = scripts_dir / name
        if path.exists():
            referenced.append(path)
            seen_names.add(name)
        else:
            print(f"  WARNING: ALWAYS_INCLUDE script not found: {name}")
    return sorted(referenced, key=lambda p: p.name)


def write_readme(output_dir: Path, game_scripts: list[Path]) -> None:
    """Generate a preface README explaining project history + bundle scope."""
    refs = "\n".join(f"  - {p.name}" for p in game_scripts) or "  (none auto-detected)"
    content = f"""# TinySea -- AI context bundle

## Project history (important)

TinySea was **originally an interactive Unity game** (Northeastern University
GhostLab) where players manage a marine ecosystem by buying/selling organisms
across a 3-tier food chain while temperature fluctuates seasonally. The
codebase still contains all of that game logic.

It has since been **extended into a headless ecosystem simulation** for
research use. The simulation lives entirely under `Assets/scripts/Simulation/`
and runs the same biology (Arrhenius thermal performance, Holling-II predation,
condition-based reproduction/death) without any of the game's UI, scenes,
or player interaction.

**Both layers coexist** in the repo. When answering questions about the
simulation, prefer files in this bundle. Game-only scripts (UI, scene
controllers, shop, sound) are intentionally excluded -- they are not part
of the simulation's behavior or research output.

## What's in this bundle

- All scripts under `Assets/scripts/Simulation/` (core sim, bulk runs, UI for
  the sim's results screen, CSV export, WebGL download glue, data structures).
- All ScriptableObject configs and species data (`SimulationConfig`,
  `SpeciesDatabase`, `RunSpeciesList`, `species.csv`).
- All WebGL `.jslib` plugins used by the simulation for browser file downloads.
- Simulation UI prefabs from `Assets/prefabs/Simulation/` (YAML). These hold
  the inspector-set wiring between the simulation UI scripts and their visual
  tree -- not visible from the C# alone.
- Unity package manifest (`manifest.json`, `packages-lock.json`) so the AI
  knows which Unity packages and versions are in play.
- `ProjectSettings.asset` -- build target, scripting define symbols, layers.
- All documentation under `docs/` -- the authoritative simulation spec,
  diagrams (mermaid), screenshots, and the simulation-variables spreadsheet.
- Validation methodology notes and test CSV fixtures from `Tests/`
  (`Tests_*.md`, `Tests_*.csv`). Run-output subfolders (`Brain/`,
  `tinysea_bulk_*`) are excluded -- those are generated artifacts.
- Both `CLAUDE.md` files (top-level project overview + Unity-side).
- Game-side scripts that the simulation directly references, auto-detected
  by scanning Simulation/*.cs for their class names. In this bundle:
{refs}

## What's NOT in this bundle

- `simulation.unity` scene file -- the YAML payload is too large for the
  Claude web app's context window. The prefabs + the C# scripts together
  reconstruct the wiring; the scene only adds positional / parenting data.
- Unity materials, animations, audio, sprites (binary).
- Game-only scripts the simulation never references (UI, scenes, shop, etc.).
- The website (`Tinysea PHP webiste/`) -- separate concern.
- Build outputs (`Builds/`, `Library/`, `Temp/`).

## File-naming convention

All files are flat (no subfolders) with prefixes to avoid collisions:
  - `Game_*`            -- game scripts referenced by the simulation
  - `DS_*`              -- data structures (from `Simulation/DataStructure/`)
  - `UI_*`              -- simulation UI scripts (results screen, config editor)
  - `Prefab_*`          -- simulation UI prefabs (YAML)
  - `Package_*`         -- Unity package manifest + lock
  - `ProjectSettings_*` -- Unity project settings
  - `diagram_*`         -- mermaid diagrams from `docs/diagrams/`
  - `Tests_*`           -- validation methodology notes + CSV fixtures
  - `ProjectRoot_CLAUDE.md` / `Unity_CLAUDE.md` -- the two CLAUDE.md files
"""
    (output_dir / "_README_FOR_AI.md").write_text(content, encoding="utf-8")


# -- Main extraction --------------------------------------------------------

def extract(output_dir: Path) -> None:
    if output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    used = {}
    count = 0
    PREFIXES = {"DataStructure": "DS_", "UI": "UI_"}

    sim_dir         = UNITY_ROOT / "Assets" / "scripts" / "Simulation"
    scripts_dir     = UNITY_ROOT / "Assets" / "scripts"
    resources_dir   = UNITY_ROOT / "Assets" / "Resources"
    plugins_dir     = UNITY_ROOT / "Assets" / "plugins" / "WebGL"
    sim_prefabs_dir = UNITY_ROOT / "Assets" / "prefabs" / "Simulation"
    packages_dir    = UNITY_ROOT / "Packages"
    settings_dir    = UNITY_ROOT / "ProjectSettings"
    docs_dir        = UNITY_ROOT / "docs"

    # 1. Simulation/ scripts (everything, recursive)
    print("[ 1/11] Simulation scripts ...")
    for f in sorted(sim_dir.rglob("*")):
        if f.is_file() and f.suffix not in SOURCE_SKIP_EXT:
            prefix = PREFIXES.get(f.parent.name, "")
            if flat_copy(f, output_dir, used, prefix):
                count += 1

    # 2. Game-side scripts referenced by simulation (auto-detected)
    print("[ 2/11] Game scripts referenced by simulation (auto-detect) ...")
    referenced = detect_referenced_game_scripts(sim_dir, scripts_dir)
    for src in referenced:
        if flat_copy(src, output_dir, used, "Game_"):
            count += 1
    print(f"        detected {len(referenced)} referenced game script(s)")

    # 3. ScriptableObject assets
    print("[ 3/11] ScriptableObject assets ...")
    if resources_dir.exists():
        for f in sorted(resources_dir.glob("*")):
            if f.is_file() and f.suffix in (".asset", ".json"):
                if flat_copy(f, output_dir, used):
                    count += 1

    # 4. Species data
    print("[ 4/11] Species data ...")
    species_csv = UNITY_ROOT / "Assets" / "textdata" / "species.csv"
    if species_csv.exists() and flat_copy(species_csv, output_dir, used):
        count += 1

    # 5. WebGL plugins
    print("[ 5/11] WebGL plugins ...")
    if plugins_dir.exists():
        for f in sorted(plugins_dir.glob("*")):
            if f.is_file() and f.suffix not in SOURCE_SKIP_EXT:
                if flat_copy(f, output_dir, used):
                    count += 1

    # 6. Simulation UI prefabs (YAML). Captures inspector-set wiring between
    #    sim UI scripts and their visual tree (sliders, input boxes, results
    #    rows). simulation.unity itself is intentionally NOT bundled -- its
    #    YAML payload is ~200% of the Claude web app's context window. The
    #    prefabs + scripts together are enough to reconstruct the wiring.
    print("[ 6/11] Simulation UI prefabs ...")
    if sim_prefabs_dir.exists():
        for f in sorted(sim_prefabs_dir.rglob("*.prefab")):
            if flat_copy(f, output_dir, used, "Prefab_"):
                count += 1

    # 7. Unity package manifest + lock (dependency versions).
    print("[ 7/11] Unity package manifest ...")
    if packages_dir.exists():
        for fname in ("manifest.json", "packages-lock.json"):
            p = packages_dir / fname
            if p.exists() and flat_copy(p, output_dir, used, "Package_"):
                count += 1

    # 8. ProjectSettings.asset (build target, scripting defines, layers).
    print("[ 8/11] Project settings ...")
    if settings_dir.exists():
        ps = settings_dir / "ProjectSettings.asset"
        if ps.exists() and flat_copy(ps, output_dir, used, "ProjectSettings_"):
            count += 1

    # 9. Documentation -- keep PDFs, PNGs, xlsx, etc. (only skip .meta)
    print("[ 9/11] Documentation (incl. images, xlsx, diagrams) ...")
    if docs_dir.exists():
        for f in sorted(docs_dir.rglob("*")):
            if f.is_file() and f.suffix not in GLOBAL_SKIP_EXT:
                prefix = "diagram_" if f.parent.name == "diagrams" else ""
                if flat_copy(f, output_dir, used, prefix):
                    count += 1

    # 10a. Validation / test fixtures + notes (top-level Tests/ only;
    #      skip subdirs which contain run outputs).
    print("[10/11] Tests/ validation notes + fixtures ...")
    tests_dir = UNITY_ROOT / "Tests"
    if tests_dir.exists():
        for f in sorted(tests_dir.glob("*")):
            if f.is_file() and f.suffix in {".md", ".csv"}:
                if flat_copy(f, output_dir, used, "Tests_"):
                    count += 1

    # 10b. Both CLAUDE.md files
    print("[10/11] CLAUDE.md (parent + Unity-side) ...")
    parent_claude = PROJECT_ROOT / "CLAUDE.md"
    if parent_claude.exists() and flat_copy(parent_claude, output_dir, used, "ProjectRoot_"):
        count += 1
    unity_claude = UNITY_ROOT / "CLAUDE.md"
    if unity_claude.exists() and flat_copy(unity_claude, output_dir, used, "Unity_"):
        count += 1

    # 11. Generated preface README
    print("[11/11] _README_FOR_AI.md preface ...")
    write_readme(output_dir, referenced)
    count += 1

    print(f"\nDone -- {count} files -> {output_dir}")
    print("All flat, no subdirectories. Ready to upload to an AI agent.")


if __name__ == "__main__":
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else OUTPUT_DEFAULT
    extract(out)
