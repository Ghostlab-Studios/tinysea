<?php
$current_page = 'about';
$page_title = 'About - Tiny Sea Simulation';
include __DIR__ . '/../includes/header.php';
?>

<section class="about-page">
    <div class="page-hero">
        <h1>About Tiny Sea Simulation</h1>
        <p class="subtitle">A Scientific Research Platform for Marine Ecosystem Dynamics</p>
    </div>
    
    <div class="content-section">
        <div class="card">
            <div class="card-icon">🔬</div>
            <h2>Research Purpose</h2>
            <p>Tiny Sea Simulation is a scientific research platform developed in collaboration with marine biologist Brian Helmuth to study climate change impacts on marine food webs. This simulation has evolved from a temperature-based management game into a sophisticated tool for generating research data on ecosystem dynamics.</p>
        </div>
        
        <div class="card">
            <div class="card-icon">🌡️</div>
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
            <div class="card-icon">🐠</div>
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
            <div class="card-icon">⚙️</div>
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
    </div>
</section>

<?php include __DIR__ . '/../includes/footer.php'; ?>
