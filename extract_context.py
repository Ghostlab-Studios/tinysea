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
  - All documentation in docs/ (markdown, diagrams, screenshots, xlsx, etc.)
  - Both CLAUDE.md files (parent project overview + Unity-side)
  - A generated _README_FOR_AI.md preface explaining project history + bundle scope

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
- All documentation under `docs/` -- the authoritative simulation spec,
  diagrams (mermaid), screenshots, and the simulation-variables spreadsheet.
- Both `CLAUDE.md` files (top-level project overview + Unity-side).
- Game-side scripts that the simulation directly references, auto-detected
  by scanning Simulation/*.cs for their class names. In this bundle:
{refs}

## What's NOT in this bundle

- Unity scenes, prefabs, materials, animations, audio, sprites (binary).
- Game-only scripts the simulation never references (UI, scenes, shop, etc.).
- The website (`Tinysea PHP webiste/`) -- separate concern.
- Build outputs (`Builds/`, `Library/`, `Temp/`).

## File-naming convention

All files are flat (no subfolders) with prefixes to avoid collisions:
  - `Game_*`     -- game scripts referenced by the simulation
  - `DS_*`       -- data structures (from `Simulation/DataStructure/`)
  - `UI_*`       -- simulation UI (results screen, config editor)
  - `diagram_*`  -- mermaid diagrams from `docs/diagrams/`
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

    sim_dir       = UNITY_ROOT / "Assets" / "scripts" / "Simulation"
    scripts_dir   = UNITY_ROOT / "Assets" / "scripts"
    resources_dir = UNITY_ROOT / "Assets" / "Resources"
    plugins_dir   = UNITY_ROOT / "Assets" / "plugins" / "WebGL"
    docs_dir      = UNITY_ROOT / "docs"

    # 1. Simulation/ scripts (everything, recursive)
    print("[1/8] Simulation scripts ...")
    for f in sorted(sim_dir.rglob("*")):
        if f.is_file() and f.suffix not in SOURCE_SKIP_EXT:
            prefix = PREFIXES.get(f.parent.name, "")
            if flat_copy(f, output_dir, used, prefix):
                count += 1

    # 2. Game-side scripts referenced by simulation (auto-detected)
    print("[2/8] Game scripts referenced by simulation (auto-detect) ...")
    referenced = detect_referenced_game_scripts(sim_dir, scripts_dir)
    for src in referenced:
        if flat_copy(src, output_dir, used, "Game_"):
            count += 1
    print(f"      detected {len(referenced)} referenced game script(s)")

    # 3. ScriptableObject assets
    print("[3/8] ScriptableObject assets ...")
    if resources_dir.exists():
        for f in sorted(resources_dir.glob("*")):
            if f.is_file() and f.suffix in (".asset", ".json"):
                if flat_copy(f, output_dir, used):
                    count += 1

    # 4. Species data
    print("[4/8] Species data ...")
    species_csv = UNITY_ROOT / "Assets" / "textdata" / "species.csv"
    if species_csv.exists() and flat_copy(species_csv, output_dir, used):
        count += 1

    # 5. WebGL plugins
    print("[5/8] WebGL plugins ...")
    if plugins_dir.exists():
        for f in sorted(plugins_dir.glob("*")):
            if f.is_file() and f.suffix not in SOURCE_SKIP_EXT:
                if flat_copy(f, output_dir, used):
                    count += 1

    # 6. Documentation -- keep PDFs, PNGs, xlsx, etc. (only skip .meta)
    print("[6/8] Documentation (incl. images, xlsx, diagrams) ...")
    if docs_dir.exists():
        for f in sorted(docs_dir.rglob("*")):
            if f.is_file() and f.suffix not in GLOBAL_SKIP_EXT:
                prefix = "diagram_" if f.parent.name == "diagrams" else ""
                if flat_copy(f, output_dir, used, prefix):
                    count += 1

    # 7. Both CLAUDE.md files
    print("[7/8] CLAUDE.md (parent + Unity-side) ...")
    parent_claude = PROJECT_ROOT / "CLAUDE.md"
    if parent_claude.exists() and flat_copy(parent_claude, output_dir, used, "ProjectRoot_"):
        count += 1
    unity_claude = UNITY_ROOT / "CLAUDE.md"
    if unity_claude.exists() and flat_copy(unity_claude, output_dir, used, "Unity_"):
        count += 1

    # 8. Generated preface README
    print("[8/8] _README_FOR_AI.md preface ...")
    write_readme(output_dir, referenced)
    count += 1

    print(f"\nDone -- {count} files -> {output_dir}")
    print("All flat, no subdirectories. Ready to upload to an AI agent.")


if __name__ == "__main__":
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else OUTPUT_DEFAULT
    extract(out)
