"""Build a single combined PDF of all TinySea simulation diagrams.

Reads the rendered PNGs from docs/diagrams/rendered/v12/, adds a title page,
table of contents, and one page per diagram with title and caption.

Usage: python build_diagrams_pdf.py
"""
import os
import datetime
from reportlab.lib.pagesizes import letter, landscape, portrait
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch
from reportlab.lib.colors import HexColor
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Image, PageBreak, Table, TableStyle,
    NextPageTemplate, PageTemplate, BaseDocTemplate, Frame,
)
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.utils import ImageReader

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DIAGRAMS_DIR = os.path.join(SCRIPT_DIR, 'diagrams')
RENDER_DIR = os.path.join(DIAGRAMS_DIR, 'rendered', 'v12')
OUTPUT_PDF = os.path.join(RENDER_DIR, 'TinySea-Simulation-Diagrams.pdf')

# Reading order matches docs/diagrams/README.md index: outer-to-inner zoom.
DIAGRAMS = [
    ('bulk-hierarchy', 'Bulk → Run → Scenario hierarchy',
     'How a bulk CSV upload becomes N runs, each producing M scenarios and output files. Source: BulkSimulationController.cs, SimulationController.cs, CsvBatchParser.cs.'),
    ('scenario-flow', 'Scenario flow',
     'Per-scenario day loop: temperature → biology dispatch → record StepRecord → crash check → CSV emission. Source: SimulationRunner.Run, EcosystemSimulator.HasCrashed.'),
    ('temperature-model', 'Temperature model',
     'Daily temperature = BaseTemperature + seasonal sinusoid + linear climate trend + interannual variation (zero-mean, biasMean-corrected) + autocorrelated daily noise, clamped to [MinTemp, MaxTemp]. Source: TemperatureCalculator.cs.'),
    ('biology-overview', 'Biology — 10-step overview',
     'The 10 sub-steps invoked each day in EcosystemSimulator.ProcessBiologyStep: thermal performance → feeding/predation → raw final performance → update Condition → final performance → thermal death → condition death → reproduction → natural death → population rounding.'),
    ('biology-performance-phase', 'Biology phase 1: performance (Steps 1–5)',
     'Arrhenius thermal performance with cosine fade near CTmin/CTmax, Tier 1 food-pool linear FedRate (v10), Tier 2 Holling II + per-predator FedRate (v11), Condition update with Pmax rate scaling (v9). Source: SimSpecies.CalculatePerformance, EcosystemSimulator.ProcessFeedingWithAccumulator, UpdateCondition.'),
    ('biology-death-phase', 'Biology phase 2: death (Steps 6–7)',
     'Thermal death (instant wipeout when RawThermalPerformance == 0) and condition death (graduated severity below DeathThreshold + survivor fitness boost to prevent death spirals). Source: EcosystemSimulator.ApplyThermalDeath, ApplyConditionDeath.'),
    ('biology-life-phase', 'Biology phase 3: life (Steps 8–9)',
     'Reproduction with piecewise reproScale joined at STRUGGLING_REPRO_RATE = 0.10, Pmax birth multiplier (v9), Tier 1 NO_PREDATOR_PENALTY = 0.85, fractional birth accumulator. Newborns inherit parent group Condition (v10). Natural death is flat-rate, performance-independent. Source: EcosystemSimulator.ApplyReproduction, ApplyNaturalDeathWithAccumulator.'),
    ('pmax-flow', 'Pmax flow through the pipeline',
     'The four places Pmax enters biology, all post-Condition: Step 1 thermal performance scaling, Step 4 Condition drain divisor, Step 4 Condition recovery multiplier, Step 8 birth multiplier. Pmax is deliberately absent from the Condition target so thresholds are species-agnostic.'),
    ('csv-output-shape', 'CSV output shape',
     'Per-scenario CSV (#config: header + #species: table + 41 daily columns + 17 v12 per-species columns + #summary + #extinction), aggregate.csv (10+ === SECTION === blocks including v12 per-species metrics), and bulk_summary.csv (6 sections, with v12.3 cross-run filtering for Condition). Source: SimulationRunner.ToCsvInternal, ScenarioResult.ToAggregateCsv, BulkSimulationController.GenerateBulkSummary.'),
    ('accumulator-pattern', 'Fractional-event accumulator pattern',
     'How births / predation / natural death / condition death track fractional events across days: rawEvents -> accumulator[FullName] -> floor() -> integer apply, residual carries to next day. v12 adds public accessors GetBirthAccum / GetNaturalDeathAccum / GetConditionDeathAccum / GetPredationAccum. _thermalDeathAccumulators is declared but unused (thermal death is binary).'),
]


