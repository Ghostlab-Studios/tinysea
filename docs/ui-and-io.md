# Simulation UI and IO

This document describes the Unity scene UI that configures and drives a headless
ecosystem run, and the file IO that persists results. It covers the input UI,
the species editor, the thermal curve editor, the results screen, the scenario
rows, file save and load paths, server upload, and the native file dialog. All
file and line references point at C# under
`tinysea/Assets/scripts/Simulation/`.

The simulation lives in the scene `Assets/scenes/simulation.unity`. That is the
only scene with `enabled: 1` in `ProjectSettings/EditorBuildSettings.asset`
(`EditorBuildSettings.asset:23-25`), so it is the shipping build.

Related documents (do not duplicate, cross reference):

- `run-scenario-batch.md` for the day loop, `SimulationController`, and
  `SimulationRunner`.
- `bulk-system.md` for `BulkSimulationController`, `CsvUploadHandler`, and the
  bulk CSV format.
- `configuration-reference.md` for `SimulationConfig` field semantics, ranges,
  and defaults.
- `csv-output-formats.md` for the scenario, aggregate, config, and bulk summary
  CSV layouts.
- `data-structures.md` for `SpeciesData`, `ScenarioResult`, and
  `AggregateResults` field definitions.
- `biology-and-formulas.md` and `temperature-model.md` for the formulas the
  thermal editor previews.

## Build target status

The WebGL build target is deprecated and canceled. The current build targets are
Windows standalone and macOS standalone. Every WebGL code path in this subsystem
is guarded by `#if UNITY_WEBGL && !UNITY_EDITOR` and is not the active path:

- `WebGLDownload.DownloadCsv` (`WebGLDownload.cs:11-19`) and the whole
  `WebGLZipDownload` jslib surface (`WebGLZipDownload.cs:21-33`).
- `ServerUpload.IsAvailable` returns `true` only on WebGL non-editor
  (`ServerUpload.cs:33-43`); on Windows and macOS it returns `false`, so server
  upload never runs.
- The WebGL branches in `ResultsScreenUI.TriggerDownload` and
  `ResultsScreenUI.DownloadAllAsZip` (`ResultsScreenUI.cs:638-648, 745-765`).

These scripts remain in the tree because the same C# wrappers compile on every
platform and the non-WebGL `#else` branches carry the active behavior (write to
disk, open with the OS app). When reading the code, treat the `#else` branch of
each guard as the live path for Windows and macOS, and the `#if UNITY_WEBGL`
branch as dead for shipping builds.

## Scene UI map

The simulation scene has the input panel, the species panels, the species editor
panel, and the results panel. The shipping `simulation.unity` scene contains
exactly two `SpeciesTierConfig` instances: one for Tier 1 with `tier: 0` and
`maxSpeciesPerTier: 6`, and one for Tier 2 with `tier: 1` and `maxSpeciesPerTier: 3`.
Both share the same `RunSpeciesList`. The Tier 2 panel exists in the scene but is
secondary legacy: current runs are Tier 1 only (`SimulationConfig.Tier2Enabled`
defaults false), so the Tier 2 panel's add catalog is empty and the panel adds
nothing. The `SpeciesUIController` and `ScenarioRowUI` counts are dynamic, one per
species row and one per scenario respectively.

| UI controller | File | Role |
|---|---|---|
| `SimulationInputUI` | `UI/SimulationInputUI.cs` | Environment and run parameters, validate and start a run |
| `SpeciesTierConfig` (two instances: Tier 1 and Tier 2) | `UI/SpeciesTierConfig.cs` | Add and remove species rows for a tier |
| `SpeciesUIController` (one per species row) | `UI/SpeciesUIController.cs` | Display one species row, inline count edit, open editor |
| `EditSpeciesUI` | `UI/EditSpeciesUI.cs` | Full species editor panel |
| `ThermalParameterController` | `UI/ThermalParameterController.cs` | Drives the thermal sliders and graph in the editor |
| `ThermalParameterSlider` | `UI/ThermalParameterSlider.cs` | One slider plus input field, Celsius or raw |
| `ThermalGraphEditor` | `UI/ThermalGraphEditor.cs` | High resolution editable curve preview |
| `ThermalGraphUI` | `UI/ThermalGraphUI.cs` | Read only curve thumbnail on a species row |
| `ResultsScreenUI` | `UI/ResultsScreenUI.cs` | Progress, results, scenario list, downloads |
| `ScenarioRowUI` (one per scenario) | `UI/ScenarioRowUI.cs` | One scenario result row in the results list |

The species controllers communicate through a static event bus
`SpeciesEditEvents` (`DataStructure/SpeciesEditEvents.cs`) so no controller holds
a direct reference to another. Shared rendering helpers are `PixelFont`
(`UI/PixelFont.cs`) and `AxisHelper` (`UI/AxisHelper.cs`).

Data flows from UI into two ScriptableObjects: `SimulationConfig` (environment
and run parameters) and `RunSpeciesList` (the mutable species list). The
controllers mutate these in place. See `configuration-reference.md` for the
ScriptableObject field reference.

## Input UI: `SimulationInputUI`

`SimulationInputUI` (`SimulationInputUI.cs:6`) binds a fixed set of TMP input
fields and toggles to fields on a `SimulationConfig` asset (serialized at
`SimulationInputUI.cs:9`). It also holds a reference to `SimulationController`
(`SimulationInputUI.cs:11`) to start the run.

### Bound fields

Each control maps one to one to a `SimulationConfig` field. Floats use
`float.TryParse` with `NumberStyles.Float` and `CultureInfo.InvariantCulture`;
ints use `NumberStyles.Integer`.

All temperature fields on this panel are in degrees Celsius, matching the display
convention used everywhere in the UI. The config fields back this: `BaseTemperature`
is documented as "Base/mean temperature in °C" (`SimulationConfig.cs:79-80`),
`ClimateTrend` is °C warming per year (`SimulationConfig.cs:87-88`), and
`TemperatureBoundsMin`/`TemperatureBoundsMax` are the min and max possible
temperature in °C (`SimulationConfig.cs:110-115`). The thermal editor converts to
Kelvin internally, but nothing on this input panel is in Kelvin.

| UI control | Config field | Type | Units | Range enforced on Run |
|---|---|---|---|---|
| `BaseTemperatureInput` | `BaseTemperature` | float | °C | none |
| `SeasonalAmplitude` | `SeasonalAmplitude` | float | °C swing | none |
| `ClimateTrend` | `ClimateTrend` | float | °C per year | none |
| `InterannualVariation` (toggle) | `InterannualVariation` | bool | n/a | n/a |
| `VariabilityMagnitude` | `VariabilityMagnitude` | float | °C | none |
| `WarmingBias` | `WarmingBias` | float | °C | none |
| `Autocorrelated` (toggle) | `Autocorrelated` | bool | n/a | n/a |
| `DailyVariationRange` | `DailyVariationRange` | float | °C | none |
| `RandomnessGrowthRate` | `RandomnessGrowthRate` | float | °C per year | none |
| `TemperatureBoundsMin` | `TemperatureBoundsMin` | float | °C | none |
| `TemperatureBoundsMax` | `TemperatureBoundsMax` | float | °C | none |
| `CarryingCapacityTier1` | `CarryingCapacityTier1` | float | individuals | none |
| `ConditionDrainRate` | `ConditionDrainRate` | float | per day | none |
| `ConditionRecoveryRate` | `ConditionRecoveryRate` | float | per day | none |
| `DaysPerScenarioInput` | `DaysPerScenario` | int | days | `[1, 182500]` |
| `NumberOfScenariosInput` | `NumberOfScenarios` | int | count | `[1, 100]` |

`CarryingCapacityTier1` is the Tier 1 shared resource-pool capacity in individuals.
It feeds the FedRate term `food_density = max(0, 1 - tier1Consumption/capacity)`,
where `tier1Consumption = sum of Population*max(1, eatingAmount)`, in the
biology loop (`SimulationConfig.cs:48-59`); the inspector `[Range]` attribute is
`[100, 100000]` with default `5000`, but the input panel does not re-enforce that
range on Run. `ConditionDrainRate` and `ConditionRecoveryRate` are the
simulator-global condition (health) rates, expressed as a fraction of the gap
closed per day: drain `0.15` is roughly 8 days from full health to the death
threshold at suboptimal temperature, recovery `0.10` is roughly 10 good days to
fully recover (`SimulationConfig.cs:63-74`). Their inspector `[Range]` is
`[0.01, 1.0]` with defaults `0.15` and `0.10`.

The two integer ranges are passed to `TryReadInt` at
`SimulationInputUI.cs:298-299`. `182500` is 500 years of 365-day scenarios.
`100` caps scenario count per run. The float fields have no numeric range check on
this panel. The only validation that runs before the simulation starts is
`SimulationConfig.IsValid` inside `SimulationController.StartSimulation`
(`SimulationController.cs:90-94`). `IsValid` (`SimulationConfig.cs:146-195`) checks
exactly four conditions and returns the first failure as an error string:
`DaysPerScenario >= 1`, `NumberOfScenarios >= 1`, a non-empty `RunSpecies` species
list, and `CarryingCapacityTier1 > 0` (carrying capacity is always on as of v11.1).
It does not bound the temperature fields or the condition rates. It additionally
logs a non-fatal warning, without failing, when the summed initial Tier 1
population exceeds `CarryingCapacityTier1` (`SimulationConfig.cs:177-191`). See
`configuration-reference.md` for the full `SimulationConfig` field reference.

The species list is not edited here. Tier 1 carrying capacity
(`CarryingCapacityTier1`) is the only species-pool parameter on this panel. The
condition drain and recovery fields are the simulator-global rates; per-species
overrides live in the species editor.

