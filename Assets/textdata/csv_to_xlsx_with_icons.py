#!/usr/bin/env python
# csv_to_xlsx_with_icons.py
# species.csv -> species.xlsx with embedded PNG icons from ./icons
# Drops "SpeciesAmount" and "IconFile", inserts "Icon" between A(Index) and B(Name),
# no autofilter; compact widths; numbers left-aligned & vertically centered.

from __future__ import print_function
import os, sys, csv
import xlsxwriter

try:
    from PIL import Image
    PIL_AVAILABLE = True
except Exception:
    PIL_AVAILABLE = False

# ---------------- Config ----------------
DEFAULT_CSV    = "species.csv"
DEFAULT_ICONS  = "icons"
DEFAULT_XLSX   = "species.xlsx"

ICON_COL_TITLE = "Icon"
ICON_AFTER_HEADER = "Index"  # insert Icon right after this header

# Remove these columns entirely from the XLSX
DROP_COLUMNS = set(["SpeciesAmount", "IconFile"])

# Small icons + centered rows
ICON_BOX_PX   = 40
ROW_PAD_PX    = 6

# Compact column widths (Excel "character" units)
DEFAULT_COL_WIDTH = 10
COL_WIDTHS = {
    "Index": 6,
    ICON_COL_TITLE: 6,
    "Name": 12,
    # Numeric knobs
    "Variant": 8, "Tier": 6, "Cost": 8, "EatingAmount": 8, "ReproductionMultiplier": 8,
    "DeathThreshold": 9, "DeathRate": 8, "MinimumDeaths": 8, "ReproThreshold": 9,
    "EatingStars": 8, "ReproductionStars": 9, "DeathThresholdStars": 10, "DeathRateStars": 9, "ThermalBreadthStars": 10,
    # Thermal curve (wider to avoid #####)
    "OptimalTempK": 12, "ArrhenBreadth": 12, "ArrhenLower": 12, "ArrhenUpper": 12, "LowerBoundK": 12, "UpperBoundK": 12,
    # Text
    "Description": 18, "TemperatureThresholdText": 18, "ReproductionRateText": 18
}
WRAP_COLUMNS = set(["Description", "TemperatureThresholdText", "ReproductionRateText"])

# --------------- IO helpers ---------------
def read_csv(path):
    if sys.version_info[0] < 3:
        f = open(path, 'rb'); import codecs
        reader = csv.reader(codecs.getreader('utf-8')(f))
    else:
        f = open(path, 'r', newline='', encoding='utf-8-sig'); reader = csv.reader(f)
    headers, rows = None, []
    for row in reader:
        if headers is None: headers = row
        else: rows.append(row)
    f.close()
    return headers, rows

def resolve_icon_path(base_dir, icons_dir, csv_value):
    if not csv_value: return None
    p1 = os.path.join(base_dir, csv_value)
    if os.path.isfile(p1): return p1
    p2 = os.path.join(base_dir, icons_dir, os.path.basename(csv_value))
    if os.path.isfile(p2): return p2
    return None

