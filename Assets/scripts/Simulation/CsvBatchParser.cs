using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Static utility to parse and validate a bulk-batch CSV into BulkBatchConfig objects.
///
/// Expected CSV format: 16 global columns + 20 per species × N species (sp1_, sp2_, sp3_, ... prefixed)
/// Species count is detected dynamically by scanning the header for sequential spN_ prefixes.
///
/// Collects ALL errors before returning (does not stop at first error).
/// Unknown columns generate a Debug.Log warning but do not fail.
/// </summary>
public static class CsvBatchParser
{
    private static readonly string[] GLOBAL_COLUMNS =
    {
        "batch_name", "days", "num_scenarios",
        "base_temp", "seasonal_amp", "climate_trend",
        "variability_mag", "warming_bias",
        "daily_var_range", "randomness_growth", "autocorrelated",
        "interannual_variation",
        "temp_min", "temp_max",
        "carrying_cap_t1"
    };

    // Optional global columns with defaults (backward compatible).
    // `use_carrying_cap` is deprecated as of v11.1 — carrying capacity is always on
    // (Tier 1 species without a resource limit grow without bound, which is biologically
    // meaningless and triggers integer-overflow accumulators). Old CSVs that still
    // include the column parse fine; the column's value is logged as a warning and
    // ignored. New CSVs should omit it entirely.
    private static readonly string[] OPTIONAL_GLOBAL_COLUMNS =
    {
        "condition_drain_rate", "condition_recovery_rate",
        "temperature_timeseries_file",
        "use_carrying_cap"
    };

    private static readonly string[] SPECIES_COLUMNS =
    {
        "name", "variant", "tier", "pop",
        "eating", "repro_mult",
        "death_thresh", "death_rate", "repro_thresh",
        "natural_death_rate", "natural_death_var",
        "hunt_eff", "hunt_var",
        "opt_temp_c",
        "arrhen_breadth", "arrhen_lower", "arrhen_upper",
        "lower_bound_c", "upper_bound_c"
    };

    // Optional species columns with defaults (backward compatible)
    private static readonly string[] OPTIONAL_SPECIES_COLUMNS =
    {
        "pmax", "ctmin", "ctmax", "temp_offset",
        "condition_drain_rate", "condition_recovery_rate"
    };

    /// <summary>
    /// Parse CSV content into batch configs with full validation.
    /// Returns true if parsing succeeded with no errors.
    /// </summary>
    public static bool TryParse(string csvContent, out List<BulkBatchConfig> batches, out List<string> errors)
    {
        batches = new List<BulkBatchConfig>();
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            errors.Add("CSV content is empty.");
            return false;
        }

        // Strip BOM if present
        if (csvContent[0] == '\uFEFF')
            csvContent = csvContent.Substring(1);

