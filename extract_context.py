"""
Extract ALL simulation-related TinySea files into a FLAT folder for Claude context.
No subdirectories. Skips .meta files. Prefixes prevent name collisions.

Coverage:
  - All Simulation/ scripts (core, bulk, UI, data structures)
  - All ScriptableObject assets (SimulationConfig, SpeciesDatabase, RunSpeciesList)
  - Game-side scripts the simulation references (ThermalCurve, Temperature, etc.)
  - WebGL plugins (jslib for downloads/ZIP — bulk results)
  - Species data (species.csv)
  - All documentation (condition system, variables, diagrams, reproduction)
  - Both CLAUDE.md files

Usage:
    python extract_context.py                   # outputs to ./claude_context/
    python extract_context.py path/to/output    # outputs to specified folder
"""

import sys
import shutil
from pathlib import Path

# ── Configuration ──────────────────────────────────────────────────────────

PROJECT_ROOT = Path(__file__).resolve().parent
OUTPUT_DEFAULT = PROJECT_ROOT / "claude_context"

SKIP_EXTENSIONS = {".meta", ".pdf", ".png", ".jpg", ".jpeg"}

# Game-side scripts that simulation code references or shares logic with
GAME_SCRIPTS = [
    "ThermalCurve.cs",       # Arrhenius formula (shared between game + sim)
    "Temperature.cs",        # Game temperature model (sim has TemperatureCalculator)
    "TemperatureTrend.cs",   # Game temp trend (sim has TemperatureCalculator)
    "CharacterManager.cs",   # Game species equivalent of SimSpecies
]


def flat_copy(src: Path, output_dir: Path, used: dict, prefix: str = ""):
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


def extract(output_dir: Path):
    if output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    used = {}
    count = 0
    PREFIXES = {"DataStructure": "DS_", "UI": "UI_"}

    # ── 1. Simulation/ scripts (ALL — core, bulk, CSV, WebGL, UI, data) ──
    sim_dir = PROJECT_ROOT / "Assets" / "scripts" / "Simulation"
    print("[1/7] Simulation scripts ...")
    for f in sorted(sim_dir.rglob("*")):
        if f.is_file() and f.suffix not in SKIP_EXTENSIONS:
            prefix = PREFIXES.get(f.parent.name, "")
            if flat_copy(f, output_dir, used, prefix):
                count += 1

    # ── 2. Game-side scripts ──
    scripts_dir = PROJECT_ROOT / "Assets" / "scripts"
    print("[2/7] Game scripts ...")
    for name in GAME_SCRIPTS:
        src = scripts_dir / name
        if src.exists():
            if flat_copy(src, output_dir, used, "Game_"):
                count += 1
        else:
            print(f"  WARNING: {name} not found")

    # ── 3. ScriptableObject assets (Resources/) ──
    resources_dir = PROJECT_ROOT / "Assets" / "Resources"
    print("[3/7] ScriptableObject assets ...")
    for f in sorted(resources_dir.glob("*")):
        if f.is_file() and f.suffix in (".asset", ".json") and f.suffix not in SKIP_EXTENSIONS:
            if flat_copy(f, output_dir, used):
                count += 1

    # ── 4. Species data ──
    print("[4/7] Species data ...")
    species_csv = PROJECT_ROOT / "Assets" / "textdata" / "species.csv"
    if species_csv.exists():
        if flat_copy(species_csv, output_dir, used):
            count += 1

    # ── 5. WebGL plugins (jslib — bulk ZIP downloads, file saves) ──
    plugins_dir = PROJECT_ROOT / "Assets" / "plugins" / "WebGL"
    print("[5/7] WebGL plugins ...")
    if plugins_dir.exists():
        for f in sorted(plugins_dir.glob("*")):
            if f.is_file() and f.suffix not in SKIP_EXTENSIONS:
                if flat_copy(f, output_dir, used):
                    count += 1

    # ── 6. Documentation ──
    docs_dir = PROJECT_ROOT / "docs"
    print("[6/7] Documentation ...")
    for f in sorted(docs_dir.rglob("*")):
        if f.is_file() and f.suffix not in SKIP_EXTENSIONS:
            prefix = "diagram_" if f.parent.name == "diagrams" else ""
            if flat_copy(f, output_dir, used, prefix):
                count += 1

    # ── 7. CLAUDE.md ──
    print("[7/7] CLAUDE.md ...")
    project_claude = PROJECT_ROOT / "CLAUDE.md"
    if project_claude.exists():
        if flat_copy(project_claude, output_dir, used):
            count += 1

    # ── Summary ──
    print(f"\nDone -- {count} files -> {output_dir}")
    print("All flat, no subdirectories. Ready to upload to Claude.")


if __name__ == "__main__":
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else OUTPUT_DEFAULT
    extract(out)
