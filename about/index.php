<?php
$current_page = 'about';
$page_title = 'About - Tiny Sea Simulation';
include __DIR__ . '/../includes/header.php';
?>

<section class="about-page">
    <div class="page-hero is-simple has-bg">
        <h1>About Tiny Sea Simulation</h1>
        <p class="subtitle">A Scientific Research Platform for Marine Ecosystem Dynamics</p>
    </div>

    <div class="content-section">
        <div class="card">
            <div class="card-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M9 3h6"/><path d="M10 3v6.5L4.8 18a2 2 0 0 0 1.7 3h11a2 2 0 0 0 1.7-3L14 9.5V3"/><path d="M7 15h10"/>
                </svg>
            </div>
            <h2>Research Purpose</h2>
            <p>Tiny Sea Simulation is a scientific research platform developed in collaboration with marine biologist Brian Helmuth to study climate change impacts on marine food webs. This simulation has evolved from a temperature-based management game into a sophisticated tool for generating research data on ecosystem dynamics.</p>
        </div>

        <div class="card">
            <div class="card-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <circle cx="12" cy="12" r="10"/><polygon points="16.24 7.76 14.12 14.12 7.76 16.24 9.88 9.88 16.24 7.76"/>
                </svg>
            </div>
            <h2>Two Ways to Explore</h2>
            <p>Tiny Sea comes in two modes that share the same underlying ecosystem model:</p>
            <div class="data-grid">
                <div class="data-category">
                    <h3>Play the Game</h3>
                    <ul>
                        <li>Runs in your web browser</li>
                        <li>Buy and sell organisms</li>
                        <li>Keep the food web balanced as the climate shifts</li>
                    </ul>
                </div>
                <div class="data-category">
                    <h3>Run the Simulation</h3>
                    <ul>
                        <li>Desktop app for Windows and macOS</li>
                        <li>Headless multi-year scenarios</li>
                        <li>Exports CSV datasets for research</li>
                    </ul>
                </div>
            </div>
        </div>

        <div class="card">
            <div class="card-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M14 14.76V4a2 2 0 0 0-4 0v10.76a4 4 0 1 0 4 0z"/>
                </svg>
            </div>
            <h2>Temperature Modeling</h2>
            <p>The simulation uses realistic temperature modeling that incorporates multiple environmental factors:</p>
            <ul>
                <li><strong>Base Temperature:</strong> Starting point for the marine environment</li>
                <li><strong>Seasonal Variation:</strong> Natural temperature cycles throughout the year</li>
                <li><strong>Climate Warming Trends:</strong> Long-term temperature increase over time</li>
                <li><strong>Interannual Variation:</strong> Year-to-year fluctuations with memory effects</li>
                <li><strong>Daily Variation:</strong> Short-term temperature changes with autocorrelation to simulate weather persistence</li>
            </ul>
        </div>

        <div class="card">
            <div class="card-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M16.5 12c-1.8 2.6-4.7 4.5-8 4.5-2 0-3.8-.7-5.2-1.8 1-1 1.6-1.7 1.6-2.7s-.6-1.7-1.6-2.7C4.7 8 6.5 7.5 8.5 7.5c3.3 0 6.2 1.9 8 4.5z"/>
                    <path d="M16.5 12 21 8.5v7z"/><circle cx="8" cy="11" r="1"/>
                </svg>
            </div>
            <h2>Ecosystem Structure</h2>
            <p>The simulation models a complete marine food web with:</p>
            <ul>
                <li><strong>27 Marine Species</strong> across 3 ecological tiers (producers, herbivores, carnivores)</li>
                <li><strong>3 Thermal Variants</strong> per species representing different temperature adaptations</li>
                <li><strong>Realistic Population Dynamics</strong> based on predator-prey interactions</li>
                <li><strong>Temperature-Dependent Performance</strong> using Arrhenius equations</li>
            </ul>
        </div>

        <div class="card">
            <div class="card-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <circle cx="12" cy="12" r="3"/>
                    <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>
                </svg>
            </div>
            <h2>How It Works</h2>

            <h3>Thermal Performance Calculations</h3>
            <p>Each species has an optimal temperature range. Performance is calculated using the Arrhenius equation, which models how biological rates change with temperature. Species perform best at their optimal temperature and experience reduced efficiency in warmer or cooler conditions.</p>

            <h3>Feeding Dynamics</h3>
            <p>Predator-prey relationships are modeled with realistic feeding mechanics:</p>
            <ul>
                <li>Herbivores consume producers based on availability and temperature performance</li>
                <li>Carnivores hunt herbivores with efficiency affected by environmental conditions</li>
                <li>Population sizes fluctuate based on food availability and reproductive success</li>
            </ul>

            <h3>Population Tracking</h3>
            <p>The simulation tracks population counts for each species and thermal variant over time, generating CSV data for scientific analysis. This allows researchers to observe how different species adapt (or fail to adapt) to changing temperature conditions.</p>
        </div>

        <div class="about-band cta">
            <h2>Run It Yourself</h2>
            <p>Download the desktop simulation and start generating your own marine ecosystem datasets.</p>
            <a href="/" class="btn-primary">Get the Simulation &rarr;</a>
        </div>
    </div>
</section>

<?php include __DIR__ . '/../includes/footer.php'; ?>