        string[] lines = csvContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            errors.Add("CSV must have a header row and at least one data row.");
            return false;
        }

        // Parse header → column index map (case-insensitive)
        string[] headers = ParseCsvLine(lines[0]);
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            string col = headers[i].Trim();
            if (!string.IsNullOrEmpty(col))
                columnIndex[col] = i;
        }

        // Detect species count from header (sequential sp1_, sp2_, sp3_, ...)
        int speciesCount = 0;
        for (int i = 1; i <= 100; i++)
        {
            string prefix = $"sp{i}_";
            bool found = false;
            foreach (var kv in columnIndex)
            {
                if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                { found = true; break; }
            }
            if (found)
                speciesCount = i;
            else
                break;
        }

        if (speciesCount == 0)
        {
            errors.Add("No species columns found. CSV must have at least sp1_name, sp1_variant, etc.");
            return false;
        }

        // Build required columns list (global + per-species)
        var requiredColumns = new List<string>(GLOBAL_COLUMNS);
        for (int s = 1; s <= speciesCount; s++)
        {
            string prefix = $"sp{s}_";
            foreach (var col in SPECIES_COLUMNS)
                requiredColumns.Add(prefix + col);
        }

        // Validate all required columns exist
        var missingColumns = new List<string>();
        foreach (var col in requiredColumns)
        {
            if (!columnIndex.ContainsKey(col))
                missingColumns.Add(col);
        }

        if (missingColumns.Count > 0)
        {
            errors.Add($"Missing required columns ({missingColumns.Count}): {string.Join(", ", missingColumns)}");
            return false;
        }

        // Warn about unknown columns (include optional columns as known)
        var knownColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in requiredColumns) knownColumns.Add(col);
        foreach (var col in OPTIONAL_GLOBAL_COLUMNS) knownColumns.Add(col);
        for (int s = 1; s <= speciesCount; s++)
        {
            string prefix = $"sp{s}_";
            foreach (var col in OPTIONAL_SPECIES_COLUMNS)
                knownColumns.Add(prefix + col);
        }
        foreach (var kv in columnIndex)
        {
            if (!knownColumns.Contains(kv.Key))
                Debug.Log($"CsvBatchParser: Unknown column '{kv.Key}' will be ignored.");
        }

        // Parse data rows
        var batchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int rowIdx = 1; rowIdx < lines.Length; rowIdx++)
        {
            string line = lines[rowIdx].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] fields = ParseCsvLine(line);
            int rowNum = rowIdx + 1; // 1-based for error messages

            var batch = new BulkBatchConfig();

            // Parse global fields
            batch.BatchName = GetString(fields, columnIndex, "batch_name");
            batch.Days = GetInt(fields, columnIndex, "days", rowNum, errors);
            batch.NumScenarios = GetInt(fields, columnIndex, "num_scenarios", rowNum, errors);
            batch.BaseTemp = GetFloat(fields, columnIndex, "base_temp", rowNum, errors);
            batch.SeasonalAmp = GetFloat(fields, columnIndex, "seasonal_amp", rowNum, errors);
            batch.ClimateTrend = GetFloat(fields, columnIndex, "climate_trend", rowNum, errors);
            batch.VariabilityMag = GetFloat(fields, columnIndex, "variability_mag", rowNum, errors);
            batch.WarmingBias = GetFloat(fields, columnIndex, "warming_bias", rowNum, errors);
            batch.DailyVarRange = GetFloat(fields, columnIndex, "daily_var_range", rowNum, errors);
            batch.RandomnessGrowth = GetFloat(fields, columnIndex, "randomness_growth", rowNum, errors);
            batch.Autocorrelated = GetBool(fields, columnIndex, "autocorrelated", rowNum, errors);
            batch.InterannualVariation = GetBool(fields, columnIndex, "interannual_variation", rowNum, errors);
            batch.TempMin = GetFloat(fields, columnIndex, "temp_min", rowNum, errors);
            batch.TempMax = GetFloat(fields, columnIndex, "temp_max", rowNum, errors);
            batch.CarryingCapT1 = GetFloat(fields, columnIndex, "carrying_cap_t1", rowNum, errors);

            // Optional global columns (backward compatible — missing columns use defaults)
            batch.ConditionDrainRate = GetFloatOptional(fields, columnIndex, "condition_drain_rate", 0.15f);
            batch.ConditionRecoveryRate = GetFloatOptional(fields, columnIndex, "condition_recovery_rate", 0.10f);
            // Batch 3: optional environmental temperature timeseries file path (empty => parametric model).
            batch.TemperatureTimeseriesFile = GetString(fields, columnIndex, "temperature_timeseries_file");

            // v11.1 deprecation: use_carrying_cap column is deprecated. Carrying capacity
            // is always on. Log a warning if the column is present in the CSV but do not
            // alter behaviour. The bulk loader will treat every row as if it were `true`.
            if (columnIndex.ContainsKey("use_carrying_cap"))
            {
                Debug.LogWarning(
                    $"Row {rowNum}: 'use_carrying_cap' column is deprecated and will be ignored. " +
                    "Carrying capacity is always on as of v11.1. Remove the column from new CSV files.");
            }

            // Parse species (dynamic N species — skip if name is empty)
            for (int s = 1; s <= speciesCount; s++)
            {
                string spName = GetString(fields, columnIndex, $"sp{s}_name");
                if (string.IsNullOrWhiteSpace(spName))
                    continue;

                var sp = new BulkSpeciesConfig();
                ParseSpecies(fields, columnIndex, $"sp{s}_", sp, rowNum, errors);
                batch.Species.Add(sp);
            }

            // A row with no usable species (every sp_name blank) can't simulate anything.
            if (batch.Species.Count == 0)
                errors.Add($"Row {rowNum}: at least one species is required — all sp_name columns are blank.");

            // Cross-field validation
            ValidateBatch(batch, rowNum, errors);

            // Unique batch_name check
            if (!string.IsNullOrEmpty(batch.BatchName))
            {
                if (batchNames.Contains(batch.BatchName))
                    errors.Add($"Row {rowNum}: Duplicate batch_name '{batch.BatchName}'.");
                else
                    batchNames.Add(batch.BatchName);
            }

            batches.Add(batch);
        }

        if (batches.Count == 0)
            errors.Add("No data rows found.");

        return errors.Count == 0;
    }

    // ==================== SPECIES PARSING ====================

    private static void ParseSpecies(string[] fields, Dictionary<string, int> columnIndex,
        string prefix, BulkSpeciesConfig species, int rowNum, List<string> errors)
    {
        species.Name = GetString(fields, columnIndex, prefix + "name");
        species.Variant = GetString(fields, columnIndex, prefix + "variant");
        species.Tier = GetInt(fields, columnIndex, prefix + "tier", rowNum, errors);
        species.Pop = GetInt(fields, columnIndex, prefix + "pop", rowNum, errors);
        species.Eating = GetFloat(fields, columnIndex, prefix + "eating", rowNum, errors);
        species.ReproMult = GetFloat(fields, columnIndex, prefix + "repro_mult", rowNum, errors);
        species.DeathThresh = GetFloat(fields, columnIndex, prefix + "death_thresh", rowNum, errors);
        species.DeathRate = GetFloat(fields, columnIndex, prefix + "death_rate", rowNum, errors);
        species.ReproThresh = GetFloat(fields, columnIndex, prefix + "repro_thresh", rowNum, errors);
        species.NaturalDeathRate = GetFloat(fields, columnIndex, prefix + "natural_death_rate", rowNum, errors);
        species.NaturalDeathVar = GetFloat(fields, columnIndex, prefix + "natural_death_var", rowNum, errors);
        species.HuntEff = GetFloat(fields, columnIndex, prefix + "hunt_eff", rowNum, errors);
        species.HuntVar = GetFloat(fields, columnIndex, prefix + "hunt_var", rowNum, errors);
        species.OptTempC = GetFloat(fields, columnIndex, prefix + "opt_temp_c", rowNum, errors);
        species.ArrhenBreadth = GetFloat(fields, columnIndex, prefix + "arrhen_breadth", rowNum, errors);
        species.ArrhenLower = GetFloat(fields, columnIndex, prefix + "arrhen_lower", rowNum, errors);
        species.ArrhenUpper = GetFloat(fields, columnIndex, prefix + "arrhen_upper", rowNum, errors);
        species.LowerBoundC = GetFloat(fields, columnIndex, prefix + "lower_bound_c", rowNum, errors);
        species.UpperBoundC = GetFloat(fields, columnIndex, prefix + "upper_bound_c", rowNum, errors);

        // Optional columns with variant-aware defaults (backward compatible — missing columns use variant defaults)
        var parsedVariant = SpeciesData.ResolveVariantEnum(species.Variant);  // Batch 1B: Cold/Warm/Hot + legacy aliases
        SpeciesData.GetVariantThermalDefaults(parsedVariant, out float defPmax, out float defCtMin, out float defCtMax);

        species.Pmax = GetFloatOptional(fields, columnIndex, prefix + "pmax", defPmax);
        species.CTminC = GetFloatOptional(fields, columnIndex, prefix + "ctmin", defCtMin);
        species.CTmaxC = GetFloatOptional(fields, columnIndex, prefix + "ctmax", defCtMax);
        species.TempOffset = GetFloatOptional(fields, columnIndex, prefix + "temp_offset", 0f);
        // Batch 2: per-species condition timescale. Default -1 => inherit the row-global rate.
        species.ConditionDrainRate = GetFloatOptional(fields, columnIndex, prefix + "condition_drain_rate", -1f);
        species.ConditionRecoveryRate = GetFloatOptional(fields, columnIndex, prefix + "condition_recovery_rate", -1f);
    }

    // ==================== VALIDATION ====================

    private static void ValidateBatch(BulkBatchConfig batch, int rowNum, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(batch.BatchName))
            errors.Add($"Row {rowNum}: batch_name cannot be empty.");

        if (batch.Days < 1 || batch.Days > 182500)
            errors.Add($"Row {rowNum}: days must be between 1 and 182500.");

        if (batch.NumScenarios < 1 || batch.NumScenarios > 100)
            errors.Add($"Row {rowNum}: num_scenarios must be between 1 and 100.");

        if (batch.TempMax <= batch.TempMin)
            errors.Add($"Row {rowNum}: temp_max ({batch.TempMax}) must be greater than temp_min ({batch.TempMin}).");

        // Carrying capacity is always on (v11.1) — cap value must always be positive.
        if (batch.CarryingCapT1 <= 0)
            errors.Add($"Row {rowNum}: carrying_cap_t1 must be positive (carrying capacity is always on).");

        for (int s = 0; s < batch.Species.Count; s++)
            ValidateSpecies(batch.Species[s], $"sp{s + 1}", rowNum, errors);
    }

    private static void ValidateSpecies(BulkSpeciesConfig sp, string prefix, int rowNum, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(sp.Name))
            errors.Add($"Row {rowNum}: {prefix}_name cannot be empty.");

        if (string.IsNullOrWhiteSpace(sp.Variant))
            errors.Add($"Row {rowNum}: {prefix}_variant cannot be empty.");
        // Batch 1A: any non-empty variant label is accepted (free-text). Legacy names
        // (Common/Tropical/Arctic/Custom, case-insensitive) still resolve to their
        // thermal defaults via SpeciesData.GetVariantThermalDefaults; unknown labels
        // fall back to Custom/Common defaults for blank optional columns.

        // Tier-1-only simulator: only prey (tier 0) species are accepted. Any tier != 0
        // (predator / Tier-2 rows) is rejected at parse with a clear error.
        if (sp.Tier != 0)
            errors.Add($"Row {rowNum}: {prefix}_tier must be 0. This is a Tier-1 (prey-only) simulator — Tier 2 (predator) species are not supported.");

        if (sp.Pop < 0)
            errors.Add($"Row {rowNum}: {prefix}_pop must be non-negative.");

        if (sp.DeathThresh < 0 || sp.DeathThresh > 1)
            errors.Add($"Row {rowNum}: {prefix}_death_thresh must be between 0 and 1.");

        if (sp.DeathRate < 0 || sp.DeathRate > 1)
            errors.Add($"Row {rowNum}: {prefix}_death_rate must be between 0 and 1.");

        if (sp.ReproThresh < 0 || sp.ReproThresh > 1)
            errors.Add($"Row {rowNum}: {prefix}_repro_thresh must be between 0 and 1.");

        if (sp.ReproMult < 0)
            errors.Add($"Row {rowNum}: {prefix}_repro_mult must be non-negative.");

        if (sp.NaturalDeathRate < 0)
            errors.Add($"Row {rowNum}: {prefix}_natural_death_rate must be non-negative.");

        if (sp.NaturalDeathVar < 0)
            errors.Add($"Row {rowNum}: {prefix}_natural_death_var must be non-negative.");

        if (sp.HuntEff < 0 || sp.HuntEff > 1)
            errors.Add($"Row {rowNum}: {prefix}_hunt_eff must be between 0 and 1.");

        if (sp.HuntVar < 0)
            errors.Add($"Row {rowNum}: {prefix}_hunt_var must be non-negative.");

        if (sp.UpperBoundC <= sp.LowerBoundC)
            errors.Add($"Row {rowNum}: {prefix}_upper_bound_c ({sp.UpperBoundC}) must be greater than {prefix}_lower_bound_c ({sp.LowerBoundC}).");
    }

    // ==================== VALUE EXTRACTION ====================

    private static string GetString(string[] fields, Dictionary<string, int> columnIndex, string column)
    {
        if (!columnIndex.TryGetValue(column, out int idx) || idx >= fields.Length)
            return "";
        return fields[idx].Trim();
    }

    private static int GetInt(string[] fields, Dictionary<string, int> columnIndex,
        string column, int rowNum, List<string> errors)
    {
        string val = GetString(fields, columnIndex, column);
        if (string.IsNullOrEmpty(val))
        {
            errors.Add($"Row {rowNum}: '{column}' cannot be empty.");
            return 0;
        }
        if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            return result;
        errors.Add($"Row {rowNum}: '{column}' value '{val}' is not a valid integer.");
        return 0;
    }

    private static float GetFloat(string[] fields, Dictionary<string, int> columnIndex,
        string column, int rowNum, List<string> errors)
    {
        string val = GetString(fields, columnIndex, column);
        if (string.IsNullOrEmpty(val))
        {
            errors.Add($"Row {rowNum}: '{column}' cannot be empty.");
            return 0f;
        }
        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            return result;
        errors.Add($"Row {rowNum}: '{column}' value '{val}' is not a valid number.");
        return 0f;
    }

    private static float GetFloatOptional(string[] fields, Dictionary<string, int> columnIndex,
        string column, float defaultValue)
    {
        if (!columnIndex.TryGetValue(column, out int idx) || idx >= fields.Length)
            return defaultValue;
        string val = fields[idx].Trim();
        if (string.IsNullOrEmpty(val))
            return defaultValue;
        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            return result;
        return defaultValue;
    }

    private static bool GetBool(string[] fields, Dictionary<string, int> columnIndex,
        string column, int rowNum, List<string> errors)
    {
        string val = GetString(fields, columnIndex, column);
        if (string.IsNullOrEmpty(val))
        {
            errors.Add($"Row {rowNum}: '{column}' cannot be empty.");
            return false;
        }
        string lower = val.ToLowerInvariant();
        if (lower == "true" || lower == "1" || lower == "yes") return true;
        if (lower == "false" || lower == "0" || lower == "no") return false;
        errors.Add($"Row {rowNum}: '{column}' value '{val}' is not a valid boolean (use true/false, 1/0, or yes/no).");
        return false;
    }

    // ==================== TEMPLATE GENERATION ====================

    /// <summary>
    /// Generate a downloadable template CSV: the header (same column definitions as
    /// TryParse, so they always stay in sync) plus one data row that mirrors the LIVE
    /// simulation defaults — the SimulationConfig in Resources and the RunSpeciesList it
    /// references. The template is therefore current by construction and Tier-1-only:
    /// every species row is whatever the configured list holds (all tier 0).
    ///
    /// This is deliberately dynamic. The previous hard-coded example row drifted out of
    /// sync — it still emitted Tier-2 'Sheplik' rows and legacy Arctic/Common/Tropical
    /// variants that the current parser rejects, so a freshly downloaded template failed
    /// to re-upload. Mirroring the assets makes that class of bug impossible.
    ///
    /// Note: the deprecated `use_carrying_cap` column is intentionally omitted.
    /// </summary>
    public static string GenerateTemplate()
    {
        // Species: the curated default list (the Tier-1 organisms) PLUS one fully custom
        // example species, so the template doubles as a guideline for adding your own.
        // N species are supported — the column count below is driven by this list's size.
        var runSpecies = Resources.Load<RunSpeciesList>("RunSpeciesList");
        var species = new List<SpeciesData>();
        if (runSpecies != null && runSpecies.speciesList != null)
        {
            foreach (var sd in runSpecies.speciesList)
                if (sd != null) species.Add(sd);
        }
        species.Add(BuildCustomExampleSpecies());   // final row: custom name / variant / values

        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        // ---------- Header (skip the deprecated use_carrying_cap column) ----------
        foreach (var col in GLOBAL_COLUMNS)
            sb.Append(col).Append(',');
        foreach (var col in OPTIONAL_GLOBAL_COLUMNS)
        {
            if (col == "use_carrying_cap") continue;
            sb.Append(col).Append(',');
        }
        for (int s = 1; s <= species.Count; s++)
        {
            string prefix = $"sp{s}_";
            foreach (var col in SPECIES_COLUMNS)
                sb.Append(prefix).Append(col).Append(',');
            foreach (var col in OPTIONAL_SPECIES_COLUMNS)
                sb.Append(prefix).Append(col).Append(',');
        }
        sb.Length--;            // drop trailing comma
        sb.AppendLine();

        // ---------- Data row: globals = canonical DEFAULTS ----------
        // Hard-coded to mirror the shipped SimulationConfig defaults — deliberately NOT read
        // from the live config instance (the input UI mutates that, and the template must
        // always emit the defaults, never in-session edits). Keep in sync with the
        // SimulationConfig defaults / the Reset button. Columns, in order:
        //   batch_name, days, num_scenarios, base_temp, seasonal_amp, climate_trend,
        //   variability_mag, warming_bias, daily_var_range, randomness_growth, autocorrelated,
        //   interannual_variation, temp_min, temp_max, carrying_cap_t1, condition_drain_rate,
        //   condition_recovery_rate, temperature_timeseries_file(empty=parametric model)
        sb.Append("default_batch,365,5,20,5,0,0,0,5,0,true,false,0,40,5000,0.15,0.1,,");

        // ---------- Data row: one block per species, mirroring ConvertSpecies ----------
        // ConvertSpecies does Celsius -> Kelvin (+273.15) on opt/lower/upper temps, so we
        // invert it here (Kelvin -> Celsius) for the *_c columns. Everything else is 1:1.
        for (int i = 0; i < species.Count; i++)
        {
            var sp = species[i];
            string name = string.IsNullOrEmpty(sp.speciesLabel) ? sp.speciesName.ToString() : sp.speciesLabel;
            string variant = string.IsNullOrEmpty(sp.variantLabel) ? sp.variant.ToString() : sp.variantLabel;

            sb.Append(EscapeCsv(name)).Append(',');
            sb.Append(EscapeCsv(variant)).Append(',');
            sb.Append(sp.tier).Append(',');
            sb.Append(sp.count).Append(',');
            sb.Append(sp.eatingAmount.ToString(ci)).Append(',');
            sb.Append(sp.reproductionMultiplier.ToString(ci)).Append(',');
            sb.Append(sp.deathThreshold.ToString(ci)).Append(',');
            sb.Append(sp.deathRate.ToString(ci)).Append(',');
            sb.Append(sp.reproThreshold.ToString(ci)).Append(',');
            sb.Append(sp.naturalDeathRate.ToString(ci)).Append(',');
            sb.Append(sp.naturalDeathVariance.ToString(ci)).Append(',');
            sb.Append(sp.huntingEfficiency.ToString(ci)).Append(',');
            sb.Append(sp.huntingVariance.ToString(ci)).Append(',');
            sb.Append((sp.optimalTempK - 273.15f).ToString("0.##", ci)).Append(',');
            sb.Append(sp.arrhenBreadth.ToString(ci)).Append(',');
            sb.Append(sp.arrhenLower.ToString(ci)).Append(',');
            sb.Append(sp.arrhenUpper.ToString(ci)).Append(',');
            sb.Append((sp.lowerBoundK - 273.15f).ToString("0.##", ci)).Append(',');
            sb.Append((sp.upperBoundK - 273.15f).ToString("0.##", ci)).Append(',');
            sb.Append(sp.pmax.ToString(ci)).Append(',');
            sb.Append(sp.ctMinC.ToString(ci)).Append(',');
            sb.Append(sp.ctMaxC.ToString(ci)).Append(',');
            sb.Append(sp.TemperatureDebuff.ToString(ci)).Append(',');
            sb.Append(sp.conditionDrainRate.ToString(ci)).Append(',');
            sb.Append(sp.conditionRecoveryRate.ToString(ci)).Append(',');
        }

        sb.Length--;            // drop trailing comma
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// A fully custom example species for the template — custom name, custom variant, and
    /// hand-picked biology values — so the template shows exactly how to add your own (it can
    /// be deleted if unwanted). Tier 0 (the only legal tier in this Tier-1 sim). Temps are
    /// stored in Kelvin here; GenerateTemplate emits them back as Celsius (the *_c columns).
    /// </summary>
    private static SpeciesData BuildCustomExampleSpecies()
    {
        return new SpeciesData
        {
            speciesName = SpeciesName.Custom,
            speciesLabel = "CustomSpecies",
            variant = SpeciesVariant.Custom,
            variantLabel = "My Custom Variant",
            tier = 0,
            count = 20,
            eatingAmount = 3f,
            reproductionMultiplier = 0.45f,
            deathThreshold = 0.3f,
            deathRate = 0.6f,
            reproThreshold = 0.25f,
            naturalDeathRate = 0.02f,
            naturalDeathVariance = 0.01f,
            huntingEfficiency = 1f,
            huntingVariance = 0f,
            optimalTempK = 21f + 273.15f,
            arrhenBreadth = 6000f,
            arrhenLower = 5000f,
            arrhenUpper = 35000f,
            lowerBoundK = 20f + 273.15f,
            upperBoundK = 22f + 273.15f,
            pmax = 0.8f,
            ctMinC = 1f,
            ctMaxC = 38f,
            TemperatureDebuff = 0f,
            conditionDrainRate = 0.15f,
            conditionRecoveryRate = 0.10f
        };
    }

    /// <summary>Quote a CSV field if it contains a comma, quote, or newline (writer side).</summary>
    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    // ==================== CSV LINE PARSER ====================

    /// <summary>
    /// Parse a single CSV line, handling quoted fields with escaped quotes.
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
