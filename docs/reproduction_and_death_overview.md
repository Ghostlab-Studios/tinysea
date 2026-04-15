# TinySea Simulation: Death & Reproduction Systems

## Overview

The simulation uses two core systems to govern species survival: **death** (how species decline) and **reproduction** (how species grow). Both are graduated, meaning there are no hard cliffs or binary on/off switches. This document covers the current formulas and logic for both.

---

## Condition (Species Health)

Before diving into death and reproduction, it helps to understand **Condition**, since both systems depend on it.

Condition is a value between 0 and 1 that represents the overall health of a species. Think of it as stored energy reserves. It starts at 1.0 (perfect health) and changes over time based on the environment.

**What drives Condition down:**
- Poor temperature (low thermal performance drains Condition)
- Poor feeding (T2 predators only, when prey is scarce)

**What drives Condition up:**
- Good temperature (high thermal performance lets Condition recover)
- Good feeding (T2 predators successfully hunting prey)

Condition always drains *toward* the current environmental target, which is `RawThermalPerformance x FedRate` (thermal performance without Pmax scaling). If the environment is good, Condition recovers. If the environment is poor, Condition drops. The drain is asymmetric: Condition drains faster than it recovers, and drain accelerates up to 5x near lethal temperatures.

**Why Condition matters:** Condition acts as a buffer. It is a lagging indicator, not an instantaneous snapshot. A species that had a good summer enters winter with high Condition, even though winter temperatures are bad. This models **thermal acclimation**: the ability of organisms to tolerate short-term environmental stress using energy reserves built up during favorable periods. Condition doesn't change instantly with the weather. It takes time to drain, giving species a realistic window of resilience.

Condition is independent of population size. Fewer individuals does not cause Condition to drop. In fact, for T2 predators, fewer individuals means less competition for prey, which improves feeding and helps Condition recover faster.

---

## Death

There are four types of death in the simulation. Within the 10-step biology sequence, they occur in this order: Predation (step 2), Thermal Death (step 6), Condition Death (step 7), then Natural Death (step 9). Note that Reproduction (step 8) is interleaved between Condition Death and Natural Death.

### 1. Thermal Death (Instant Kill)

If the temperature goes beyond a species' absolute survival range (CTmin or CTmax), the entire population dies instantly. This represents lethal temperature extremes. No buffer, no Condition, nothing can save the species.

- **Trigger:** `RawThermalPerformance == 0`, which occurs when temperature reaches or exceeds CTmin/CTmax. Note: a smooth 2-degree cosine fade zone (`LETHAL_TRANSITION_WIDTH = 2.0`) exists just inside the CTmin/CTmax boundaries, where performance drops gradually toward zero. This fade causes Condition to drain before the lethal limit is reached, but the instant kill only fires when performance hits exactly zero.
- **Result:** Entire population wiped out
- **Example:** If CTmax = 40C and the temperature hits 41C, all individuals die. At 39C (within the 2-degree fade zone), performance is reduced but not zero, so thermal death does not trigger — instead, Condition drains faster due to low performance.

This is the only binary death type. It is all-or-nothing.

### 2. Predation (Holling Type II)

Higher-tier species consume lower-tier species via a **Holling Type II functional response**. This is a death mechanism for T1 prey — individuals are removed from the prey population when eaten by T2 predators.

- **Trigger:** T2 predators exist and T1 prey are present
- **Mechanism:** Hunting success scales with the prey:predator ratio. At the reference ratio (20:1), base `HuntingEfficiency` (default 0.75) applies. Below this ratio, hunting success decreases (prey scarcity). Above, it increases toward 1.0. Daily `HuntingVariance` (+/-0.15) adds stochasticity.
- **Effect on prey:** Prey eaten = `predator_pop x eating_amount x hunting_success`. Uses a fractional accumulator (same as other death types).
- **Effect on predators:** Predation does not directly kill predators, but poor hunting reduces their `FedRate`, which lowers the Condition drain target and eventually triggers condition death.

