import csv, io

# Species templates: [name, variant, tier, pop, eating, repro_mult, death_thresh, death_rate,
#   repro_thresh, nat_death, nat_death_var, hunt_eff, hunt_var, opt_temp_c, arrhen_breadth,
#   arrhen_lower, arrhen_upper, lower_c, upper_c, pmax]

# Standard species
HEX_ARC = ['Hexapod','Arctic',0,1000,0,0.45,0.3,0.6,0.4,0.02,0.01,1.0,0,17.85,4000,13974,35000,17.75,17.95,0.85]
HEX_COM = ['Hexapod','Common',0,1000,0,0.45,0.3,0.6,0.4,0.02,0.01,1.0,0,23.85,8000,3000,35000,22.85,24.85,0.65]
HEX_TRO = ['Hexapod','Tropical',0,1000,0,0.45,0.3,0.6,0.4,0.02,0.01,1.0,0,29.85,4000,15827,35000,29.75,29.95,0.85]
SHE_ARC = ['Sheplik','Arctic',1,100,1.5,0.1,0.2,0.3,0.3,0.01,0.005,0.75,0.1,17.85,4000,13974,35000,17.75,17.95,0.85]
SHE_COM = ['Sheplik','Common',1,100,1.5,0.1,0.2,0.3,0.3,0.01,0.005,0.75,0.1,23.85,8000,3000,35000,22.85,24.85,0.65]
SHE_TRO = ['Sheplik','Tropical',1,100,1.5,0.1,0.2,0.3,0.3,0.01,0.005,0.75,0.1,29.85,4000,15827,35000,29.75,29.95,0.85]
STD6 = [HEX_ARC, HEX_COM, HEX_TRO, SHE_ARC, SHE_COM, SHE_TRO]

# Custom species — prey (tier 0)
CORAL    = ['Coral','Custom',0,500,0,0.45,0.3,0.6,0.4,0.02,0.01,1.0,0,22,6000,5000,35000,20,24,0.70]
KELP     = ['Kelp','Custom',0,500,0,0.45,0.3,0.6,0.4,0.02,0.01,1.0,0,12,5000,10000,35000,10,14,0.75]
ANEMONE  = ['Anemone','Custom',0,500,0,0.45,0.3,0.6,0.4,0.02,0.01,1.0,0,28,5000,12000,35000,26,30,0.75]
JELLY    = ['Jellyfish','Custom',0,500,0,0.40,0.25,0.5,0.35,0.015,0.008,1.0,0,20,10000,2000,35000,15,25,0.60]
URCHIN   = ['Urchin','Custom',0,500,0,0.50,0.35,0.65,0.45,0.02,0.01,1.0,0,23,3000,8000,35000,22,24,0.80]

# Custom species — predators (tier 1)
NUDI     = ['Nudibranch','Custom',1,50,1.5,0.1,0.2,0.3,0.3,0.01,0.005,0.75,0.1,22,6000,5000,35000,20,24,0.70]
SEASTAR  = ['Seastar','Custom',1,50,1.5,0.1,0.2,0.3,0.3,0.01,0.005,0.75,0.1,12,5000,10000,35000,10,14,0.75]
LIONFISH = ['Lionfish','Custom',1,50,1.5,0.1,0.2,0.3,0.3,0.01,0.005,0.75,0.1,28,5000,12000,35000,26,30,0.75]
CRAB     = ['Crab','Custom',1,50,1.5,0.1,0.2,0.3,0.3,0.01,0.005,0.75,0.1,20,8000,3000,35000,18,22,0.65]
OCTOPUS  = ['Octopus','Custom',1,50,1.5,0.1,0.2,0.3,0.3,0.01,0.005,0.80,0.08,25,7000,4000,35000,23,27,0.70]

EMPTY = [''] * 20
MAX_SP = 10

# Build header
sp_fields = []
for i in range(1, MAX_SP + 1):
    for f in ['name','variant','tier','pop','eating','repro_mult','death_thresh','death_rate',
              'repro_thresh','natural_death_rate','natural_death_var','hunt_eff','hunt_var',
              'opt_temp_c','arrhen_breadth','arrhen_lower','arrhen_upper','lower_bound_c',
              'upper_bound_c','pmax']:
        sp_fields.append(f'sp{i}_{f}')