def compute_scale_and_yoffset(img_path, box_px, row_px):
    if not PIL_AVAILABLE:
        s = 0.35; used_h = int(box_px * s); yoff = max(2, (row_px - used_h)//2)
        return s, s, yoff
    try:
        im = Image.open(img_path); w, h = im.size; im.close()
        if w <= 0 or h <= 0:
            return 0.35, 0.35, 2
        s = min(float(box_px)/w, float(box_px)/h)
        used_h = int(h * s); yoff = max(2, (row_px - used_h)//2)
        return s, s, yoff
    except Exception:
        return 0.35, 0.35, 2

def pts(px):  # pixels -> points
    return px * 0.75

# --------------- Main ---------------
def main():
    base = os.getcwd()
    csv_path  = sys.argv[1] if len(sys.argv) > 1 else os.path.join(base, DEFAULT_CSV)
    icons_dir = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_ICONS
    xlsx_out  = sys.argv[3] if len(sys.argv) > 3 else os.path.join(base, DEFAULT_XLSX)

    if not os.path.isabs(csv_path): csv_path = os.path.join(base, csv_path)
    if not os.path.isfile(csv_path): print("CSV not found:", csv_path); sys.exit(1)

    headers, rows = read_csv(csv_path)
    if not headers: print("CSV has no header"); sys.exit(1)

    # Locate the original IconFile column (we’ll drop it from output, but still use its paths)
    iconfile_idx = None
    for i, h in enumerate(headers):
        if h.strip().lower() == "iconfile":
            iconfile_idx = i; break

    # Build output header list: drop unwanted columns, then insert "Icon" after "Index"
    out_headers = [h for h in headers if h not in DROP_COLUMNS]
    try:
        insert_at = out_headers.index(ICON_AFTER_HEADER) + 1
    except ValueError:
        insert_at = 1  # fallback (after first column)
    out_headers.insert(insert_at, ICON_COL_TITLE)
    icon_col = insert_at

    wb = xlsxwriter.Workbook(xlsx_out)
    ws = wb.add_worksheet("species")

    # Left + vertical center everywhere
    header_fmt = wb.add_format({'bold': True, 'bg_color': '#EEEEEE', 'border': 1,
                                'align': 'left', 'valign': 'vcenter'})
    text_fmt   = wb.add_format({'align': 'left', 'valign': 'vcenter'})
    wrap_fmt   = wb.add_format({'text_wrap': True, 'align': 'left', 'valign': 'vcenter'})
    num_fmt    = wb.add_format({'num_format': '0.############', 'align': 'left', 'valign': 'vcenter'})

    # Write headers
    for c, title in enumerate(out_headers):
        ws.write(0, c, title, header_fmt)

    # Column widths (compact; thermal columns wide enough to avoid #####)
    for c, title in enumerate(out_headers):
        w = COL_WIDTHS.get(title, DEFAULT_COL_WIDTH)
        ws.set_column(c, c, w, wrap_fmt if title in WRAP_COLUMNS else text_fmt)

    # Freeze header; NO autofilter
    ws.freeze_panes(1, 0)

    row_h_px = ICON_BOX_PX + ROW_PAD_PX
    row_h_pts = pts(row_h_px)

    # Map from original header index to output column index (skipping dropped columns and accounting for inserted Icon)
    src_to_out = {}
    out_c = 0
    for i, h in enumerate(headers):
        if h in DROP_COLUMNS:  # skip
            continue
        if out_c == icon_col:
            out_c += 1  # reserve for Icon
        src_to_out[i] = out_c
        out_c += 1

    # Write rows
    for r, src in enumerate(rows, start=1):
        ws.set_row(r, row_h_pts)

        # 1) Write normal cells (skipping dropped)
        for i, cell in enumerate(src):
            if i not in src_to_out:  # dropped column
                continue
            c_out = src_to_out[i]
            # numeric?
            wrote = False
            if cell not in (None, ""):
                try:
                    val = float(cell)
                    ws.write_number(r, c_out, val, num_fmt)
                    wrote = True
                except Exception:
                    pass
            if not wrote:
                ws.write(r, c_out, cell, text_fmt)

        # 2) Insert image in Icon column
        img_path = None
        if iconfile_idx is not None and iconfile_idx < len(src):
            img_path = resolve_icon_path(os.path.dirname(csv_path), icons_dir, src[iconfile_idx])

        if img_path:
            xs, ys, yoff = compute_scale_and_yoffset(img_path, ICON_BOX_PX, row_h_px)
            try:
                ws.insert_image(r, icon_col, img_path, {'x_scale': xs, 'y_scale': ys,
                                                        'x_offset': 2, 'y_offset': int(max(2, yoff))})
            except Exception as e:
                ws.write(r, icon_col, img_path, text_fmt)
        else:
            ws.write(r, icon_col, "", text_fmt)

    wb.close()
    print("Wrote:", xlsx_out)

if __name__ == "__main__":
    if sys.version_info[0] >= 3:
        unicode = str
    main()