### 3. Condition Death (Graduated, Threshold-Gated)

When Condition drops below the **DeathThreshold** (default 0.3), individuals begin dying. This is **threshold-gated**, meaning deaths only occur once the threshold is crossed. Above the threshold, zero condition deaths happen.

Below the threshold, the death rate scales proportionally with how far below the threshold the species is. The further below, the more die.

**Formula:**

```
severity = (DeathThreshold - Condition) / DeathThreshold
deaths  = Population x severity x DeathRate x BiologyStep
```

Where:
- `severity` ranges from 0 (at the threshold) to 1.0 (at Condition = 0)
- `DeathRate` is the maximum fraction of population that can die per day (T1: 0.6, T2: 0.3)
- `BiologyStep` is the time step (1 day)

**Example values:**

| Condition | Severity | What happens |
|-----------|----------|-------------|
| 0.30 (at threshold) | 0.0 | No deaths. Condition is right at the line. |
| 0.15 | 0.5 | Half the max death rate applies. Population is declining. |
| 0.00 | 1.0 | Full death rate. Species is in critical condition. |

**Fractional accumulator:** Deaths are often fractional. For example, if the formula says 3.5 should die, 3 die immediately and the 0.5 carries over to the next day. If the next day produces 6.6 deaths, the total becomes 6.6 + 0.5 = 7.1. So 7 die, and 0.1 carries forward. No fractional deaths are ever lost.

**Survivor fitness boost:** After condition deaths, the surviving population gets healthier on average. The logic: the individuals that died were the weakest (lowest condition). Removing them raises the group average.

```
new_condition = old_condition x old_population / new_population
```

This is important because it prevents death spirals. As the weakest die off, the survivors become healthier, which slows down further deaths.

### 4. Natural Death (Flat Rate, Always Active)

A small, constant daily death rate that represents old age, disease, and accidents. This applies every single day, regardless of temperature, Condition, or any other factor. It is not gated by any threshold.

| Parameter | T1 (Prey) | T2 (Predator) | Why different? |
|-----------|-----------|---------------|---------------|
| Base rate | 2% per day | 1% per day | Allometric scaling: larger animals have lower background mortality |
| Variance | ±1% per day | ±0.5% per day | Random daily fluctuation around the base rate |

So on any given day, the actual natural death rate for T1 might be anywhere from 1% to 3%, and for T2 it might be 0.5% to 1.5%.

Natural death also uses a **fractional accumulator**, same as condition death. Small fractional deaths carry over between days.

**Why this matters:** Even when Condition is perfect and temperature is ideal, species still lose individuals. This is the baseline mortality that reproduction must overcome for the population to grow. For T2 predators with only 1% natural death, this is a slow bleed, but over a 90-day winter it adds up.

---

## Reproduction

Reproduction is driven by **Condition** (species health), not instantaneous thermal performance.

### Threshold-Shaped, Not Threshold-Gated

This is the key difference between death and reproduction.

- **Death** is threshold-**gated**: it only triggers once Condition drops below DeathThreshold. Above the threshold, zero deaths.
- **Reproduction** is threshold-**shaped**: the threshold is an inflection point that shapes the curve, but reproduction always happens as long as Condition > 0. There is no gate.

Even a species in poor condition (say Condition = 0.10) still produces a trickle of births. Those tiny fractional births accumulate over days and eventually become whole individuals.

### Why Condition (Not Thermal Performance)?

Thermal performance is instantaneous. It only reflects the current temperature on that specific day. In winter, thermal performance drops to near-zero, which previously caused zero reproduction for roughly 90 days straight. This is ecologically unrealistic: animals with stored energy reserves still reproduce in cold weather, just at reduced rates.

Condition solves this because it is a lagging indicator. It integrates:
- **Temperature history:** drains when thermal performance is poor
- **Feeding:** drains when the species is underfed (predators with scarce prey)
- **Recent past:** takes time to drain, so a species entering winter healthy still has reserves