header = [
    'batch_name','days','num_scenarios','base_temp','seasonal_amp','climate_trend',
    'variability_mag','warming_bias','daily_var_range','randomness_growth',
    'autocorrelated','interannual_variation','temp_min','temp_max',
    'use_carrying_cap','carrying_cap_t1','condition_drain_rate','condition_recovery_rate'
] + sp_fields


def make_row(name, days, scenarios, base_temp, seasonal_amp, climate_trend,
             var_mag, warm_bias, daily_var, rand_growth, autocorr, interann,
             tmin, tmax, use_cap, cap_t1, drain, recovery, species_list):
    global_cols = [name, days, scenarios, base_temp, seasonal_amp, climate_trend,
                   var_mag, warm_bias, daily_var, rand_growth, autocorr, interann,
                   tmin, tmax, use_cap, cap_t1, drain, recovery]
    padded = list(species_list)
    while len(padded) < MAX_SP:
        padded.append(EMPTY)
    sp_cols = []
    for sp in padded[:MAX_SP]:
        sp_cols.extend(sp)
    return global_cols + sp_cols


# Low-pop variants for fragile ecosystem test
low_hex_arc = list(HEX_ARC); low_hex_arc[3] = 50
low_hex_com = list(HEX_COM); low_hex_com[3] = 50
low_hex_tro = list(HEX_TRO); low_hex_tro[3] = 50
low_she_arc = list(SHE_ARC); low_she_arc[3] = 10
low_she_com = list(SHE_COM); low_she_com[3] = 10
low_she_tro = list(SHE_TRO); low_she_tro[3] = 10

rows = []

# ---- TEMPERATURE REGIME TESTS ----
# 1. Baseline mild — Common should dominate
rows.append(make_row('Std_Mild20', 365, 5, 20, 10, 0, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.15, 0.1, STD6))

# 2. Cold — Arctic should dominate, Tropical dies
rows.append(make_row('Std_Cold12', 365, 5, 12, 8, 0, 0, 0, 5, 0,
    'false', 'false', -10, 45, 'true', 5000, 0.15, 0.1, STD6))

# 3. Hot — Tropical should dominate, Arctic dies
rows.append(make_row('Std_Hot32', 365, 5, 32, 8, 0, 0, 0, 5, 0,
    'false', 'false', -5, 50, 'true', 5000, 0.15, 0.1, STD6))

# 4. Extreme cold — everything should crash
rows.append(make_row('Extreme_Cold5', 365, 5, 5, 5, 0, 0, 0, 3, 0,
    'false', 'false', -15, 45, 'true', 5000, 0.15, 0.1, STD6))

# 5. Tropical paradise — 30C no trend
rows.append(make_row('Tropical_30', 365, 5, 30, 8, 0, 0, 0, 5, 0,
    'false', 'false', -5, 50, 'true', 5000, 0.15, 0.1, STD6))

# ---- CLIMATE TREND TESTS ----
# 6. Slow warming — 2 years
rows.append(make_row('Warming_Slow', 730, 5, 20, 10, 0.02, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.15, 0.1, STD6))

# 7. Fast warming — should crash
rows.append(make_row('Warming_Fast', 730, 5, 20, 10, 0.10, 0, 0, 5, 0,
    'false', 'false', -5, 50, 'true', 5000, 0.15, 0.1, STD6))

# 8. Cooling trend
rows.append(make_row('Cooling', 730, 3, 25, 10, -0.05, 0, 0, 5, 0,
    'false', 'false', -10, 45, 'true', 5000, 0.15, 0.1, STD6))

# ---- DURATION TESTS ----
# 9. Short run — 30 days, barely any dynamics
rows.append(make_row('Short_30d', 30, 3, 20, 10, 0, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.15, 0.1, STD6))

# 10. Long run — 5 years with slow warming
rows.append(make_row('Long_5yr', 1825, 3, 20, 10, 0.01, 0, 0, 5, 0,
    'false', 'false', -5, 50, 'true', 5000, 0.15, 0.1, STD6))

# ---- VARIABILITY TESTS ----
# 11. High variability — big daily swings, interannual on
rows.append(make_row('HighVar', 365, 5, 20, 12, 0, 2.0, 0.5, 15, 0.02,
    'true', 'true', -10, 50, 'true', 5000, 0.15, 0.1, STD6))

# ---- SYSTEM PARAMETER TESTS ----
# 12. No carrying capacity — populations can explode
rows.append(make_row('NoCap', 365, 5, 20, 10, 0, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'false', 0, 0.15, 0.1, STD6))

# 13. High condition drain — species stressed faster
rows.append(make_row('HighDrain', 365, 5, 20, 10, 0, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.30, 0.05, STD6))