### Lifecycle and defaults

`Awake` calls `StoreDefaults` (`SimulationInputUI.cs:143-147, 190-198`), which
snapshots every config field into a private `SimulationConfigDefaults` object via
`SimulationConfigDefaults.CreateFrom` (`SimulationInputUI.cs:95-118`). This
snapshot is the source for the Reset button.

`OnEnable` (`SimulationInputUI.cs:149-171`) re-attaches the Run and Reset click
listeners (removing first to avoid duplicate subscriptions), stores defaults if
not already stored, then calls `PopulateUIFromConfig` and
`ClearAllValidationColors`. `PopulateUIFromConfig` (`SimulationInputUI.cs:228-256`)
writes config values into the fields with `value.ToString(CultureInfo.InvariantCulture)`
for floats (`SetFloat`, `SimulationInputUI.cs:345-349`) and ints (`SetInt`,
`SimulationInputUI.cs:351-355`), and `SetIsOnWithoutNotify` for toggles
(`SetToggleNoNotify`, `SimulationInputUI.cs:339-343`) so populating does not fire
change events.

The float `ToString` call passes no format specifier, so it uses the default `G`
format (shortest round-trippable representation, full precision, not a fixed number
of decimals). This is an exact inverse of the read path, which uses
`float.TryParse` with `NumberStyles.Float` and `CultureInfo.InvariantCulture`
(`SimulationInputUI.cs:372-377`): a value populated from the config and immediately
read back parses to the same float. There is no `:F2` truncation on this panel,
unlike the species CSV and the editor condition fields. The displayed text is the
full value, so populate-then-Run does not alter a field's value through formatting.

`OnDisable` removes the two click listeners (`SimulationInputUI.cs:173-184`).

### Validation and start

`OnRunSimulationClicked` (`SimulationInputUI.cs:258`) runs the full validate then
apply then start sequence:

1. `ClearAllValidationColors` resets every field background to white
   (`SimulationInputUI.cs:262, 419-437`).
2. Each field is read with `TryReadFloat` or `TryReadInt`, which also recolors
   that field's background in the same call: `SetFieldColor` is called inside
   `TryReadFloat` at `SimulationInputUI.cs:379` and inside `TryReadInt` at
   `SimulationInputUI.cs:415`. Valid is `Color.white`; invalid is
   `new Color(1f, 0.80f, 0.80f, 1f)`, a pale red (`SimulationInputUI.cs:66-67`).
   `SetFieldColor` targets the field's `Image` component, falling back to
   `targetGraphic` (`SimulationInputUI.cs:439-455`). The boolean results are
   accumulated with `&=` into `allValid` (`SimulationInputUI.cs:281-299`). A field
   whose serialized reference is null returns early before any parse or recolor
   (`SimulationInputUI.cs:361-366, 387-392`): it is treated as valid, the config
   default is kept, and it stays the white set in step 1.
3. If `allValid` is false, the method logs a warning and returns without writing
   config or starting (`SimulationInputUI.cs:301-305`).
4. On success, every parsed value is written back onto the config. The two
   toggles are copied directly from `isOn` only when their references are non null
   (`SimulationInputUI.cs:311, 316`). The condition fields are likewise written
   only when their UI references are non null (`SimulationInputUI.cs:327-328`).
   The general rule is symmetric: a null UI reference means the field is skipped on
   both read and write, so its config default is kept untouched. The toggle and
   condition non-null guards are this same rule made explicit at the write site.
5. In the editor, `EditorUtility.SetDirty(config)` marks the asset dirty
   (`SimulationInputUI.cs:333-335`).
6. `simulationController.StartSimulation()` launches the run
   (`SimulationInputUI.cs:336`).

`StartSimulation` revalidates with `SimulationConfig.IsValid` and, if valid,
starts the scenario coroutine (`SimulationController.cs:74-97`). See
`run-scenario-batch.md` for the coroutine.

### Reset

`OnResetClicked` (`SimulationInputUI.cs:204`) applies the stored defaults back
onto the config via `SimulationConfigDefaults.ApplyTo`
(`SimulationInputUI.cs:120-140`), repopulates the UI, clears validation colors,
and marks the config dirty in the editor. Reset affects only `SimulationConfig`,
not `RunSpeciesList` (`SimulationInputUI.cs:202-203`).

## Species list rows: `SpeciesTierConfig` and `SpeciesUIController`

### `SpeciesTierConfig`

One `SpeciesTierConfig` (`SpeciesTierConfig.cs:15`) manages one tier panel. Its
serialized fields are the tier index (`tier`, 0 for Tier 1 prey, 1 for Tier 2
predator, `SpeciesTierConfig.cs:19`), a `RunSpeciesList` reference, a species
entry prefab, a content parent transform, and a plus button.

Note the tier numbering split documented in `data-structures.md`:
`SpeciesData.tier` is 0-based (0 prey, 1 predator, `SpeciesDatabase.cs:51`), while
the runtime `SimSpecies.Tier` is 1-based. `SpeciesTierConfig.tier` matches the
0-based `SpeciesData.tier`.

The add catalog is computed lazily by the `TierCatalog` property
(`SpeciesTierConfig.cs:39-53`). It reads `runSpeciesList.SpeciesDatabase`, or
falls back to `Resources.Load<SpeciesDatabase>("SpeciesDatabase")`, and keeps
every entry whose `tier` matches this panel. The max entries the tier can hold is
`TierCap` (`SpeciesTierConfig.cs:56`), the catalog count when non zero, else the
serialized `maxSpeciesPerTier` (default 3).

`Start` calls `SetupButtonListeners`, `PopulateFromRunSpeciesList`, and
`UpdateButtonVisibility` (`SpeciesTierConfig.cs:58-63`). `OnEnable` and
`OnDisable` subscribe and unsubscribe `HandleSpeciesDeleted` to
`SpeciesEditEvents.OnSpeciesDeleted` (`SpeciesTierConfig.cs:65-75`).

`PopulateFromRunSpeciesList` (`SpeciesTierConfig.cs:172`) walks the whole
`runSpeciesList.speciesList`, and for every entry whose `tier` matches and while
the instantiated count is below `TierCap`, instantiates a row with
`InstantiateSpeciesEntry`. It passes a per-tier visual index (1 based) and the
actual list index. The actual list index is what later edit and delete events
key on, not the visual index.

`InstantiateSpeciesEntry` (`SpeciesTierConfig.cs:205`) instantiates the prefab
under `contentParent`, moves it to just above the plus button row by sibling
index, then on the prefab's `SpeciesUIController` calls `SetRunSpeciesList` and
the `Initialize(visualIndex, listIndex, speciesData)` overload. It tracks the
GameObject and its list index in two parallel lists
(`instantiatedEntries`, `entryRunSpeciesListIndices`).

`OnPlusClicked` (`SpeciesTierConfig.cs:255`) adds the next catalog species for the
tier in catalog order, one per click, until every catalog species has been added.
It deep copies the catalog template with `CloneSpeciesData`, which delegates to
`SpeciesData.DeepCopy` (`SpeciesTierConfig.cs:383-389`, `SpeciesDatabase.cs:261`),
appends the clone to `runSpeciesList.speciesList`, instantiates a row at list
index `Count - 1`, and updates button visibility. The clone never mutates the
master `SpeciesDatabase` because `DeepCopy` round trips through `JsonUtility`
then re-attaches the `icon` reference (`SpeciesDatabase.cs:261-266`).

`OnMinusClicked` (`SpeciesTierConfig.cs:289`) exists but its button is commented
out (`SpeciesTierConfig.cs:29, 82-83`). It is still reachable through
`ClearAllSpecies`, a `[ContextMenu]` action that calls it in a loop
(`SpeciesTierConfig.cs:416-423`). It removes the last entry from
`runSpeciesList`, destroys the row, and updates visibility.

`HandleSpeciesDeleted` (`SpeciesTierConfig.cs:90`) keeps the UI consistent when a
species is deleted from the editor. Because deletion shifts every higher list
index down by one, `UpdateIndicesAfterDelete` (`SpeciesTierConfig.cs:130`)
decrements every tracked index greater than the deleted index and pushes the new
index into each affected `SpeciesUIController` with `SetRunSpeciesListIndex`. If
the deleted species belonged to this tier, its row is destroyed and visual
indices are renumbered with `UpdateVisualIndices`
(`SpeciesTierConfig.cs:157-167`). Indices in other tiers are still corrected even
when the deleted row was not in this tier (`SpeciesTierConfig.cs:118-123`).

`UpdateButtonVisibility` (`SpeciesTierConfig.cs:394`) hides the plus button's
parent once the instantiated count reaches `TierCap`.

In the editor, every mutation marks `runSpeciesList` dirty with
`EditorUtility.SetDirty` (`SpeciesTierConfig.cs:332-334, 352-354, 375-377`).

### `SpeciesUIController`

`SpeciesUIController` (`SpeciesUIController.cs:6`, marked `[ExecuteAlways]`) drives
one row. It holds an immutable catalog reference `speciesDatabase` and the mutable
`runSpeciesList`, a `ThermalGraphUI` for the row thumbnail, and display widgets:
a visual index label, an icon image, name and type texts, an editable count
field, and an edit button (`SpeciesUIController.cs:8-26`).

The critical state is `runSpeciesListIndex` (`SpeciesUIController.cs:37`), the row's
actual index in `RunSpeciesList`. It is the value sent to edit events, distinct
from the visual `#NN` display index.

Initialization has three overloads:

- `Initialize(visualIndex, listIndex, speciesData)` (`SpeciesUIController.cs:203`)
  is the one used by `SpeciesTierConfig`. It stores the list index, the direct
  `SpeciesData` reference, mirrors the species name, variant, and label into
  local fields, then calls `ApplyThermalValues` and `UpdateUIDisplay`.
