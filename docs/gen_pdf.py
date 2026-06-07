"""Generate a user-facing PDF guide for TinySea Simulation."""
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch
from reportlab.lib.colors import HexColor
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, Image
)
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT, TA_CENTER
import os


def build_pdf(output_path):
    doc = SimpleDocTemplate(
        output_path, pagesize=letter,
        topMargin=0.6*inch, bottomMargin=0.6*inch,
        leftMargin=0.65*inch, rightMargin=0.65*inch,
    )
    styles = getSampleStyleSheet()
    W = doc.width  # available width

    # -- Styles --
    s_title = ParagraphStyle('DocTitle', parent=styles['Title'],
        fontSize=22, spaceAfter=4, leading=26)
    s_subtitle = ParagraphStyle('SubTitle', parent=styles['Normal'],
        fontSize=10, spaceAfter=14, alignment=TA_CENTER,
        textColor=HexColor('#777777'))
    s_h1 = ParagraphStyle('H1', parent=styles['Heading1'],
        fontSize=16, spaceBefore=18, spaceAfter=8,
        textColor=HexColor('#1a5276'), borderWidth=0)
    s_h2 = ParagraphStyle('H2', parent=styles['Heading2'],
        fontSize=12, spaceBefore=12, spaceAfter=5,
        textColor=HexColor('#2c3e50'))
    s_h3 = ParagraphStyle('H3', parent=styles['Heading3'],
        fontSize=10, spaceBefore=8, spaceAfter=3,
        textColor=HexColor('#34495e'))
    s_body = ParagraphStyle('Body', parent=styles['Normal'],
        fontSize=9, leading=12, spaceAfter=5)
    s_note = ParagraphStyle('Note', parent=styles['Normal'],
        fontSize=8.5, leading=11, spaceAfter=5,
        textColor=HexColor('#555555'), leftIndent=8)
    s_tc = ParagraphStyle('TC', parent=styles['Normal'],
        fontSize=7.5, leading=10, alignment=TA_LEFT)
    s_th = ParagraphStyle('TH', parent=styles['Normal'],
        fontSize=7.5, leading=10, alignment=TA_LEFT, textColor=colors.white)

    ts_default = TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), HexColor('#2c3e50')),
        ('TEXTCOLOR', (0, 0), (-1, 0), colors.white),
        ('FONTSIZE', (0, 0), (-1, -1), 7.5),
        ('ALIGN', (0, 0), (-1, -1), 'LEFT'),
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('GRID', (0, 0), (-1, -1), 0.4, HexColor('#cccccc')),
        ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, HexColor('#f8f8f8')]),
        ('TOPPADDING', (0, 0), (-1, -1), 3),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 3),
        ('LEFTPADDING', (0, 0), (-1, -1), 4),
        ('RIGHTPADDING', (0, 0), (-1, -1), 4),
    ])

    def T(text):
        """Cell text."""
        return Paragraph(text, s_tc)

    def TH(text):
        """Header cell."""
        return Paragraph(f'<b>{text}</b>', s_th)

    def make_table(headers, rows, col_widths=None):
        data = [[TH(h) for h in headers]]
        for row in rows:
            data.append([T(c) for c in row])
        t = Table(data, colWidths=col_widths, repeatRows=1)
        t.setStyle(ts_default)
        return t

    # Column width helpers
    c3 = [W*0.22, W*0.13, W*0.65]  # field, default, description
    c3b = [W*0.25, W*0.15, W*0.60]
    c4 = [W*0.22, W*0.10, W*0.10, W*0.58]  # field, type, default, desc
    c2 = [W*0.25, W*0.75]  # name, description

    # Screenshot paths
    ss_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'screenshots')

    def add_screenshot(img_name, caption=None):
        """Add a screenshot scaled to page width, with optional caption."""
        img_path = os.path.join(ss_dir, img_name)
        if not os.path.exists(img_path):
            return
        img = Image(img_path, width=W, height=W * 0.56)  # ~16:9 aspect
        img.hAlign = 'CENTER'
        story.append(img)
        if caption:
            story.append(Paragraph(f'<i>{caption}</i>', s_note))
        story.append(Spacer(1, 8))

    story = []

    # ===================== TITLE =====================
    story.append(Paragraph('TinySea Simulation Guide', s_title))
    story.append(Paragraph('Reference for UI fields, CSV formats, and output data', s_subtitle))
    story.append(Spacer(1, 6))

    # ===================== 1. SIMULATION SETUP =====================
    add_screenshot('MainTempScreen.png', 'Simulation setup screen — left panel (temperature) and right panel (species & parameters)')
    story.append(Paragraph('1. Simulation Setup (Left Panel)', s_h1))
    story.append(Paragraph(
        'These fields appear on the left side of the simulation configuration screen. '
        'All temperatures are in Celsius.', s_body))

    story.append(Paragraph('Base Parameters', s_h2))
    story.append(make_table(
        ['UI Field', 'Default', 'Description'],
        [
            ['Base Temperature (tMean)', '20', 'The average/mean annual temperature. The simulation oscillates around this value seasonally.'],
        ], c3))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Seasonal', s_h2))
    story.append(make_table(
        ['UI Field', 'Default', 'Description'],
        [
            ['Seasonal Amplitude', '10', 'How much temperature swings above/below the base each year. 10 means the range is roughly base +/- 10 degrees across summer and winter.'],
        ], c3))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Climate', s_h2))
    story.append(make_table(
        ['UI Field', 'Default', 'Description'],
        [
            ['Climate Trend', '1', 'Degrees of warming per year (linear). 0 = stable climate. 0.02 = slow warming. Negative = cooling.'],
            ['Interannual Variation', 'On', 'When checked, each simulated year gets a random temperature offset, making some years warmer/cooler than others.'],
        ], c3))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Daily Variation', s_h2))
    story.append(make_table(
        ['UI Field', 'Default', 'Description'],
        [
            ['Autocorrelation', 'On', 'When checked, day-to-day temperature changes are smooth (like real weather). When off, each day is fully random.'],
            ['Random Range', '5', 'Base daily temperature variation range in degrees. Higher = more day-to-day noise.'],
            ['Randomness Growth Rate', '0.5', 'How much the daily random range increases each year. Models increasing climate instability over time.'],
        ], c3))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Temperature Bounds', s_h2))
    story.append(make_table(
        ['UI Field', 'Default', 'Description'],
        [
            ['Min', '-5', 'Hard floor -- temperature never drops below this value.'],
            ['Max', '50', 'Hard ceiling -- temperature never exceeds this value.'],
        ], c3))
    story.append(Spacer(1, 4))

    # ===================== RIGHT PANEL =====================
    story.append(Paragraph('1b. Simulation Setup (Right Panel)', s_h1))

    story.append(Paragraph('Species Count', s_h2))
    story.append(Paragraph(
        'The Tier 1 and Tier 2 tables show species in the simulation. '
        'Click the [+] button to add a species. Click a species row to edit it (opens the Species Config screen). '
        'Tier 1 = prey, Tier 2 = predators. Each species has a Name, Variant type (Arctic/Common/Tropical/Custom), '
        'a thermal curve preview, and starting population Count.', s_body))
    story.append(Spacer(1, 2))

    story.append(Paragraph('Interannual Variation', s_h2))
    story.append(make_table(
        ['UI Field', 'Default', 'Description'],
        [
            ['Year-to-year swing range', '2', 'Magnitude of year-to-year random temperature offset. Higher = more variation between years.'],
            ['Warming Bias', '1.5', 'Skews the year-to-year variation toward warmer years. 0 = symmetric. Higher = warmer years more likely.'],
        ], c3))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Simulation Parameters', s_h2))
    story.append(make_table(
        ['UI Field', 'Default', 'Description'],
        [
            ['Carrying Capacity / Soft Limit', '5000', 'Maximum sustainable Tier 1 population. As prey approaches this number, their birth rate decreases. Does NOT kill creatures -- just slows reproduction.'],
            ['Health Rate: Drain', '0.15', 'How fast species health declines when temperature is bad. Higher = faster decline.'],
            ['Health Rate: Recovery', '0.10', 'How fast species health recovers when temperature is good. Intentionally slower than drain (harder to recover than to get sick).'],
            ['Days per scenario', '365', 'How many days each simulation scenario runs. 365 = 1 year.'],
            ['Number of scenarios', '5', 'How many times to run the simulation with different random seeds. More scenarios = better statistics.'],
        ], c3))

    story.append(PageBreak())

    # ===================== 2. SPECIES CONFIG =====================
    add_screenshot('species_config.png', 'Species configuration — biology parameters (left) and thermal curve editor (right)')
    story.append(Paragraph('2. Species Configuration', s_h1))
    story.append(Paragraph(
        'This screen opens when you click a species in the Species Count table, or click [+] to add one. '
        'Left side has biology parameters, right side has the thermal curve editor with a live preview graph.', s_body))

    story.append(Paragraph('Biology Parameters (Left)', s_h2))
    story.append(make_table(
        ['UI Field', 'Prey Default', 'Predator Default', 'Description'],
        [
            ['Name', '--', '--', 'Species name (e.g. Hexapod, Sheplik, or any custom name).'],
            ['Variant', 'Common', 'Common', 'Thermal type: Arctic (cold-adapted), Common (generalist), Tropical (warm-adapted), or Custom.'],
            ['Count', '1000', '100', 'Starting population at the beginning of the simulation.'],
            ['Eating Amount', '0', '1.5', 'Prey consumed per creature per day. Always 0 for prey (Tier 1).'],
            ['Repro Threshold', '0.25', '0.25', 'Health level where reproduction transitions from struggling to healthy. Not a hard gate -- reproduction can still occur below this, just at very low rates.'],
            ['Reproduction Multiplier', '0.45', '0.1', 'Birth rate multiplier. Higher = faster reproduction. Predators reproduce ~4.5x slower than prey.'],
            ['Temperature DeathThreshold', '0.3', '0.3', 'Health level below which species start dying from thermal stress. Above this = safe.'],
            ['Temperature DeathRate', '0.6', '0.3', 'Max fraction that die per day when health is below the death threshold. Prey die faster (less physiological buffering).'],
            ['Temperature Offset', '0', '0', 'Shifts the temperature this species "feels." Positive = feels warmer than ambient.'],
            ['Natural DeathVariance', '0.01', '0.005', 'Random daily fluctuation in natural death rate.'],
            ['Natural DeathRate', '0.02', '0.01', 'Base death rate from old age/disease (always active regardless of temperature). 0.02 = 2% per day.'],
        ], c4))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Tier 2 Only (Predator)', s_h3))
    story.append(make_table(
        ['UI Field', 'Default', 'Description'],
        [
            ['Hunting Efficiency', '0.75', 'Base hunting success rate (75%). Adjusted by prey availability -- more prey = easier hunting, fewer prey = harder.'],
            ['Hunting Variance', '0.15', 'Daily random fluctuation in hunting success.'],
        ], c3))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Thermal Curve Editor (Right)', s_h2))
    story.append(Paragraph(
        'The graph shows how well the species performs at each temperature. The green curve is the thermal performance '
        'curve -- the peak is where the species does best. Performance drops on both sides.', s_body))
    story.append(make_table(
        ['UI Field', 'Description'],
        [
            ['Optimal Temp', 'Temperature (in Celsius) where the species performs best. The peak of the curve.'],
            ['Lower Bound', 'Cold-side inflection point. Below this, performance drops faster.'],
            ['Upper Bound', 'Warm-side inflection point. Above this, performance drops faster.'],
            ['Lethal Min', 'Below this temperature, the species dies immediately. A 2-degree fade zone applies near this limit.'],
            ['Lethal Max', 'Above this temperature, the species dies immediately. Same 2-degree fade zone.'],
            ['Arrhen Breadth', 'Width of the thermal curve. Higher = wider tolerance range. Common (8000) is broader than Arctic/Tropical (4000).'],
            ['Arrhen Lower', 'Controls how steeply performance drops on the cold side.'],
            ['Arrhen Upper', 'Controls how steeply performance drops on the warm side.'],
            ['Peak Height (Pmax)', 'Maximum performance at optimal temperature. 0.65 for Common (generalist trade-off), 0.85 for Arctic/Tropical (specialist advantage).'],
            ['Area under the Curve', 'Visual indicator only -- shows the total area under the performance curve.'],
        ], c2))

    story.append(PageBreak())

    # ===================== 3. BULK CSV =====================
    add_screenshot('upload.png', 'Bulk CSV upload screen — drag-and-drop or press L to load a file')
    story.append(Paragraph('3. Bulk CSV Upload', s_h1))
    story.append(Paragraph(
        'Press <b>L</b> or drag-and-drop a CSV file onto the upload screen. Each row in the CSV defines one '
        '"run" with its own temperature settings and species. The simulation executes all rows and packages '
        'results as a ZIP download. Use <b>Download Template</b> to get a blank CSV with all columns.', s_body))

    story.append(Paragraph('Environment Columns (one value per row)', s_h2))
    story.append(make_table(
        ['CSV Column', 'Description'],
        [
            ['batch_name', 'Label for this run (shows up in output filenames and summary).'],
            ['days', 'Days per scenario (e.g. 365 for 1 year, 730 for 2 years).'],
            ['num_scenarios', 'Number of scenarios per run (1-100). More = better statistics.'],
            ['base_temp', 'Base temperature in Celsius.'],
            ['seasonal_amp', 'Seasonal amplitude in Celsius.'],
            ['climate_trend', 'Warming per year in Celsius (0 = stable, negative = cooling).'],
            ['variability_mag', 'Year-to-year variation magnitude.'],
            ['warming_bias', 'Bias toward warmer years.'],
            ['daily_var_range', 'Daily random variation range.'],
            ['randomness_growth', 'How much daily randomness increases per year.'],
            ['autocorrelated', 'true/false -- smooth daily transitions.'],
            ['interannual_variation', 'true/false -- enable year-to-year variation.'],
            ['temp_min', 'Temperature floor (hard minimum).'],
            ['temp_max', 'Temperature ceiling (hard maximum).'],
            ['use_carrying_cap', 'true/false -- enable prey population soft limit.'],
            ['carrying_cap_t1', 'Tier 1 carrying capacity (required if carrying cap is on).'],
            ['condition_drain_rate', 'Health drain rate (optional, default 0.15).'],
            ['condition_recovery_rate', 'Health recovery rate (optional, default 0.10).'],
        ], c2))
    story.append(Spacer(1, 6))

    story.append(Paragraph('Species Columns (prefixed sp1_, sp2_, sp3_, ...)', s_h2))
    story.append(Paragraph(
        'Each species gets 20 columns with a numbered prefix. For example, the first species uses '
        'sp1_name, sp1_variant, sp1_tier, sp1_pop, etc. The second uses sp2_name, sp2_variant, and so on. '
        'Leave sp*_name blank to skip a slot. All temperatures in Celsius.', s_body))
    story.append(make_table(
        ['Column Suffix', 'Description'],
        [
            ['name', 'Species name (e.g. "Hexapod", "Coral", or any custom name).'],
            ['variant', 'Arctic, Common, Tropical, or Custom.'],
            ['tier', '0 = Tier 1 (prey), 1 = Tier 2 (predator).'],
            ['pop', 'Starting population.'],
            ['eating', 'Prey consumed per creature per day (0 for prey).'],
            ['repro_mult', 'Reproduction multiplier.'],
            ['death_thresh', 'Health threshold for thermal death.'],
            ['death_rate', 'Max death rate when below threshold.'],
            ['repro_thresh', 'Health inflection for reproduction.'],
            ['natural_death_rate', 'Base natural death rate.'],
            ['natural_death_var', 'Natural death variance.'],
            ['hunt_eff', 'Hunting efficiency (predators only).'],
            ['hunt_var', 'Hunting variance.'],
            ['opt_temp_c', 'Optimal temperature (Celsius).'],
            ['arrhen_breadth', 'Arrhenius breadth (curve width).'],
            ['arrhen_lower', 'Arrhenius lower (cold drop-off).'],
            ['arrhen_upper', 'Arrhenius upper (warm drop-off).'],
            ['lower_bound_c', 'Lower thermal bound (Celsius).'],
            ['upper_bound_c', 'Upper thermal bound (Celsius).'],
            ['pmax', 'Peak performance height (0-1). Optional, defaults to 1.0.'],
        ], c2))

    story.append(PageBreak())

    # ===================== 4. RESULTS =====================
    add_screenshot('Results.png', 'Results screen — progress, statistics, and download buttons')
    story.append(Paragraph('4. Understanding Results', s_h1))
    story.append(Paragraph(
        'After simulation completes, you see the Results screen with download buttons. '
        'There are two download options:', s_body))

    story.append(Paragraph('Download Options', s_h2))
    story.append(make_table(
        ['Button', 'What You Get'],
        [
            ['Download Aggregate CSV', 'A single CSV with summary statistics across all scenarios for the current run.'],
            ['Download All (ZIP)', 'A ZIP file with everything: individual scenario CSVs, aggregate CSV, config CSV, and (for bulk) a bulk_summary.csv at the root.'],
        ], c2))
    story.append(Spacer(1, 6))

    story.append(Paragraph('Terminology', s_h2))
    story.append(make_table(
        ['Term', 'Definition'],
        [
            ['Scenario', 'One simulation execution with one random seed. The atomic unit. If you set "Number of scenarios" to 5, you get 5 scenarios.'],
            ['Run', 'One configuration that generates N scenarios. In the UI: the current setup. In bulk mode: one CSV row.'],
            ['Bulk', 'A collection of runs uploaded as a single CSV file. Each row = one run.'],
        ], c2))
    story.append(Spacer(1, 8))

    # -- Scenario CSV --
    story.append(Paragraph('4a. Scenario CSV (one per scenario)', s_h2))
    story.append(Paragraph(
        'Each scenario file has configuration comments at the top (lines starting with #), '
        'then daily population data. R reads these cleanly with read.csv() since # is the default comment character.', s_body))
    story.append(Paragraph('Key columns in the daily data:', s_note))
    story.append(make_table(
        ['Column', 'Description'],
        [
            ['Day', 'Simulation day (1, 2, 3, ...).'],
            ['Temperature', 'Temperature on this day (Celsius).'],
            ['Tier1Pop / Tier2Pop', 'Total prey / predator population at end of day.'],
            ['Tier{n}_{variantLabel} (dynamic)', 'Population breakdown by variant label, one column per distinct (tier, variant label) present in the run (e.g. Tier1_Hot_Specialist, Tier1_M2). Replaces the old fixed Tier1Arctic/Common/Tropical/Custom + Tier2* buckets; ThermalVariant enum names are no longer emitted.'],
            ['EatenT1', 'Prey eaten by predators this day.'],
            ['TempDeathsT1 / TempDeathsT2', 'Deaths from lethal temperatures (instant kill at extreme temps).'],
            ['ConditionDeathsT1 / ConditionDeathsT2', 'Deaths from chronic thermal stress (health drops below death threshold).'],
            ['NaturalDeathsT1 / NaturalDeathsT2', 'Deaths from natural causes (old age, disease -- always active).'],
            ['BirthsT1 / BirthsT2', 'New offspring this day.'],
            ['AvgConditionT1 / AvgConditionT2', 'Average health of the population (0 = dead, 1 = perfect health).'],
        ], c2))
    story.append(Spacer(1, 8))

    # -- Aggregate CSV --
    story.append(Paragraph('4b. Aggregate CSV (one per run)', s_h2))
    story.append(Paragraph(
        'Summarizes all scenarios within one run. Sections are separated by === TITLE === headers.', s_body))

    story.append(Paragraph('Summary section:', s_h3))
    story.append(make_table(
        ['Field', 'Description'],
        [
            ['Scenarios Run', 'Total number of scenarios executed.'],
            ['Survived / Crashed', 'How many scenarios survived vs had total population collapse.'],
            ['Crash Rate', 'Percentage of scenarios that crashed.'],
            ['Avg Crash Day', 'Average day when crashes occurred (only shown if any crashed).'],
        ], c2))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Per-Species Population Stats (the key section for Brian\'s request):', s_h3))
    story.append(Paragraph(
        'Every species gets its own row with statistics across all scenarios. Custom species are '
        'tracked individually by name (e.g. "Coral_Custom", "Nudibranch_Custom").', s_note))
    story.append(make_table(
        ['Column', 'Description'],
        [
            ['Species', 'Species name and variant (e.g. Hexapod_Arctic, Coral_Custom).'],
            ['Avg', 'Average final population across ALL scenarios (including extinct ones showing 0).'],
            ['SurvivedAvg', 'Average final population only across scenarios where this species survived. More meaningful -- shows what it achieves when alive, not diluted by extinctions.'],
            ['Min', 'Smallest final population across any scenario.'],
            ['Max', 'Largest final population across any scenario.'],
            ['Extinct', 'Number of scenarios where this species went extinct (final population = 0).'],
            ['Survived', 'Number of scenarios where this species survived (final population > 0).'],
            ['ExtinctionRate', 'Percentage of scenarios where this species went extinct.'],
        ], c2))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Individual Scenarios table:', s_h3))
    story.append(Paragraph(
        'One row per scenario showing seed, crash status, final populations by variant, '
        'and temperature stats. Includes T1Custom/T2Custom columns.', s_note))
    story.append(Spacer(1, 8))

    # -- Bulk Summary --
    story.append(Paragraph('4c. Bulk Summary CSV (one per bulk upload)', s_h2))
    story.append(Paragraph(
        'Found at the ZIP root as bulk_summary.csv. Aggregates results across ALL runs in a bulk upload.', s_body))

    story.append(Paragraph('Per-Run Results table:', s_h3))
    story.append(Paragraph(
        'One row per CSV row (run), showing: run name, scenarios survived/crashed, crash rate, '
        'base temperature, climate trend, and average population for each species.', s_note))
    story.append(Spacer(1, 4))

    story.append(Paragraph('Per-Species Aggregate (across all runs):', s_h3))
    story.append(make_table(
        ['Column', 'Description'],
        [
            ['Species', 'Species name_variant (same format as aggregate.csv).'],
            ['GrandMean', 'Average of each run\'s average population. Note: if runs have very different temperatures, this mixes unlike conditions.'],
            ['SurvivedMean', 'Average population only across runs where the species survived. More meaningful than GrandMean when many runs cause extinction.'],
            ['RunsExtinct', 'Number of runs where this species went extinct.'],
            ['RunsSurvived', 'Number of runs where this species had survivors.'],
            ['ExtinctionRate', 'Percentage of runs where this species went extinct.'],
        ], c2))
    story.append(Paragraph(
        '<b>Note:</b> There are no Min/Max columns at the bulk level. Min/Max of averages are not meaningful '
        'real population values -- use the per-run aggregate.csv files for Min/Max.', s_note))

    story.append(PageBreak())

    # ===================== 5. DEFAULT SPECIES =====================
    story.append(Paragraph('5. Default Species Quick Reference', s_h1))
    story.append(Paragraph(
        'Default thermal parameters for the three standard variants. These are the starting points '
        'when you select a variant in the Species Config screen.', s_body))

    story.append(make_table(
        ['Parameter', 'Arctic', 'Common', 'Tropical'],
        [
            ['Optimal Temp', '18 C', '24 C', '30 C'],
            ['Arrhen Breadth', '4,000 (narrow)', '8,000 (wide)', '4,000 (narrow)'],
            ['Arrhen Lower', '13,974', '3,000', '15,827'],
            ['Arrhen Upper', '35,000', '35,000', '35,000'],
            ['Lower Bound', '17.75 C', '23 C', '29.75 C'],
            ['Upper Bound', '17.95 C', '25 C', '29.95 C'],
            ['Peak Height (Pmax)', '0.85 (specialist)', '0.65 (generalist)', '0.85 (specialist)'],
        ], [W*0.25, W*0.25, W*0.25, W*0.25]))
    story.append(Spacer(1, 4))
    story.append(Paragraph(
        '<b>Key insight:</b> Arctic and Tropical are specialists -- high peak (0.85) but very narrow thermal windows. '
        'Common is a generalist -- lower peak (0.65) but much wider tolerance. '
        'This means Common dominates at moderate temperatures, while specialists only thrive when '
        'conditions closely match their optimal range.', s_note))
    story.append(Spacer(1, 8))

    story.append(Paragraph('Default Biology Parameters', s_h2))
    story.append(make_table(
        ['Parameter', 'Prey (Tier 1)', 'Predator (Tier 2)', 'Why Different'],
        [
            ['Starting Pop', '1,000', '100', 'Prey outnumber predators ~10:1'],
            ['Eating Amount', '0', '1.5', 'Prey don\'t eat other species'],
            ['Repro Multiplier', '0.45', '0.1', 'Prey reproduce ~4.5x faster'],
            ['Death Threshold', '0.3', '0.3', 'Same stress tolerance'],
            ['Death Rate', '0.6', '0.3', 'Prey die faster from stress (smaller = less buffering)'],
            ['Natural Death Rate', '2%', '1%', 'Prey have shorter lifespans'],
            ['Hunting Efficiency', 'N/A', '75%', 'Only predators hunt'],
        ], [W*0.22, W*0.20, W*0.20, W*0.38]))

    story.append(Spacer(1, 16))
    story.append(Paragraph(
        '<i>Auto-generated reference. For the latest details, check the simulation source code or contact the team.</i>',
        ParagraphStyle('Footer', parent=styles['Normal'],
            fontSize=7.5, textColor=HexColor('#999999'), alignment=TA_CENTER)))

    doc.build(story)
    print(f"PDF generated: {output_path}")


if __name__ == '__main__':
    script_dir = os.path.dirname(os.path.abspath(__file__))
    pdf_path = os.path.join(script_dir, 'TinySea_Simulation_Guide.pdf')
    build_pdf(pdf_path)