This models the ecological reality of **thermal acclimation** and **energy reserve-mediated reproduction**. Organisms do not base reproductive decisions solely on today's temperature. They invest in reproduction based on their overall body condition, which reflects weeks or months of accumulated environmental quality.

### The Formula

Reproduction uses a continuous, graduated scale with two regions joined at the **ReproThreshold** (default 0.25). The threshold is the inflection point where reproduction transitions from strong to weak.

**Above the threshold (healthy reproduction):**

```
t          = (Condition - ReproThreshold) / (1.0 - ReproThreshold)
reproScale = 0.10 + 0.90 x t
```

This ramps from 0.10 (at the threshold) up to 1.0 (at full Condition). A species in good health reproduces strongly.

**Below the threshold (struggling reproduction):**

```
reproScale = 0.10 x (Condition / ReproThreshold)
```

This ramps from 0.0 (at Condition = 0) up to 0.10 (at the threshold). A species in poor health still reproduces, but at a heavily reduced rate.

**Both regions give exactly 0.10 at the threshold, so the curve is continuous. No jump, no cliff.**

**Important clarification:** reproScale is not simply equal to Condition. It is a piecewise transformation that uses the threshold as an inflection point. For example:
- At Condition = 0.50, reproScale = 0.40 (not 0.50)
- At Condition = 0.15, reproScale = 0.06 (not 0.15)

The threshold shapes how aggressively Condition translates into reproductive output.

**Final birth calculation:**

```
births = Population x reproScale x ReproductionMultiplier x BiologyStep
```

Where:
- `reproScale` is calculated from Condition as described above (0 to 1)
- `ReproductionMultiplier` is the species' base birth rate (T1: 0.45, T2: 0.1)
- `BiologyStep` is the time step (1 day)

### reproScale at Key Condition Levels

| Condition | Region | reproScale | What it means |
|-----------|--------|-----------|---------------|
| 1.00 | Above threshold | 1.000 | Peak health. Full reproduction rate. |
| 0.80 | Above threshold | 0.760 | Healthy. Strong reproduction. |
| 0.50 | Above threshold | 0.400 | Decent health. Moderate reproduction. |
| 0.25 | At threshold | 0.100 | Inflection point. Both formulas meet here. |
| 0.15 | Below threshold | 0.060 | Struggling. Only a trickle of births. |
| 0.10 | Below threshold | 0.040 | Very low. Rare births, but accumulator captures them. |
| 0.05 | Below threshold | 0.020 | Near death. Barely any births. |
| 0.00 | | 0.000 | Condition is zero. No reproduction. |

### Additional Factors

After the base birth calculation, several additional factors are applied:

- **No-Predator Penalty:** If no T2 predators exist in the ecosystem, T1 births are reduced by 15%. This prevents unchecked prey growth when predators go extinct.
- **Minimum Population:** A species needs at least 2 individuals to reproduce (`MIN_POPULATION_FOR_REPRODUCTION = 2`). This models the ecological requirement for a mate.
- **Carrying Capacity (T1 only):** Reproduction slows as T1 population approaches the cap (default 5000). Uses a linear density factor: `growthFactor = max(0, 1 - tierPop/capacity)`. At 50% capacity, birth rate is halved. At 100%, births stop.
- **Birth Accumulator:** Same as the death accumulators. Fractional births carry over between days. Even 0.02 births per day will accumulate to 1 whole birth over roughly 50 days.
- **Newborn Condition Dilution:** Newborns enter at Condition = 0.5, which slightly lowers the group average. This creates natural self-regulation: each batch of births mildly suppresses the next by pulling Condition down. Condition then recovers, allowing more births. This is a gentle negative feedback loop.

### Why This Does Not Create a Death Spiral

A valid concern: if low Condition reduces births, and fewer births mean fewer individuals, could Condition keep dropping in a self-reinforcing spiral?