- `Initialize(visualIndex, name, variant)` (`SpeciesUIController.cs:238`) and
  `Initialize(SpeciesData)` (`SpeciesUIController.cs:262`) are fallbacks that
  resolve data from the database or take a data object directly.

`ApplyThermalValues` (`SpeciesUIController.cs:365`) copies the nine thermal fields
(`optimalTempK`, `arrhenBreadth`, `arrhenLower`, `arrhenUpper`, `lowerBoundK`,
`upperBoundK`, `pmax`, `ctMinC`, `ctMaxC`) from the data onto the `ThermalGraphUI`
and forces a redraw. In play mode it calls `thermalGraphUI.OnValidate()`
directly; in the editor it defers via `EditorApplication.delayCall`
(`SpeciesUIController.cs:381-394`).

`UpdateUIDisplay` (`SpeciesUIController.cs:400`) sets the icon sprite, the name
text (`speciesLabel` if set else the `speciesName` enum, via `getName`,
`SpeciesUIController.cs:433-440`), the type text (the free text `variantLabel` if
set else `variant.ToString()`, `SpeciesUIController.cs:419-424`), and the count
field.

Inline count edit: the count field content type is forced to
`IntegerNumber` and `OnCountFieldEndEdit` is wired to `onEndEdit`
(`SpeciesUIController.cs:56-60`). `OnCountFieldEndEdit` (`SpeciesUIController.cs:156`)
parses the new value; a non negative integer is written straight to
`currentSpeciesData.count` and the asset is marked dirty in the editor, with no
Save button needed. An invalid value reverts the field text to the current count.

The edit button calls `OnEditButtonClicked` (`SpeciesUIController.cs:182`), which
fires `SpeciesEditEvents.RequestEdit(runSpeciesListIndex)` after guarding that the
index is set.

Save-event refresh: `OnEnable` subscribes `HandleSpeciesSaved` to
`SpeciesEditEvents.OnSpeciesSaved` (`SpeciesUIController.cs:63-73`).
`HandleSpeciesSaved` (`SpeciesUIController.cs:104`) checks whether the saved index
equals this row's index and, if so, calls `RefreshFromRunSpeciesList`
(`SpeciesUIController.cs:120`), which re-fetches the data object, re-syncs local
fields, and refreshes both the thumbnail and the display widgets.

## Species editor: `EditSpeciesUI`

`EditSpeciesUI` (`EditSpeciesUI.cs:18`) is the full-screen species editor. It
edits one `SpeciesData` object in place by reference, not a copy
(`EditSpeciesUI.cs:80`), and keeps a value-copy backup for the Reset button
(`EditSpeciesUI.cs:83`).

### Open flow

The panel listens on `SpeciesEditEvents.OnEditRequested`
(`EditSpeciesUI.cs:192, 269`). `HandleEditRequested(speciesIndex)`
(`EditSpeciesUI.cs:269`) stores `currentEditingIndex`, resolves
`currentEditingData = runSpeciesList.speciesList[speciesIndex]`, builds a backup
via `SpeciesDataBackup.CreateFrom` (`EditSpeciesUI.cs:125-155`), rebuilds the
organism dropdown, populates fields, clears validation, then opens the panel.
An out-of-range index nulls the working data and backup and still opens.

### Fields

The editor exposes these controls (`EditSpeciesUI.cs:31-64`):

| Section | Control | `SpeciesData` field | Validation on Save |
|---|---|---|---|
| Header | `tierField` (read only) | `tier` shown as `Tier {tier+1}` | n/a |
| Basic | `nameField` | `speciesLabel` | non empty |
| Basic | `variantDropdown` | organism preset selector | n/a |
| Basic | `variantLabelField` | `variantLabel` | trimmed, may be empty |
| Basic | `countField` | `count` | int, `>= 0` |
| Gameplay | `eatingAmountField` | `eatingAmount` | float, `>= 0` |
| Gameplay | `reproThresholdField` | `reproThreshold` | float, `[0, 1]` |
| Gameplay | `reproMultiplierField` | `reproductionMultiplier` | float, `>= 0` |
| Gameplay | `tempDeathThresholdField` | `deathThreshold` | float, `[0, 1]` |
| Gameplay | `tempDeathRateField` | `deathRate` | float, `[0, 1]` |
| Gameplay | `tempDebuff` | `TemperatureDebuff` | float, no range |
| Gameplay | `naturalDeathVarianceField` | `naturalDeathVariance` | float, `>= 0` |
| Gameplay | `naturalDeathRateField` | `naturalDeathRate` | float, `>= 0` |
| Condition | `conditionDrainRateField` | `conditionDrainRate` | blank means inherit |
| Condition | `conditionRecoveryRateField` | `conditionRecoveryRate` | blank means inherit |
| Foraging (all tiers) | `resourceFindingEfficiencyField` | `huntingEfficiency` | float, `[0, 1]` |
| Foraging (all tiers) | `resourceFindingVarianceField` | `huntingVariance` | float, `>= 0` |

The validation ranges are exactly as passed to `TryReadFloat` and `TryReadInt` at
`EditSpeciesUI.cs:584-598`. Every bracketed range is inclusive on both endpoints:
`TryReadFloat` rejects only when `value < min || value > max`
(`EditSpeciesUI.cs:782`) and `TryReadInt` likewise (`EditSpeciesUI.cs:826`), so
`[0, 1]` accepts both `0` and `1`. A `>= 0` field has no upper bound (the max
defaults to `float.MaxValue`/`int.MaxValue`, `EditSpeciesUI.cs:763, 807`). `Start`
forces the content type of each numeric field to `IntegerNumber` or `DecimalNumber`
so the on-screen keyboard restricts input (`EditSpeciesUI.cs:246-258`).

`TemperatureDebuff` (the `tempDebuff` field, validated with no range) is a
per-species temperature offset in degrees Celsius. At run time it is copied into
`SimSpecies.TemperatureDebuff` (`SimSpecies.cs:71`) and added to the experienced
temperature at the start of `CalculatePerformance`:
`temperatureCelsius += TemperatureDebuff` (`SimSpecies.cs:97`). A positive value
shifts the species' experienced temperature warmer, a negative value cooler, so it
moves the whole thermal response along the temperature axis without changing its
shape. The default is `0` (`SpeciesDatabase.cs:56`, no offset). The field has no
range because any real offset is valid; typical values are small, within a few
degrees of zero. The inline comment at `SpeciesDatabase.cs:56` calls it a
"performance debuff", but the actual runtime effect is the additive Celsius offset
shown above.

The foraging section applies to all tiers. It has two inputs,
`resourceFindingEfficiencyField` and `resourceFindingVarianceField`, shown for every
tier with plain scene labels and no per-tier label or visibility logic
(`EditSpeciesUI.cs:54-60, 452-458`). Tier 1 forages the shared resource pool, Tier 2
and a future Tier 3 hunt the tier below; the same two fields edit
`SpeciesData.huntingEfficiency` and `huntingVariance` in every case. `PopulateFields`
always populates them (`EditSpeciesUI.cs:454-458`) and `SaveData` always validates and
writes them, efficiency in `[0, 1]` and variance `>= 0` (`EditSpeciesUI.cs:596-598,
638-640`). The earlier Tier-2-only hunting section and its `showHunting`/`tier >= 1`
gate were removed. See `bulk-system.md` and `run-scenario-batch.md` for the Tier 2 gate
(`EcosystemSimulator.Tier2Enabled` defaults false).

### Condition rate inherit sentinel

The per-species condition rates use a negative sentinel meaning inherit the
simulator-global rate. The canonical sentinel written on Save is exactly `-1f`.
On display, any stored value `< 0` is treated as inherit: `PopulateFields` shows
an empty field when the stored value is `< 0`, else the value to three decimals
(`EditSpeciesUI.cs:420-426`). On Save, `ParseRateOrInherit`
(`EditSpeciesUI.cs:803-811`) returns exactly `-1f` for a blank field or any
unparseable or negative entry, and returns the parsed value only when it parses
and is `>= 0`. A value of `0` is therefore a valid explicit rate, not inherit.
Save writes both rates via `ParseRateOrInherit` (`EditSpeciesUI.cs:642-643`).
Note the normalization asymmetry: a non-`-1` negative value such as `-0.3` already
stored in the data shows blank in the field, and is rewritten to `-1f` only if the
user re-saves (the blank field parses to `-1f`); if the panel is closed without
saving, the original `-0.3` is preserved unchanged. See `biology-and-formulas.md`
for how the `-1` sentinel resolves to the global rate at run time.

### Organism dropdown (preset selector)

The dropdown is a data driven projection of the catalog plus a trailing literal
`Custom` option (`CUSTOM_OPTION`, `EditSpeciesUI.cs:73`). `PopulateOrganismDropdown`
(`EditSpeciesUI.cs:308`) clears a parallel index list `_organismCatalogIndices`,
then for each Tier 1 entry in `originalDatabase.speciesList` (entries with
`tier != 0` are skipped, `EditSpeciesUI.cs:318`) adds the entry `DisplayName` as an
option and pushes its catalog index. It then appends `Custom` and the sentinel
index `-1`. The dropdown is filled with `AddOptions` while
`_suppressOrganismCallback` is set so the load does not fire the change handler.

`FindOrganismOption` (`EditSpeciesUI.cs:335`) returns the dropdown index whose
catalog entry matches the row by both `speciesName` and `variant`, else the last
option, `Custom`.

