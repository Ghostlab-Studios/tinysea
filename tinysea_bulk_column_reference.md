# TinySea Bulk Upload — CSV Column Reference

## How to Use
Each row = one batch configuration. Each batch runs `num_scenarios` times with the same settings but different random seeds.

## Simulation Settings
| Column | Type | Description | Example |
|--------|------|-------------|---------|
| batch_name | text | Label for this batch (becomes folder name in ZIP output) | warm_baseline |
| days | integer | Days per scenario | 365 |
| num_scenarios | integer | How many times to run this config | 5 |

## Temperature Settings
| Column | Type | Description | Example |
|--------|------|-------------|---------|
| base_temp | number | Mean annual temperature (°C) | 20 |
| seasonal_amp | number | Seasonal temperature swing (°C) | 10 |
| climate_trend | number | Warming per year (°C/year) | 1 |
| variability_mag | number | Interannual variability magnitude | 2 |
| warming_bias | number | Warming bias factor | 1.5 |
| daily_var_range | number | Daily random variation range (°C) | 5 |
| randomness_growth | number | Rate of randomness increase | 0.5 |
| autocorrelated | true/false | Use autocorrelated temperature? | true |
| temp_min | number | Absolute minimum temperature bound (°C) | -5 |
| temp_max | number | Absolute maximum temperature bound (°C) | 50 |

## Carrying Capacity
| Column | Type | Description | Example |
|--------|------|-------------|---------|
| use_carrying_cap | true/false | Enable carrying capacity for Tier 1? | true |
| carrying_cap_t1 | integer | Tier 1 population limit | 5000 |

## Species (S1 = Species 1, S2 = Species 2)
Repeat these columns with prefix S1_ and S2_. All temperatures in Celsius.

| Column Suffix | Type | Description | Example |
|---------------|------|-------------|---------|
| name | text | Species name | Hexapod |
| variant | text | Thermal variant (Arctic/Common/Tropical) | Common |
| tier | integer | Food chain tier (0=prey, 1=predator) | 0 |
| pop | integer | Starting population | 20 |
| eating | number | Eating amount per individual | 0 |
| reproMult | number | Reproduction multiplier | 0.45 |
| deathThresh | number | Performance threshold for thermal death | 0.3 |
| deathRate | number | Death rate when below threshold | 0.6 |
| minDeaths | integer | Minimum deaths per cycle | 1 |
| reproThresh | number | Performance threshold for reproduction | 0.25 |
| naturalDeathRate | number | Base natural death rate | 0.02 |
| naturalDeathVar | number | Natural death rate variance | 0.01 |
| huntEff | number | Hunting efficiency | 0.75 |
| huntVar | number | Hunting variance | 0.15 |
| optTempC | number | Optimal temperature (°C) | 20 |
| arrhenBreadth | number | Arrhenius breadth parameter | 5273.15 |
| arrhenLower | number | Arrhenius lower parameter | 10273.15 |
| arrhenUpper | number | Arrhenius upper parameter | 21273.15 |
| lowerBoundC | number | Lower thermal bound (°C) | 12 |
| upperBoundC | number | Upper thermal bound (°C) | 22 |

## Notes
- Open the template in Excel/Google Sheets for easier editing
- Copy a row and tweak only the values you want to change
- batch_name must be unique per row (used as folder name in output)
- All temperature values are in Celsius — the simulation converts to Kelvin internally