No. The system is self-correcting, not self-reinforcing. Here is why:

- Condition drains toward the environmental target (temperature x feeding rate), which is independent of population size. Births do not affect this target.
- For T2 predators, fewer individuals actually *helps*. Fewer predators means less competition for prey, better feeding, and faster Condition recovery.
- The survivor fitness boost pushes Condition back up whenever weak individuals die.
- Newborn dilution is mild. Newborns enter at 0.5, which is above the death threshold (0.3). The condition dip from each batch of births is small (typically 3-5%) and recovers quickly.

---

## Emergent Behavior: Condition-Dependent Fecundity

The interaction of condition-based reproduction, condition death, and seasonal temperature produces **emergent periodicity in reproductive output**. This is a pattern not explicitly coded, but arising naturally from the formula interactions.

Because reproduction scales with Condition (a lagging indicator of species health), reproductive output oscillates seasonally:

- **Summer:** High thermal performance. Condition recovers. Reproduction ramps toward full rate.
- **Autumn:** Temperature drops. Condition begins draining. Reproduction slows gradually.
- **Winter:** Condition low. Reproduction reduced to a trickle (but still non-zero). Natural death and condition death thin the population.
- **Spring:** Temperature rises. Condition recovers. Reproduction accelerates. Populations rebound.

Combined with **newborn condition dilution**, the system exhibits **condition-dependent fecundity**: organisms reproduce more when healthy and scale back reproductive output as condition declines. This creates natural fluctuations in birth rates without any explicit seasonal forcing. We did not code this behavior in. It emerges from the interaction between the condition-based formula and newborn dilution.

In 10-year simulations, T2 predators show persistent oscillations: peaking at 500-1000+ in summer, dipping to 30-70 in winter, and recovering every spring. These cycles are stable over the full simulation period with no damping or divergence.

This is consistent with **condition-dependent reproductive variation**, a well-established phenomenon in ecology where an organism's body condition mediates its reproductive investment. The principle is grounded in state-dependent life history theory (McNamara & Houston, 1996) and has been documented empirically in marine species:

- **Jorgensen et al. (2006)** showed that Atlantic cod skip spawning seasons when body condition is poor. Fecundity is directly mediated by energy reserves, not just environmental cues.
- **Brosset et al. (2016)** demonstrated that body condition acts as the primary driver of reproductive success in small pelagic fish, with poor-condition individuals producing fewer and lower-quality offspring.
- **Rideout et al. (2011)** found that condition-dependent fecundity regulation is widespread across marine teleosts, where individuals modulate reproductive effort based on stored energy.

---

## Summary

| System | Driver | Type | Threshold Role | Zero Only When |
|--------|--------|------|---------------|---------------|
| Thermal Death | Temperature vs CTmin/CTmax | Instant, binary | N/A | Temp within survival range |
| Predation | Prey:predator ratio | Holling Type II + accumulator | N/A: always active when both tiers present | No predators or no prey |
| Condition Death | Condition vs DeathThreshold | Graduated severity | **Gated**: only triggers below threshold | Condition above threshold |
| Natural Death | Flat daily rate | Constant + accumulator | N/A: always applies | Never |
| Reproduction | Condition vs ReproThreshold | Graduated, continuous | **Shaped**: inflection point, not a gate | Condition = 0 or population < 2 |

**Key takeaways:**

- All fractional values (deaths and births) use accumulators. Nothing is lost to rounding.
- Condition death and reproduction both use graduated scaling based on Condition, but they differ in how the threshold works. Death is **gated** (threshold must be crossed before deaths begin). Reproduction is **shaped** (threshold determines the curve's inflection point, but reproduction always occurs when Condition > 0).
- Condition acts as a thermal acclimation buffer. It integrates temperature and feeding history, giving species realistic resilience to short-term environmental stress.
- No hard cliffs anywhere in the system.