`OnOrganismSelected(dropdownIdx)` (`EditSpeciesUI.cs:356-376`) is the change
handler. It mutates the live `SpeciesData` reference immediately on dropdown
change, not on Save. For a real catalog index it copies the catalog template's
parameters into the working row with `CopyFrom` while preserving the row's `count`
and `index` (`EditSpeciesUI.cs:362-369`). For the `Custom` sentinel it sets
`variant = SpeciesVariant.Custom` and `speciesName = SpeciesName.Custom`
(`EditSpeciesUI.cs:371-374`). It then repopulates fields. Because this copy is
immediate and `Close` does not revert it, a dropdown change persists in the
`RunSpeciesList` entry even if the user closes without saving. Selecting a preset
is a one-time copy; the dropdown never re-derives parameters after the copy.

### Thermal parameters

Thermal parameters are not edited as raw fields. `PopulateFields` ends with
`LoadThermalParameters` (`EditSpeciesUI.cs:464-465, 471-489`), which calls
`thermalController.LoadFromSpeciesData(currentEditingData)`. Save calls
`thermalController.SaveToSpeciesData(currentEditingData)` after the gameplay
fields are written (`EditSpeciesUI.cs:652-657`). The thermal controller is
described below.

### Buttons

The five buttons are wired in `OnEnable` (remove then add) and removed in
`OnDisable` (`EditSpeciesUI.cs:201-238`):

- Close and Cancel both call `Close` (`EditSpeciesUI.cs:510, 536`). `Close`
  (`EditSpeciesUI.cs:510-531`) nulls `currentEditingIndex`, `currentEditingData`,
  and `backupData`, clears the graph highlight via
  `thermalController.ClearActiveHighlight`, clears validation colors, hides the
  panel, and fires `SpeciesEditEvents.NotifyEditClosed`. It does not restore the
  backup. The editor edits the live `SpeciesData` object by reference, and the
  gameplay, condition, hunting, and thermal fields are written only inside
  `SaveData`, so closing without saving leaves those fields as they were when the
  panel opened. There is one exception: a prior organism-dropdown change already
  mutated the live object during editing. `OnOrganismSelected`
  (`EditSpeciesUI.cs:356-376`) calls `currentEditingData.CopyFrom(...)` the moment
  the dropdown value changes (`EditSpeciesUI.cs:366`), not on Save, so the copied
  catalog parameters persist in the `RunSpeciesList` entry even if the user then
  closes without saving. The inherit-rate parse (`ParseRateOrInherit`,
  `EditSpeciesUI.cs:642-643`) is the only field write that happens solely on Save.
- Save Data calls `SaveData` (`EditSpeciesUI.cs:546`). It clears colors, validates
  every field into temporaries, and if any check fails logs a warning and returns
  without writing (`EditSpeciesUI.cs:559-612`). On success it writes
  `speciesLabel`, `variantLabel`, the gameplay fields, the two condition rates,
  the hunting fields when Tier 2+, then the thermal parameters, marks
  `runSpeciesList` dirty in the editor, fires
  `SpeciesEditEvents.NotifySpeciesSaved(currentEditingIndex)`, and closes
  (`EditSpeciesUI.cs:614-670`).
- Delete calls `Delete` (`EditSpeciesUI.cs:676`). It removes the entry from
  `runSpeciesList.speciesList`, marks dirty in the editor, fires
  `SpeciesEditEvents.NotifySpeciesDeleted(deletedIndex)`, and closes. The delete
  event is what triggers index re-mapping in every `SpeciesTierConfig`.
- Reset calls `Reset` (`EditSpeciesUI.cs:707`), restoring every field from the
  open-time backup with `SpeciesDataBackup.RestoreTo`
  (`EditSpeciesUI.cs:160-187`), then repopulating fields.

A separate `ResetToOriginalDatabase` (`EditSpeciesUI.cs:730`) performs a factory
reset by `CopyFrom` of the matching `originalDatabase` entry, preserving the row
index, and refreshes the backup. It is a public method with no wired button in
this script.

Validation colors and helpers mirror the input UI: invalid is the pale red
`new Color(1f, 0.80f, 0.80f, 1f)`, valid is white (`EditSpeciesUI.cs:76-77`),
applied to the field `Image` then `targetGraphic` (`EditSpeciesUI.cs:846-863`).

`HasUnsavedChanges` (`EditSpeciesUI.cs:892`) compares a subset of fields and the
live thermal controller values against the backup; it is a public helper not used
inside this file.

## Thermal curve editor

The thermal editor is three cooperating components: a controller, a set of
sliders, and a graph renderer. The same thermal performance formula is
reimplemented three times: in the controller (`CalculatePerformanceAtTemp`,
`ThermalParameterController.cs:534-578`), in the editable graph
(`ThermalGraphEditor.CalculatePerformance`), and in the read only thumbnail
(`ThermalGraphUI.UpdateGraph`). All three are previews only and never feed the
simulation; the authoritative version is the one in `biology-and-formulas.md`,
which mirrors `SimSpecies.CalculatePerformance` (`SimSpecies.cs:95`). The three
preview implementations are expected to produce the same curve as each other for
the same nine parameters. The exact formula they implement is given below so a
reimplementer can reproduce any of them without cross-referencing another document.

The performance `P(T)` at a temperature `T_C` in Celsius, given the nine
parameters, is (`ThermalParameterController.cs:534-578`):

```
constants:
  KELVIN_OFFSET        = 273.15
  LETHAL_TRANSITION_WIDTH = 2.0   (degrees Celsius)

inputs (display/internal units):
  T_C      experienced temperature, Celsius
  ctMinC   critical thermal minimum, Celsius
  ctMaxC   critical thermal maximum, Celsius
  OT       optimal temperature, Kelvin   (currentOptimalTemp)
  LB       lower bound, Kelvin           (currentLowerBound)
  UB       upper bound, Kelvin           (currentUpperBound)
  B        Arrhenius breadth coefficient (currentArrhenBreadth)
  L        Arrhenius lower coefficient   (currentArrhenLower)
  U        Arrhenius upper coefficient   (currentArrhenUpper)
  Pmax     peak height, 0..1             (currentPmax)

1. Cosine lethal fade, fade in [0,1], starts at 1.0:
   tw = min(LETHAL_TRANSITION_WIDTH, (ctMaxC - ctMinC) / 2)
   if T_C <= ctMinC:                 fade = 0
   else if T_C < ctMinC + tw:        fade = 0.5 * (1 + cos(PI * (ctMinC + tw - T_C) / tw))
   if T_C >= ctMaxC:                 fade = 0
   else if T_C > ctMaxC - tw:        fade *= 0.5 * (1 + cos(PI * (T_C - (ctMaxC - tw)) / tw))
   if fade <= 0: return 0            (skip the Arrhenius term entirely)
   The lower-edge branch rises 0 -> 1 as T_C goes from ctMinC to ctMinC + tw.
   The upper-edge branch falls 1 -> 0 as T_C goes from ctMaxC - tw to ctMaxC, and
   multiplies whatever the lower branch produced.

2. Convert to Kelvin: T = T_C + KELVIN_OFFSET.

3. Arrhenius ratio (Sharpe-Schoolfield style), reference is OT:
   numerator   = exp(B/OT - B/T) * (1 + exp(L/OT - L/LB) + exp(U/UB - U/OT))
   denominator = 1 + exp(L/T - L/LB) + exp(U/UB - U/T)
   ratio       = numerator / denominator      (denominator == 0 -> return 0)

4. Final: P = clamp01(ratio) * fade * Pmax
```

The operation order is fixed: the fade is computed first and short-circuits to 0
before the Arrhenius term; the ratio is clamped to `[0, 1]` first, then multiplied
by the fade, then by `Pmax`. The clamp applies to the bare Arrhenius ratio only,
not to the product. Step 0 of the controller and graph versions also clamps broken
inputs to safe minimums before the ratio is evaluated, described under
ThermalParameterController below.

### `ThermalParameterController`

`ThermalParameterController` (`ThermalParameterController.cs:8`) owns the current
thermal values, wires the sliders to a `ThermalGraphEditor`, computes Area Under
Curve (AUC), and implements proportional AUC scaling.

Serialized references are the graph editor, nine parameter sliders
(`optimalTempSlider`, `lowerBoundSlider`, `upperBoundSlider`,
`arrhenBreadthSlider`, `arrhenLowerSlider`, `arrhenUpperSlider`, `pmaxSlider`,
`ctMinSlider`, `ctMaxSlider`), an optional `aucSlider`, per-slider display ranges,
AUC settings, and a `UnityEvent OnAnyParameterChanged`
(`ThermalParameterController.cs:10-57`).

Default slider ranges (in display units, Celsius for temperatures):

| Slider | Range field | Default range | Units |
|---|---|---|---|
| Optimal temp | `optimalTempRange` | `[-5, 45]` | Celsius |
| Lower bound | `lowerBoundRange` | `[-10, 40]` | Celsius |
| Upper bound | `upperBoundRange` | `[0, 50]` | Celsius |
| Pmax | `pmaxRange` | `[0, 1]` | dimensionless |
| CTmin | `ctMinRange` | `[-50, 50]` | Celsius |
| CTmax | `ctMaxRange` | `[-50, 100]` | Celsius |
| Arrhenius breadth | `arrhenBreadthRange` | `[1000, 15000]` | raw coefficient |
| Arrhenius lower | `arrhenLowerRange` | `[3000, 25000]` | raw coefficient |
| Arrhenius upper | `arrhenUpperRange` | `[5000, 35000]` | raw coefficient |
| AUC | `aucRange` | `[2, 35]` | area units (computed AUC span) |

(`ThermalParameterController.cs:32-53`.)

