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
        "use_carrying_cap", "carrying_cap_t1"
    };

    // Optional global columns with defaults (backward compatible)
    private static readonly string[] OPTIONAL_GLOBAL_COLUMNS =
    {
        "condition_drain_rate", "condition_recovery_rate"
    };

    private static readonly string[] SPECIES_COLUMNS =
    {
        "name", "variant", "tier", "pop",
        "eating", "repro_mult",
        "death_thresh", "death_rate", "min_deaths", "repro_thresh",
        "natural_death_rate", "natural_death_var",
        "hunt_eff", "hunt_var",
        "opt_temp_c",
        "arrhen_breadth", "arrhen_lower", "arrhen_upper",
        "lower_bound_c", "upper_bound_c"
    };

    // Optional species columns with defaults (backward compatible)
    private static readonly string[] OPTIONAL_SPECIES_COLUMNS =
    {
        "pmax", "ctmin", "ctmax", "temp_offset"
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
            batch.UseCarryingCap = GetBool(fields, columnIndex, "use_carrying_cap", rowNum, errors);
            batch.CarryingCapT1 = GetFloat(fields, columnIndex, "carrying_cap_t1", rowNum, errors);

            // Optional global columns (backward compatible — missing columns use defaults)
            batch.ConditionDrainRate = GetFloatOptional(fields, columnIndex, "condition_drain_rate", 0.15f);
            batch.ConditionRecoveryRate = GetFloatOptional(fields, columnIndex, "condition_recovery_rate", 0.10f);

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
        species.MinDeaths = GetFloat(fields, columnIndex, prefix + "min_deaths", rowNum, errors);
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
        Enum.TryParse<SpeciesVariant>(species.Variant, true, out var parsedVariant);
        SpeciesData.GetVariantThermalDefaults(parsedVariant, out float defPmax, out float defCtMin, out float defCtMax);

        species.Pmax = GetFloatOptional(fields, columnIndex, prefix + "pmax", defPmax);
        species.CTminC = GetFloatOptional(fields, columnIndex, prefix + "ctmin", defCtMin);
        species.CTmaxC = GetFloatOptional(fields, columnIndex, prefix + "ctmax", defCtMax);
        species.TempOffset = GetFloatOptional(fields, columnIndex, prefix + "temp_offset", 0f);
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

        if (batch.UseCarryingCap && batch.CarryingCapT1 <= 0)
            errors.Add($"Row {rowNum}: carrying_cap_t1 must be positive when use_carrying_cap is true.");

        for (int s = 0; s < batch.Species.Count; s++)
            ValidateSpecies(batch.Species[s], $"sp{s + 1}", rowNum, errors);
    }

    private static void ValidateSpecies(BulkSpeciesConfig sp, string prefix, int rowNum, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(sp.Name))
            errors.Add($"Row {rowNum}: {prefix}_name cannot be empty.");

        if (string.IsNullOrWhiteSpace(sp.Variant))
            errors.Add($"Row {rowNum}: {prefix}_variant cannot be empty.");
        else if (!Enum.TryParse<SpeciesVariant>(sp.Variant, true, out _))
            errors.Add($"Row {rowNum}: {prefix}_variant '{sp.Variant}' is not valid. Use: Common, Tropical, Arctic, or Custom.");

        if (sp.Tier < 0 || sp.Tier > 1)
            errors.Add($"Row {rowNum}: {prefix}_tier must be 0 (prey) or 1 (predator).");

        if (sp.Pop < 0)
            errors.Add($"Row {rowNum}: {prefix}_pop must be non-negative.");

        if (sp.DeathThresh < 0 || sp.DeathThresh > 1)
            errors.Add($"Row {rowNum}: {prefix}_death_thresh must be between 0 and 1.");

        if (sp.DeathRate < 0 || sp.DeathRate > 1)
            errors.Add($"Row {rowNum}: {prefix}_death_rate must be between 0 and 1.");

        if (sp.ReproThresh < 0 || sp.ReproThresh > 1)
            errors.Add($"Row {rowNum}: {prefix}_repro_thresh must be between 0 and 1.");

        if (sp.MinDeaths < 0)
            errors.Add($"Row {rowNum}: {prefix}_min_deaths must be non-negative.");

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