# 14. Low starting population — fragile ecosystem
rows.append(make_row('LowPop_Start', 365, 5, 20, 10, 0, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.15, 0.1,
    [low_hex_arc, low_hex_com, low_hex_tro, low_she_arc, low_she_com, low_she_tro]))

# ---- CUSTOM SPECIES TESTS ----
# 15. Standard + 2 custom (moderate custom test)
rows.append(make_row('Custom_Mix8', 365, 5, 22, 10, 0, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.15, 0.1,
    STD6 + [CORAL, NUDI]))

# 16. 10 all-custom — no standard species at all
rows.append(make_row('All_Custom10', 365, 5, 22, 10, 0.03, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.15, 0.1,
    [CORAL, KELP, ANEMONE, JELLY, URCHIN, NUDI, SEASTAR, LIONFISH, CRAB, OCTOPUS]))

# 17. Many custom — 4 standard + 6 custom
rows.append(make_row('Many_Custom', 365, 5, 20, 10, 0, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.15, 0.1,
    [HEX_COM, HEX_TRO, SHE_COM, SHE_TRO, CORAL, KELP, ANEMONE, JELLY, NUDI, CRAB]))

# 18. Prey only — no predators
rows.append(make_row('Prey_Only', 365, 5, 20, 10, 0, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.15, 0.1,
    [HEX_ARC, HEX_COM, HEX_TRO, CORAL, JELLY, URCHIN]))

# 19. Cold-adapted customs in hot environment — should struggle
rows.append(make_row('ColdSp_HotEnv', 365, 5, 30, 8, 0, 0, 0, 5, 0,
    'false', 'false', -5, 50, 'true', 5000, 0.15, 0.1,
    [KELP, CORAL, ANEMONE, JELLY, SEASTAR, NUDI, LIONFISH, CRAB]))

# 20. Higher scenario count — more statistical power with customs
rows.append(make_row('HighN_10sc', 365, 10, 22, 10, 0.02, 0, 0, 5, 0,
    'false', 'false', -5, 45, 'true', 5000, 0.15, 0.1,
    STD6 + [CORAL, NUDI]))


# Write CSV
with open('Tests/test_comprehensive.csv', 'w', newline='') as f:
    writer = csv.writer(f)
    writer.writerow(header)
    for r in rows:
        writer.writerow(r)

print(f"Generated {len(rows)} rows x {len(header)} columns")
print(f"Saved to Tests/test_comprehensive.csv")
print()
print("Test matrix:")
print(f"{'#':>2}  {'Batch':<20} {'Days':>5} {'Sc':>3} {'Temp':>5} {'Trend':>6} {'Sp':>3}  Key test")
print("-" * 85)
for i, r in enumerate(rows):
    sp_count = sum(1 for j in range(10) if r[18 + j*20] != '')
    name = r[0]
    days = r[1]
    sc = r[2]
    temp = r[3]
    trend = r[5]

    tests = {
        'Std_Mild20': 'Baseline — Common dominates',
        'Std_Cold12': 'Arctic dominates, Tropical dies',
        'Std_Hot32': 'Tropical dominates, Arctic dies',
        'Extreme_Cold5': 'Everything crashes (too cold)',
        'Tropical_30': 'Tropical paradise',
        'Warming_Slow': '2yr slow warming',
        'Warming_Fast': '2yr fast warming — crashes likely',
        'Cooling': '2yr cooling trend',
        'Short_30d': '30 days — minimal dynamics',
        'Long_5yr': '5 years slow warming',
        'HighVar': 'Big daily swings + interannual',
        'NoCap': 'No carrying cap — pop explosion',
        'HighDrain': 'Fast condition drain',
        'LowPop_Start': 'Low initial pop — fragile',
        'Custom_Mix8': '6 standard + 2 custom',
        'All_Custom10': '10 all-custom species',
        'Many_Custom': '4 std + 6 custom',
        'Prey_Only': 'No predators',
        'ColdSp_HotEnv': 'Cold customs in 30C',
        'HighN_10sc': '10 scenarios for stats power',
    }
    desc = tests.get(name, '')
    print(f"{i+1:2d}. {name:<20} {days:>5} {sc:>3} {temp:>5} {trend:>6} {sp_count:>3}  {desc}")

# Count total scenarios
total_sc = sum(r[2] for r in rows)
print(f"\nTotal scenarios to simulate: {total_sc}")