The AUC slider range `aucRange` `[2, 35]` and the AUC integration window are two
different things. `aucRange` is the slider's value range in area units: it is the
expected span of the computed AUC value, and is what `aucSlider.Initialize` uses
for the slider's min and max (`ThermalParameterController.cs:155-156`). The
integration domain is separate: `CalculateAUC` integrates performance over
`[aucTempMin, aucTempMax]`, default `[0, 40]` Celsius (`ThermalParameterController.cs:51-53`).
The slider does not clamp the displayed AUC into `[2, 35]` itself; the underlying
`ThermalParameterSlider.SetDisplayValue` clamps every value to the slider's
`[minValue, maxValue]` (`ThermalParameterSlider.cs:195`), so when `CalculateAUC`
returns a value outside `[2, 35]` the slider shows the clamped endpoint, while the
controller's internal `currentAUC` keeps the true unclamped value. The `[2, 35]`
bounds are a UI display choice, not a constraint on the AUC math.

Lifecycle: initialization is lazy through `EnsureInitialized`
(`ThermalParameterController.cs:104`), called from `Start` or from the first
`LoadFromSpeciesData` or `LoadValues`, whichever runs first, so an API load before
`Start` is not overwritten. It runs `InitializeSliders` and `SubscribeToSliders`
once. `InitializeSliders` (`ThermalParameterController.cs:121`) calls each
slider's `Initialize(min, max, default, isTemperature, decimals)` with the ranges
above. `OnDestroy` unsubscribes.

Each slider has its own change handler (`ThermalParameterController.cs:233-317`)
that, while `isScalingFromAUC` is false, writes the new value into the matching
`current*` field, highlights the corresponding parameter on the graph through
`SetActiveParameter`, calls `UpdateGraphAndAUC`, then `StoreBaseline`.
`UpdateGraphAndAUC` (`ThermalParameterController.cs:612`) pushes all current
values to the graph via `UpdateGraph`, recomputes AUC with `CalculateAUC`, updates
the AUC slider, and fires `OnAnyParameterChanged` unless loading.

AUC: `CalculateAUC` (`ThermalParameterController.cs:511-529`) integrates
performance over `[aucTempMin, aucTempMax]` (default 0 to 40 Celsius) using the
trapezoid rule with `aucSamples` intervals (default 100). The integrand is
`CalculatePerformanceAtTemp` (`ThermalParameterController.cs:534-578`), which is the
performance formula given at the top of this section: cosine lethal fade, then the
Arrhenius ratio on Kelvin, clamp to `[0, 1]`, times fade times `currentPmax`.

Before evaluating the Arrhenius ratio, `CalculatePerformanceAtTemp` clamps broken
inputs to fixed safe minimums so the ratio cannot divide by zero or take a
non-physical value (`ThermalParameterController.cs:561-568`). The guard fires
per-variable when the value is `<= 0`, and substitutes: `T = 273.15` if `T <= 0`,
`OT = 293.15` if `OT <= 0`, `LB = 273.15` if `LB <= 0`, `UB = 313.15` if
`UB <= 0`, `B = 1000` if `B <= 0`, `L = 3000` if `L <= 0`, `U = 5000` if `U <= 0`
(all Kelvin for temperatures, raw for coefficients). These substitutions affect
only the local computation; they do not write back to the `current*` fields or the
sliders. If `denominator == 0` after the guards, the function returns `0`
(`ThermalParameterController.cs:575`).

Proportional AUC scaling: moving the AUC slider calls `OnAUCSliderChanged`
(`ThermalParameterController.cs:345`), which calls `ApplyProportionalScaling`
(`ThermalParameterController.cs:357-436`). It computes
`scaleFactor = targetAUC / baselineAUC`, then clamps it to `[0.05, 10]`
(`ThermalParameterController.cs:372-375`). Exactly five of the nine parameters
move under scaling, all computed from the stored baseline (not the current value)
so repeated slider moves do not accumulate error:

```
anchor (fixed): OT = baselineOptimalTemp                 (optimalTemp never moves)

lowerBound:  newLB = OT - (OT - baselineLowerBound)   * scaleFactor
upperBound:  newUB = OT + (baselineUpperBound - OT)   * scaleFactor
breadth:     newBreadth = baselineArrhenBreadth        * scaleFactor
arrhenLower: newArrhenLower = baselineArrhenLower      * scaleFactor
arrhenUpper: newArrhenUpper = baselineArrhenUpper      * scaleFactor
```

(`ThermalParameterController.cs:380-392`.) The bounds keep their side of the
optimal: the lower bound is `OT` minus its baseline distance below optimal scaled,
the upper bound is `OT` plus its baseline distance above optimal scaled, so larger
`scaleFactor` widens the curve symmetrically around the fixed peak. Each result is
then clamped to its slider range (the bounds are converted to Celsius, clamped to
`lowerBoundRange`/`upperBoundRange`, and converted back to Kelvin;
the three coefficients are clamped to their `arrhen*Range`,
`ThermalParameterController.cs:394-406`). The four parameters NOT scaled hold
fixed: `optimalTemp` is the anchor, and `Pmax`, `ctMinC`, and `ctMaxC` are not
touched by `ApplyProportionalScaling` at all (they keep their current values).

After scaling, the method updates the internal values and the five moved sliders,
redraws, recomputes the real AUC with `CalculateAUC`, and syncs the AUC slider to
the achievable AUC with `SyncAUCSliderToActual` so the slider cannot show an
impossible value (`ThermalParameterController.cs:408-426`).

Degenerate baseline: the guard is `baselineAUC <= 0.001f`
(`ThermalParameterController.cs:360`). When it trips, the method first recomputes
`baselineAUC = CalculateAUC()`; if that is still `<= 0.001f` it calls
`ResetToSafeDefaults` and returns (`ThermalParameterController.cs:360-370`).
`ResetToSafeDefaults` (`ThermalParameterController.cs:441-470`) overwrites the
nine `current*` fields with fixed values: `currentOptimalTemp = 293.15` (20 C),
`currentLowerBound = 285.15` (12 C), `currentUpperBound = 301.15` (28 C),
`currentArrhenBreadth = 5000`, `currentArrhenLower = 10000`,
`currentArrhenUpper = 20000`, `currentPmax = 1.0`, `currentCTminC = -5.0`,
`currentCTmaxC = 50.0`. It then pushes those to the sliders, redraws, recomputes
AUC, stores the baseline, and syncs the AUC slider.

Several boolean guards prevent feedback loops: `isLoading`, `isScalingFromAUC`,
`isUpdatingAUCSlider` (`ThermalParameterController.cs:90-93`).

Load: `LoadFromSpeciesData` (`ThermalParameterController.cs:656`) sets
`isLoading`, copies the nine thermal fields from `SpeciesData` into both the
`current*` fields and the sliders (temperatures with `isInternalUnits: true`,
coefficients and Pmax and CT limits with `false`), redraws, computes AUC, stores
the baseline, updates the AUC slider, clears the graph highlight, then clears
`isLoading`. `LoadValues` (`ThermalParameterController.cs:724`) is the same for the
six core parameters passed directly in Kelvin and raw units.

Save: `SaveToSpeciesData` (`ThermalParameterController.cs:782`) writes the nine
`current*` thermal fields back onto the `SpeciesData`. AUC is derived and not
saved. `GetCurrentValues` (`ThermalParameterController.cs:806`) returns a
`ThermalParameters` struct (`ThermalParameterController.cs:874-886`) holding the
nine fields.

### `ThermalParameterSlider`

`ThermalParameterSlider` (`ThermalParameterSlider.cs:12`) is one slider plus a TMP
input field plus an optional unit label. Its key trait is dual unit handling: when
`isTemperature` is true it displays Celsius but stores and returns Kelvin; when
false it displays and returns the raw value (`ThermalParameterSlider.cs:20-21`).

Public `Value` returns the internal value (`CelsiusToKelvin(display)` for
temperatures, raw otherwise) and `DisplayValue` returns the display value
(`ThermalParameterSlider.cs:47-65`). Conversions are `CelsiusToKelvin`
(`ThermalParameterSlider.cs:271-274`) and `KelvinToCelsius`
(`ThermalParameterSlider.cs:279-282`), both using `KELVIN_OFFSET = 273.15`
(`ThermalParameterSlider.cs:40`).

`Awake` (`ThermalParameterSlider.cs:67`) sets slider min and max, wires
`OnSliderChanged` to the slider and `OnInputFieldChanged` to the field's
`onEndEdit`, and forces the field content type to `DecimalNumber`. `Start`
(`ThermalParameterSlider.cs:85-90`) sets the default value only if no value was
set first (`hasBeenSet` guard). `hasBeenSet` is set to true inside
`SetDisplayValue` (`ThermalParameterSlider.cs:192`), which is the single write
path: `SetValue`, `DisplayValue`, `Value`, `Initialize`, and `ResetToDefault` all
route through it. Any external set before `Start` runs (for example the
controller's `InitializeSliders` calling `slider.Initialize(...)`, or a later
`SetValue` from `LoadFromSpeciesData`) therefore sets `hasBeenSet` and suppresses
the `Start` default, so an API-provided value survives. The serialized
`defaultValue` is only applied when no caller touched the slider before `Start`.

`SetDisplayValue` (`ThermalParameterSlider.cs:188`) is the single sync point: it
guards reentrancy with `isUpdating`, clamps the display value to
`[minValue, maxValue]`, updates the slider with `SetValueWithoutNotify` and the
field with `SetTextWithoutNotify` formatted to `decimalPlaces`, then fires
`OnValueChanged` with the internal value. `OnSliderChanged` and
`OnInputFieldChanged` both route through `SetDisplayValue`; an unparseable field
entry reverts the field text. `SetValue(value, isInternalUnits)`
(`ThermalParameterSlider.cs:168`) converts Kelvin to Celsius first when the value
is internal and this is a temperature.