def fit_image(png_path, max_w, max_h):
    """Return a reportlab Image scaled to fit within (max_w, max_h) preserving aspect."""
    iw, ih = ImageReader(png_path).getSize()
    scale = min(max_w / iw, max_h / ih)
    return Image(png_path, width=iw * scale, height=ih * scale, hAlign='CENTER')


def build_pdf():
    page_size = letter  # 8.5 x 11 in portrait
    doc = SimpleDocTemplate(
        OUTPUT_PDF, pagesize=page_size,
        topMargin=0.55 * inch, bottomMargin=0.55 * inch,
        leftMargin=0.6 * inch, rightMargin=0.6 * inch,
        title='TinySea Simulation Diagrams (v12)',
        author='TinySea / Northeastern GhostLab',
        subject='Headless ecosystem simulator visual reference',
    )

    styles = getSampleStyleSheet()
    PAGE_W = doc.width
    PAGE_H = doc.height

    s_title = ParagraphStyle(
        'Title', parent=styles['Title'],
        fontSize=30, leading=36, alignment=TA_CENTER,
        textColor=HexColor('#1a5276'),
    )
    s_subtitle = ParagraphStyle(
        'Subtitle', parent=styles['Normal'],
        fontSize=14, leading=18, alignment=TA_CENTER,
        textColor=HexColor('#555555'),
    )
    s_meta = ParagraphStyle(
        'Meta', parent=styles['Normal'],
        fontSize=11, leading=15, alignment=TA_CENTER,
        textColor=HexColor('#666666'),
    )
    s_h1 = ParagraphStyle(
        'H1', parent=styles['Heading1'],
        fontSize=18, leading=22, spaceBefore=0, spaceAfter=4,
        textColor=HexColor('#1a5276'),
    )
    s_caption = ParagraphStyle(
        'Caption', parent=styles['Normal'],
        fontSize=9.5, leading=12.5, textColor=HexColor('#444444'),
        spaceAfter=10, alignment=TA_LEFT,
    )

    story = []

    # ---------------- Title page ----------------
    story.append(Spacer(1, 1.6 * inch))
    story.append(Paragraph('TinySea<br/>Simulation Diagrams', s_title))
    story.append(Spacer(1, 0.3 * inch))
    story.append(Paragraph('Headless ecosystem simulator', s_subtitle))
    story.append(Paragraph('Visual reference', s_subtitle))
    story.append(Spacer(1, 0.7 * inch))
    story.append(Paragraph('<b>Model version:</b> v12-per-species-tracking', s_meta))
    story.append(Paragraph('<b>Source:</b> tinysea/Assets/scripts/Simulation/', s_meta))
    today = datetime.date.today().isoformat()
    story.append(Paragraph(f'<b>Generated:</b> {today}', s_meta))
    story.append(Spacer(1, 0.4 * inch))
    story.append(Paragraph('Northeastern University GhostLab', s_meta))
    story.append(PageBreak())

    # ---------------- Table of Contents ----------------
    story.append(Paragraph('Contents', s_h1))
    story.append(Spacer(1, 0.15 * inch))

    s_toc_cell = ParagraphStyle(
        'TocCell', parent=styles['Normal'],
        fontSize=9, leading=11.5, textColor=HexColor('#222222'),
    )
    s_toc_head = ParagraphStyle(
        'TocHead', parent=styles['Normal'],
        fontSize=10, leading=12, textColor=colors.white,
        fontName='Helvetica-Bold',
    )

    def _p(text, style=s_toc_cell):
        return Paragraph(text, style)

    toc_data = [[_p('#', s_toc_head), _p('Diagram', s_toc_head), _p('Covers', s_toc_head)]]
    for i, (slug, title, blurb) in enumerate(DIAGRAMS, 1):
        # Use first sentence for the TOC blurb; wraps inside column.
        short = blurb.split('. ')[0]
        if not short.endswith('.'):
            short += '.'
        toc_data.append([_p(str(i)), _p(f'<b>{title}</b>'), _p(short)])

    col_widths = [0.4 * inch, 2.4 * inch, PAGE_W - 0.4 * inch - 2.4 * inch]
    toc_table = Table(toc_data, colWidths=col_widths, repeatRows=1)
    toc_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), HexColor('#1a5276')),
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('GRID', (0, 0), (-1, -1), 0.4, HexColor('#cccccc')),
        ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, HexColor('#f5f5f5')]),
        ('TOPPADDING', (0, 0), (-1, -1), 6),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 6),
        ('LEFTPADDING', (0, 0), (-1, -1), 8),
        ('RIGHTPADDING', (0, 0), (-1, -1), 8),
    ]))
    story.append(toc_table)
    story.append(Spacer(1, 0.25 * inch))
    story.append(Paragraph(
        '<i>One Mermaid diagram per page. Each is rendered from the source-controlled '
        '<b>.md</b> file under <font face="Courier">tinysea/docs/diagrams/</font>. '
        'If a diagram and the source code disagree, trust the source and update the diagram.</i>',
        s_caption,
    ))
    story.append(PageBreak())

    # ---------------- Diagrams ----------------
    for i, (slug, title, blurb) in enumerate(DIAGRAMS, 1):
        png_path = os.path.join(RENDER_DIR, f'{slug}-1.png')
        if not os.path.exists(png_path):
            print(f'WARNING: missing {png_path}; skipping')
            continue

        story.append(Paragraph(f'{i}. {title}', s_h1))
        story.append(Paragraph(blurb, s_caption))

        avail_h = PAGE_H - 1.4 * inch  # reserve for header + caption
        avail_w = PAGE_W
        story.append(fit_image(png_path, avail_w, avail_h))
        story.append(PageBreak())

    # ---------------- Closing page ----------------
    story.append(Spacer(1, 2.5 * inch))
    story.append(Paragraph('Sources of truth', s_h1))
    story.append(Spacer(1, 0.2 * inch))
    sources = [
        ('Biology sequence', 'Assets/scripts/Simulation/EcosystemSimulator.cs'),
        ('Per-species fields & Arrhenius', 'Assets/scripts/Simulation/SimSpecies.cs'),
        ('Temperature model', 'Assets/scripts/Simulation/TemperatureCalculator.cs'),
        ('Scenario orchestration & CSV', 'Assets/scripts/Simulation/SimulationRunner.cs'),
        ('Per-scenario / aggregate stats', 'Assets/scripts/Simulation/DataStructure/ScenarioResult.cs'),
        ('Bulk orchestration & CSV', 'Assets/scripts/Simulation/BulkSimulationController.cs'),
        ('Bulk row schema', 'Assets/scripts/Simulation/BulkBatchConfig.cs'),
        ('Bulk CSV parser', 'Assets/scripts/Simulation/CsvBatchParser.cs'),
        ('Config schema', 'Assets/scripts/Simulation/DataStructure/SimulationConfig.cs'),
    ]
    src_data = [['Area', 'File']] + [[a, f] for a, f in sources]
    src_table = Table(src_data, colWidths=[2.2 * inch, PAGE_W - 2.2 * inch], repeatRows=1)
    src_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), HexColor('#1a5276')),
        ('TEXTCOLOR', (0, 0), (-1, 0), colors.white),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, 0), 10),
        ('FONTSIZE', (0, 1), (-1, -1), 9.5),
        ('FONTNAME', (1, 1), (1, -1), 'Courier'),
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('GRID', (0, 0), (-1, -1), 0.4, HexColor('#cccccc')),
        ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, HexColor('#f5f5f5')]),
        ('TOPPADDING', (0, 0), (-1, -1), 5),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 5),
        ('LEFTPADDING', (0, 0), (-1, -1), 8),
        ('RIGHTPADDING', (0, 0), (-1, -1), 8),
    ]))
    story.append(src_table)
    story.append(Spacer(1, 0.4 * inch))
    story.append(Paragraph(
        'See <b>docs/simulation-spec.md</b> and <b>docs/csv-formats.md</b> for the '
        'textual spec; <b>docs/pending-list.md</b> for known issues.',
        s_caption,
    ))

    doc.build(story)
    print(f'PDF generated: {OUTPUT_PDF}')
    print(f'Size: {os.path.getsize(OUTPUT_PDF) / 1024:.1f} KB')


if __name__ == '__main__':
    build_pdf()