### `ThermalGraphEditor`

`ThermalGraphEditor` (`ThermalGraphEditor.cs:11`, `[ExecuteAlways]`,
`[RequireComponent(typeof(RawImage))]`) renders the editable curve into a
`Texture2D` it assigns to its `RawImage`. Default texture is 512 by 256
(`ThermalGraphEditor.cs:28-29`), higher resolution than the thumbnail.

Public API used by the controller:

- `SetParameters(optTemp, breadth, lower, upper, lowerB, upperB, pmax, ctMinC,
  ctMaxC)` (`ThermalGraphEditor.cs:123`) sets all nine fields, recomputes the
  display range, and redraws.
- `SetActiveParameter(EditingParameter)` and `ClearActiveParameter`
  (`ThermalGraphEditor.cs:142-155`) control which parameter is highlighted.
  `EditingParameter` is `None, OptimalTemp, LowerBound, UpperBound, ArrhenBreadth,
  ArrhenLower, ArrhenUpper` (`ThermalGraphEditor.cs:49-58`).

`UpdateGraph` (`ThermalGraphEditor.cs:194`) clears to background, draws axis
labels and ticks via `PixelFont` and `AxisHelper`, draws the grid, computes the
per-pixel performance array with `CalculatePerformance`, draws the curve with a
glow, draws the optimal, lower, and upper bound lines, draws the active parameter
marker, then applies the pixels to the texture. The graph area is inset by fixed
margins (`MarginLeft 38, MarginBottom 22, MarginTop 4, MarginRight 4`,
`ThermalGraphEditor.cs:65-68`) to leave room for axis labels.

`CalculatePerformance` (`ThermalGraphEditor.cs:327`) is the same fade-then-
Arrhenius-then-clamp-times-Pmax shape as the controller, on Kelvin with
`KELVIN_OFFSET = 273.15` and `LETHAL_TRANSITION_WIDTH = 2.0`. `UpdateDisplayRange`
(`ThermalGraphEditor.cs:568`) auto zooms the x axis to `[ctMinC - 5, ctMaxC + 5]`
with a 20 degree minimum width. Public helpers include `CalculateAreaUnderCurve`
(trapezoid over the rendered values), `GetPeakTemperatureCelsius`, and
`GetPeakPerformance` (`ThermalGraphEditor.cs:675-708`).

### `ThermalGraphUI`

`ThermalGraphUI` (`ThermalGraphUI.cs:6`, `[ExecuteAlways]`,
`[RequireComponent(typeof(RawImage))]`) is the read only thumbnail on each species
row. Default texture is 256 by 128 (`ThermalGraphUI.cs:20-21`). It exposes the nine
thermal fields as public floats so `SpeciesUIController.ApplyThermalValues` can set
them directly. `OnValidate` (`ThermalGraphUI.cs:57`) redraws: in the editor it
defers with `EditorApplication.delayCall`, in play mode it redraws immediately.
`UpdateGraph` (`ThermalGraphUI.cs:98`) computes the same lethal-fade Arrhenius
performance and draws the curve plus min and max temperature labels at the bottom
corners. It has no editing, highlighting, axis ticks, or grid.

### Shared rendering helpers

`PixelFont` (`PixelFont.cs:8`) is a 5 by 7 bitmap font drawn straight into a
`Color[]` pixel buffer. It defines glyphs for digits, a small set of letters used
in axis labels, and punctuation (`PixelFont.cs:15-49`), and provides
`DrawString`, `DrawStringCentered`, `DrawStringRightAligned`, and
`DrawStringVertical` plus `MeasureString` (`PixelFont.cs:51-152`).

`AxisHelper` (`AxisHelper.cs:7`) computes clean tick positions with
`ComputeNiceTicks(rangeMin, rangeMax, targetCount)`
(`AxisHelper.cs:13`), snapping the interval to a 1, 2, 5, or 10 multiple of the
order of magnitude, and formats labels with `FormatTemp` and
`FormatPerformance` (`AxisHelper.cs:56-70`).

## Results screen: `ResultsScreenUI`

`ResultsScreenUI` (`ResultsScreenUI.cs:19`) shows run progress, then aggregate
results, the per-scenario list, and the download buttons. It serves both the
standard single-config run (driven by `SimulationController`) and the bulk run
(driven by `BulkSimulationController`). See `run-scenario-batch.md` and
`bulk-system.md` for the two drivers.

### Layout and state

Serialized sections (`ResultsScreenUI.cs:21-67`):

- Header: a timestamp text, a config label text, and a Download Config button.
- Progress: a progress section, a `Slider` progress bar, a progress text, and a
  Cancel button.
- Pause/Resume: a single toggle button with a label and a recolor target image.
- Results: a results section, a quick stats text, a Download Aggregate button, and
  a Download All ZIP button.
- Scenario list: a content transform and a scenario row prefab.
- Footer: a Close button.
- Editor setting `openFilesAfterSave` (default true).

Internal state includes `_currentResults` (the `AggregateResults` to render),
flags `_isRunning`, `_cancelRequested`, `_isPaused`, the two bulk readiness flags
`_bulkProgressiveReady` and `_bulkServerReady`, the instantiated row list, and the
animated-dots coroutine state (`ResultsScreenUI.cs:69-81`).

Public surface used by the drivers:

- `IsPaused` property (`ResultsScreenUI.cs:88`), polled by the bulk loop.
- `OnCancelRequested`, `OnCloseRequested`, `OnPauseToggled` actions
  (`ResultsScreenUI.cs:84-90`).
- `Show`, `Hide`, `UpdateProgress`, `UpdateBulkProgress`, `OnScenarioCompleted`,
  `DisplayResults`, `DisplayBulkResults`, `IsCancelRequested`.

`Awake` (`ResultsScreenUI.cs:92`) wires the six buttons.

### Progress mode

`Show` (`ResultsScreenUI.cs:117`) activates the panel, enters progress mode,
resets cancel and pause state, clears the scenario list and prior results, resets
progress to `Initializing...`, re-enables the cancel and pause buttons (re-adding
the pause click listener idempotently), and re-activates all three download
button GameObjects in case a prior bulk run hid them.

`SetProgressMode(showProgress)` (`ResultsScreenUI.cs:181`) toggles the progress
and results sections inversely, keeps Download Config always interactable, and
enables Download Aggregate and Download All ZIP only when results are present
(not in progress).

Two progress updaters:

- `UpdateProgress(currentScenario, totalScenarios, statusMessage)`
  (`ResultsScreenUI.cs:204`) drives the bar by scenario count and writes
  `Running scenario N of M...` or the explicit message. Used by the standard run.
- `UpdateBulkProgress(text, progress01)` (`ResultsScreenUI.cs:231-248`) drives the
  bar with an explicit `0..1` fraction, trims trailing dots from the text, and
  starts an animated dots suffix that cycles `.`, `..`, `...` every 0.4 seconds of
  unscaled time via the `AnimateDots` coroutine (`ResultsScreenUI.cs:255-271`). The
  coroutine runs while `_isRunning` and advances on `yield return null`, so it
  animates between the synchronous per-scenario computations. `StopDotsAnimation`
  (`ResultsScreenUI.cs:276-283`) ends it.

  Lifecycle preconditions: `_isRunning` is set true in `Show`
  (`ResultsScreenUI.cs:125`), which both drivers call before they start streaming
  progress, so the loop condition is already true the first time
  `UpdateBulkProgress` runs. `UpdateBulkProgress` guards against starting a second
  coroutine: it only calls `StartCoroutine(AnimateDots())` when `_dotsCoroutine`
  is null (`ResultsScreenUI.cs:246-247`), and the coroutine nulls `_dotsCoroutine`
  when it ends (`ResultsScreenUI.cs:270`), so repeated `UpdateBulkProgress` calls
  reuse the single running coroutine rather than spawning duplicates. The
  coroutine stops on either path that clears `_isRunning` (its own loop condition
  fails) or an explicit `StopDotsAnimation`, which is called by `Show`, `Hide`,
  `DisplayResults`, and `DisplayBulkResults`.

`OnScenarioCompleted(result)` (`ResultsScreenUI.cs:288`) is a hook that currently
does nothing; the full list is populated on completion.

### Pause/resume toggle

`OnPauseResumeClicked` (`ResultsScreenUI.cs:467`) flips `_isPaused`, repaints the
button, and fires `OnPauseToggled(_isPaused)`. `UpdatePauseResumeVisual`
(`ResultsScreenUI.cs:474`) sets the label to the resume or pause string, picks the
green paused color `new Color(0.30f, 0.78f, 0.36f, 1f)` or the gray running color
`new Color(0.55f, 0.55f, 0.55f, 1f)`, resolves the image to recolor (explicit
target, else `targetGraphic`, else the button's `Image`), forces the button
transition to `None` so a color tint cannot override the flat color, and sets the
image color (`ResultsScreenUI.cs:47-48, 474-493`).

Two drivers consume the pause differently. The bulk loop polls `IsPaused` in
`WaitWhilePaused`, spinning on `yield return null` while paused so Resume and
Cancel stay clickable (`BulkSimulationController.cs:166, 403-407`). The standard
`SimulationController` exposes `PauseSimulation`, `ResumeSimulation`, and a
`RunControl` pause signal (`SimulationController.cs:393-400`) but does not
subscribe to `OnPauseToggled` in its own code, so the toggle is the bulk run's
control surface. See `run-scenario-batch.md` for `RunControl`.

### Cancel and close

`OnCancelClicked` (`ResultsScreenUI.cs:446`) sets `_cancelRequested`, writes
`Cancelling...`, disables the cancel and pause buttons, and fires
`OnCancelRequested`. `IsCancelRequested` (`ResultsScreenUI.cs:439`) exposes the
flag. The standard run reads it through its own `_cancelRequested` set in
`OnCancelRequested` (`SimulationController.cs:387-391`); the bulk run sets its own
flag the same way (`BulkSimulationController.cs:409-412`).

`OnCloseClicked` (`ResultsScreenUI.cs:495`) hides the panel and fires
`OnCloseRequested`. The bulk driver, on close while running, sets cancel and
resets the upload handler to idle (`BulkSimulationController.cs:414-423`).

### Results mode (standard run)

`DisplayResults(results)` (`ResultsScreenUI.cs:345-367`) stores `_currentResults`,
stops the dots, switches to results mode, sets the header timestamp from
`results.CompletedAt`, the config label from `results.GetConfigLine()` (for
example `3650 days x 10 scenarios`, `ScenarioResult.cs:558-561`), the quick stats
from `results.GetQuickStatsLine()` (survived and crashed counts and average final
Tier 1 population, `ScenarioResult.cs:549-553`), and populates the scenario list.

The timestamp is captured once, at run completion, into `results.CompletedAt`
(a `DateTime`). Two different format strings are applied to that single value, so
the header and the filenames always describe the same instant. The header uses
`{results.CompletedAt:MMM dd, yyyy 'at' h:mm tt}` for human display, for example
`Jun 08, 2026 at 3:07 PM` (`ResultsScreenUI.cs:356`). Every download filename uses
`{_currentResults.CompletedAt:yyyy-MM-dd_HH-mm-ss}`, for example
`2026-06-08_15-07-42` (`ResultsScreenUI.cs:514, 538, 555, 743`). The filename
format uses hyphens for the time separator, not colons, so it is filesystem-safe on
Windows and macOS. Because all filename timestamps read the same `CompletedAt`,
downloading two different scenarios from the same run produces identical timestamp
stems. The one exception is the Download Config button before a run has completed:
with no `_currentResults`, it generates a fresh `System.DateTime.Now` at click time
(`ResultsScreenUI.cs:520`), so a pre-run config download carries the click time
rather than a completion time.

`PopulateScenarioList(scenarios)` (`ResultsScreenUI.cs:372`) clears prior rows then
instantiates `scenarioRowPrefab` under `scenarioListContent` for each scenario,
calling `ScenarioRowUI.Setup(scenario, OnDownloadScenarioClicked)`. If the prefab
lacks `ScenarioRowUI` it falls back to `SetupRowManually`, which finds child
transforms named `StatusIcon`, `SummaryText`, and `DownloadButton` and wires them
(`ResultsScreenUI.cs:404-421`). `ClearScenarioList` (`ResultsScreenUI.cs:426`)
destroys all tracked rows.

### Results mode (bulk run)

`DisplayBulkResults(totalBatches, totalScenarios, serverUpload)`
(`ResultsScreenUI.cs:302-340`) stops the dots, sets `_bulkServerReady = serverUpload`
and `_bulkProgressiveReady = !serverUpload` (`ResultsScreenUI.cs:306-307`),
switches to results mode, sets a completed timestamp and a `Bulk run ...` label and
quick stats, hides Download Config and Download Aggregate (multiple configs and no
single aggregate apply to bulk), shows only Download All ZIP, and clears the
scenario list (bulk has no per-scenario rows). The bulk driver calls this last
(`BulkSimulationController.cs:395`).

The two flags are exact complements, so after a bulk run exactly one of
`_bulkServerReady` and `_bulkProgressiveReady` is true. This is the invariant that
makes the download dispatcher unambiguous: `OnDownloadAllZipClicked` checks
`_bulkServerReady` first, then `_bulkProgressiveReady`, then falls through to the
standard single-run path (`ResultsScreenUI.cs:560-583`). Because one bulk flag is
always set after `DisplayBulkResults`, the single-run fallthrough (path 3) is
unreachable on a bulk run. Path 3 has its own precondition: it requires a non-null
`_currentResults` from a standard run and returns immediately if `_currentResults`
is null (`ResultsScreenUI.cs:580`). `DisplayResults` is the only method that sets
`_currentResults`, and it leaves both bulk flags false (they are reset to false in
`Show`, `ResultsScreenUI.cs:130-131`), so path 3 fires only for a standard run and
only when results are present.

## Scenario rows: `ScenarioRowUI`

`ScenarioRowUI` (`ScenarioRowUI.cs:20`) renders one scenario result. Serialized
fields are a status icon `Graphic` (works for `Image` or `SVGImage`), a summary
text, a download button, survived and crashed colors, and optional survived and
crashed sprites (`ScenarioRowUI.cs:22-35`).

`Setup(scenario, onDownloadClicked)` (`ScenarioRowUI.cs:46`) stores the scenario
index and the callback, colors the status icon green
`new Color(0.298f, 0.686f, 0.314f)` when survived or red
`new Color(0.957f, 0.263f, 0.212f)` when `scenario.Crashed`
(`ScenarioRowUI.cs:29-30, 52-69`), swaps the sprite if the icon is a plain `Image`
and a matching sprite is set, sets the summary from `scenario.GetSummaryLine()`
(for example `Scenario 1: Pop=2,450` or `Scenario 1: Crashed Day 73 (Tier 1)`,
`ScenarioResult.cs:71-78`), and wires the download button to `OnDownloadClicked`
after clearing prior listeners. `OnDownloadClicked` (`ScenarioRowUI.cs:119`)
invokes the stored callback with the scenario index. A manual `Setup` overload
(`ScenarioRowUI.cs:88`) takes explicit values for cases without a `ScenarioResult`.

## Downloads

All downloads route through `ResultsScreenUI`. The button handlers pick the
target and call a writer that branches on platform.

### Config download

`OnDownloadConfigClicked` (`ResultsScreenUI.cs:505`) builds the config CSV. If a
completed `AggregateResults` is present it uses `_currentResults.ToConfigCsv()`
with the completion timestamp; otherwise it uses
`ConfigExporter.ToCsv(simulationController.Config)` with a fresh
`System.DateTime.Now`, so config can download before, during, or after a run
(`ResultsScreenUI.cs:505-531`). The filename is `tinysea_config_{timestamp}.csv`,
where `{timestamp}` is the `yyyy-MM-dd_HH-mm-ss` format described under
DisplayResults above. `ConfigExporter` is the static helper at the bottom of
`ScenarioResult.cs`; see `csv-output-formats.md` for the config CSV layout. Config
download is always enabled (`SetProgressMode` keeps it interactable,
`ResultsScreenUI.cs:190-191`).

### Aggregate download

`OnDownloadAggregateClicked` (`ResultsScreenUI.cs:533`) writes
`_currentResults.ToAggregateCsv()` as `tinysea_aggregate_{timestamp}.csv`, with
`{timestamp}` the `yyyy-MM-dd_HH-mm-ss` completion time. It is disabled until
results exist. Layout in `csv-output-formats.md`.

### Scenario download

`OnDownloadScenarioClicked(scenarioIndex)` (`ResultsScreenUI.cs:544`) finds the
matching `ScenarioResult`, and if `CsvData` is non empty writes it as
`tinysea_scenario{index}_{timestamp}.csv` (`ResultsScreenUI.cs:544-558`), with
`{timestamp}` the `yyyy-MM-dd_HH-mm-ss` completion time shared by all downloads of
this run. The CSV text was produced by the runner; see `csv-output-formats.md`.

### Download all (ZIP or folder)

`OnDownloadAllZipClicked` (`ResultsScreenUI.cs:560`) selects one of three paths by
state. The button label says "ZIP" and the methods are named `DownloadAllAsZip` /
`DownloadBulkAsZip` / `WebGLZipDownload.DownloadAsZip`, but on the active Windows
and macOS targets no `.zip` archive is created: every path writes a plain folder of
CSV files. A real ZIP is only built on the dead WebGL path via JSZip.

1. Bulk server ready (`_bulkServerReady`): set the button to `Downloading...`,
   call `ServerUpload.TriggerDownload()`, and reset the button after 3 seconds via
   `ResetDownloadButtonAfterDelay(3f)`, which uses `WaitForSeconds(3f)`
   (`ResultsScreenUI.cs:627-631`). `WaitForSeconds` is scaled time, so the 3-second
   reset stretches or compresses with `Time.timeScale`. This contrasts with the
   dots animation, which uses unscaled time. This path is WebGL only and not
   reachable on standalone, where `ServerUpload.IsAvailable` is always false, so the
   scaled-time detail is moot on shipping builds and only matters if ported.
2. Bulk progressive ready (`_bulkProgressiveReady`): set the button to
   `Preparing ZIP...` and run `DownloadBulkAsZip` (`ResultsScreenUI.cs:779-800`),
   which yields one frame so the text renders, then calls
   `WebGLZipDownload.FinalizeProgressiveZip()`. On Windows and macOS the bulk CSV
   files were already written to disk during the run as a plain folder, so finalize
   returns the output folder and, if `openFilesAfterSave`, opens it with
   `OpenFolder`.
3. Normal single-run: build the file set from `_currentResults` in
   `DownloadAllAsZip` (`ResultsScreenUI.cs:712-771`). It assembles `aggregate.csv`,
   `config.csv`, and one `scenario_{index}.csv` per scenario with `CsvData`. The
   output base name (the ZIP stem) is
   `tinysea_results_{timestamp}.zip` with `{timestamp}` the `yyyy-MM-dd_HH-mm-ss`
   completion time (`ResultsScreenUI.cs:743`). On Windows and macOS it writes each
   file into a folder under `SavePaths.ResultsFolder` named after the stem with the
   `.zip` extension stripped, that is `tinysea_results_{timestamp}`
   (`Path.GetFileNameWithoutExtension`, `ResultsScreenUI.cs:748-758`), then opens
   the folder if `openFilesAfterSave`. No `.zip` file is produced on standalone; the
   WebGL `WebGLZipDownload.DownloadAsZip` branch that would build a real archive is
   dead for shipping builds.

Button feedback uses `SetDownloadButtonState` and `ShowButtonFeedback` with
`WaitForSeconds` coroutines to show transient `Saved!` text then re-enable
(`ResultsScreenUI.cs:588-631`).

### Platform write path

`TriggerDownload(filename, content)` (`ResultsScreenUI.cs:636`) is the single-file
writer. On WebGL non-editor it calls `WebGLDownload.DownloadCsv` (dead path). On
Windows, macOS, and editor it writes the content to
`Path.Combine(SavePaths.ResultsFolder, filename)` with `File.WriteAllText`, then,
if `openFilesAfterSave`, opens the file with the OS default application via
`OpenFile` (`ResultsScreenUI.cs:654-680`). `OpenFile` uses `Process.Start` with
`UseShellExecute = true` on Windows and `open` on macOS. `OpenFolder`
(`ResultsScreenUI.cs:685-707`) uses `explorer.exe` on Windows and `open` on macOS.

## File save and load paths: `SavePaths`

`SavePaths` (`SavePaths.cs:10`) provides the output directory.

`OutputRoot` (`SavePaths.cs:14`) is platform dependent:

| Build | `OutputRoot` |
|---|---|
| Editor | `Application.persistentDataPath` |
| macOS standalone | the user `~/Downloads` folder |
| Windows or Linux standalone | the folder containing the executable, that is `Directory.GetParent(Application.dataPath).FullName` |
| Other (including WebGL) | `Application.persistentDataPath` |

The Linux branch is the `UNITY_STANDALONE_LINUX` arm of the same `#elif` as Windows
(`SavePaths.cs:25-27`); it exists in code but Linux is not an official build target.
The shipping targets are Windows standalone and macOS standalone, as stated under
Build target status above. A reimplementer scoping platforms should treat the Linux
arm as a compile-time branch that shares the Windows behavior, not as a tested
target.

`ResultsFolder` (`SavePaths.cs:38-60`) returns a cached writable `TinySeaResults`
folder. It first tries `Path.Combine(OutputRoot, "TinySeaResults")` and uses it if
`TryCreateFolder` succeeds; otherwise it falls back to
`Path.Combine(Application.persistentDataPath, "TinySeaResults")`, which is always
writable. `TryCreateFolder` (`SavePaths.cs:62-75`) creates the directory inside a
try/catch and returns false with a warning on failure, which is the fallback
trigger (for example macOS permission denial next to the app).

The folder choice is computed lazily on the first `ResultsFolder` access (the
getter returns the cached value immediately when it is already set,
`SavePaths.cs:42-43`) and cached for the lifetime of the process. The cache field
`_cachedFolder` is a `static` field on the static `SavePaths` class
(`SavePaths.cs:10-12`), so there is exactly one resolved folder per process,
shared by every caller. Nothing invalidates it: once set, the same folder is
returned for the rest of the run, so every writer in the subsystem targets the
same directory.

Every CSV writer in this subsystem targets `SavePaths.ResultsFolder`:
`ResultsScreenUI.TriggerDownload`, `DownloadAllAsZip`, the
`WebGLZipDownload` editor and standalone branches (`WebGLZipDownload.cs:75-89,
115-121, 140-145`), and the `CsvUploadHandler` template fallback
(`CsvUploadHandler.cs:335-338`). The standard run's `SimulationController` also
computes its output directory under `SavePaths.ResultsFolder`
(`SimulationController.cs:37`).

## ZIP writers: `WebGLZipDownload`

`WebGLZipDownload` (`WebGLZipDownload.cs:19`) provides two ways to package
multiple files. On WebGL it calls jslib functions (`TinySea_InitZipDownload`,
`TinySea_AddFileToZip`, `TinySea_FinalizeZipDownload`, `TinySea_ClearZipDownload`,
`WebGLZipDownload.cs:21-33`) backed by JSZip in the browser, the dead path. On
Windows, macOS, and editor it writes a folder of plain files; there is no actual
ZIP archive created in standalone builds.

- Batch API: `DownloadAsZip(zipFilename, files)` (`WebGLZipDownload.cs:57`). On
  standalone it creates a folder under `SavePaths.ResultsFolder` named after the
  ZIP stem (the `zipFilename` with the extension stripped) and writes each
  `(name, content)` with `File.WriteAllText`, creating intermediate directories
  (`WebGLZipDownload.cs:71-92`). It writes a folder, not a `.zip` archive. The
  single-run Download All path passes `tinysea_results_{timestamp}.zip` as
  `zipFilename`, so the folder is `tinysea_results_{timestamp}` (see Download all
  above).
- Progressive API: `InitProgressiveZip`, `AddFileToProgressiveZip`,
  `FinalizeProgressiveZip`, `ClearProgressiveZip` (`WebGLZipDownload.cs:106-191`).
  On standalone, `Init` creates and clears a folder under
  `SavePaths.ResultsFolder`, each `AddFile` writes one file immediately, and
  `Finalize` returns the folder path (or null on WebGL). The bulk run uses this so
  each scenario's CSV is written as it completes rather than held in memory; see
  `bulk-system.md`. `ProgressiveOutputFolder` and `ProgressiveFileCount`
  (`WebGLZipDownload.cs:44-49`) expose progress.

## Server upload: `ServerUpload`

`ServerUpload` (`ServerUpload.cs:20`) streams CSV files to a PHP backend that
stores them in S3. It is WebGL only and not the active path on Windows or macOS.
`IsAvailable` returns true only on WebGL non-editor (`ServerUpload.cs:33-43`), so
on shipping standalone builds it is always false and the bulk run falls back to
the progressive folder writer (`BulkSimulationController.cs:117-136`).

The WebGL flow, for reference (`ServerUpload.cs:7-18`):

1. `CreateSession(onComplete)` (`ServerUpload.cs:50`) POSTs to
   `{apiBase}/api/session.php` and parses a session id.
2. `UploadFile(filename, content, onComplete)` (`ServerUpload.cs:79`) gzips and
   base64-encodes the content with `CompressToBase64` (`ServerUpload.cs:193`),
   POSTs a JSON body to `{apiBase}/api/upload.php`, and retries once on failure.
3. `GetDownloadUrl` and `TriggerDownload` (`ServerUpload.cs:134-155`) open
   `{apiBase}/api/download.php?session=...` with `Application.OpenURL` so the
   browser downloads the assembled ZIP.

`GetApiBase` returns the empty string on WebGL (same origin) and
`http://localhost` in the editor (`ServerUpload.cs:166-175`). `Clear`
(`ServerUpload.cs:160`) resets session state.

## Native file dialog: `StandaloneFileBrowser`

`StandaloneFileBrowser` (`StandaloneFileBrowser.cs:9`) provides native open and
save dialogs for the Windows and macOS standalone builds, the active targets. It
does nothing in WebGL or the editor; the editor uses `EditorUtility` dialogs
instead (`StandaloneFileBrowser.cs:5-8`).

`OpenFilePanel(title, extension)` (`StandaloneFileBrowser.cs:51`):

- Windows: P/Invokes `GetOpenFileName` from `comdlg32.dll` with an `OPENFILENAME`
  struct (`StandaloneFileBrowser.cs:11-45, 53-68`). The filter is built from the
  extension, and the flags are `OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR`
  (`0x00080000 | 0x00001000 | 0x00000008`). Returns the selected path or null.
- macOS: runs `osascript` with a `choose file of type` AppleScript and returns the
  POSIX path the script prints, or null on cancel or error
  (`StandaloneFileBrowser.cs:69-92`).

`SaveFilePanel(title, defaultName, extension)` (`StandaloneFileBrowser.cs:99`):

- Windows: P/Invokes `GetSaveFileName` with the file buffer pre-filled with
  `defaultName` and flags `OFN_EXPLORER | OFN_OVERWRITEPROMPT | OFN_NOCHANGEDIR`
  (`0x00080000 | 0x00000002 | 0x00000008`, `StandaloneFileBrowser.cs:101-119`).
- macOS: runs `osascript` with a `choose file name ... default name` AppleScript
  (`StandaloneFileBrowser.cs:120-140`).

These dialogs are invoked from the bulk CSV upload flow, not from this subsystem
directly. `CsvUploadHandler` calls `StandaloneFileBrowser.OpenFilePanel("Load Bulk
CSV", "csv")` when the user presses the `L` key on standalone, reads the chosen
file with `File.ReadAllText`, and forwards it to the bulk run
(`CsvUploadHandler.cs:282-294`). It calls
`StandaloneFileBrowser.SaveFilePanel("Save Template CSV", "bulk_template.csv",
"csv")` for the template download, falling back to a write into
`SavePaths.ResultsFolder` when the dialog is cancelled
(`CsvUploadHandler.cs:325-339`). The bulk upload UI and CSV format are documented
in `bulk-system.md`.

## Single-file writer: `WebGLDownload`

`WebGLDownload` (`WebGLDownload.cs:4`) wraps the single-file jslib download
(`TinySea_DownloadFile`). It is WebGL only and dead for shipping builds: on WebGL
non-editor it UTF-8 encodes the CSV and calls the jslib; on every other platform,
including the editor and both standalone targets, it only logs that it was called
outside WebGL (`WebGLDownload.cs:11-19`). Single-file saves on Windows and macOS go
through `ResultsScreenUI.TriggerDownload`'s `File.WriteAllText` branch instead.
