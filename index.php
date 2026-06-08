<?php
$current_page = 'simulation';
$page_title = 'Tiny Sea Simulation - Marine Ecosystem Research Platform';

// Active build version (whitelisted; default v1). Controls which build the
// download links serve (builds/v1/ or builds/v2/).
$version    = (($_GET['v'] ?? '1') === '2') ? 'v2' : 'v1';
$versionNum = ($version === 'v2') ? '2' : '1';

include __DIR__ . '/includes/header.php';
?>

<section class="about-page">
    <div class="page-hero">
        <h1>Tiny Sea Simulation</h1>
        <p class="subtitle">A Scientific Research Platform for Marine Ecosystem Dynamics</p>
        <p class="hero-text">Model how climate change reshapes marine food webs. Run multi-year ecosystem scenarios, track species across three trophic tiers, and export rich CSV datasets for analysis &mdash; all on your own machine.</p>
        <div class="hero-cta">
            <a href="#download" class="btn-primary">Download the Simulation &darr;</a>
        </div>
    </div>

    <div class="content-section">
        <div class="card download-card" id="download">
            <div class="card-icon">&#11015;&#65039;</div>
            <h2>Download</h2>
            <p>Run Tiny Sea locally for long, high-performance simulation runs. Choose your platform:</p>
            <div class="download-buttons">
                <a href="/builds/download.php?file=windows&amp;v=<?php echo $versionNum; ?>" class="download-btn windows">
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor"><path d="M3 12V6.75l8-1.25V12H3zm0 .5h8v6.5l-8-1.25V12.5zM11.5 5.35l9.5-1.6V12h-9.5V5.35zM11.5 12.5H21v6.25l-9.5 1.6V12.5z"/></svg>
                    Windows
                </a>
                <a href="/builds/download.php?file=macos&amp;v=<?php echo $versionNum; ?>" class="download-btn macos">
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor"><path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.8-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M13 3.5c.73-.83 1.94-1.46 2.94-1.5.13 1.17-.34 2.35-1.04 3.19-.69.85-1.83 1.51-2.95 1.42-.15-1.15.41-2.35 1.05-3.11z"/></svg>
                    macOS
                </a>
            </div>
            <p class="download-meta">Windows 10/11 (64-bit) &middot; macOS 11 Big Sur or later</p>
            <div class="setup-guide-link">
                <a href="/builds/download.php?file=macos-guide&amp;v=<?php echo $versionNum; ?>">&#128196; macOS Setup Guide (PDF)</a>
            </div>
        </div>

        <div class="card">
            <div class="card-icon">&#128640;</div>
            <h2>Getting Started</h2>
            <div class="flowchart">
                <div class="flow-step">
                    <div class="step-number">1</div>
                    <div class="step-content">
                        <strong>Download</strong>
                        <p>Grab the Windows or macOS build above.</p>
                    </div>
                </div>
                <div class="flow-step">
                    <div class="step-number">2</div>
                    <div class="step-content">
                        <strong>Unzip</strong>
                        <p>Extract the downloaded archive to a folder of your choice.</p>
                    </div>
                </div>
                <div class="flow-step">
                    <div class="step-number">3</div>
                    <div class="step-content">
                        <strong>Run</strong>
                        <p>Launch the app. On macOS, follow the setup guide to allow the app on first open.</p>
                    </div>
                </div>
                <div class="flow-step">
                    <div class="step-number">4</div>
                    <div class="step-content">
                        <strong>Simulate &amp; Export</strong>
                        <p>Configure scenarios, run them, and export CSV data for analysis in R, Python, or Excel.</p>
                    </div>
                </div>
            </div>
            <div class="note">macOS users: because the app is distributed outside the App Store, you'll need to allow it in System Settings on first launch. The setup guide walks you through it step by step.</div>
        </div>

        <div class="card">
            <div class="card-icon">&#127754;</div>
            <h2>About This Simulation</h2>
            <p>Tiny Sea is a scientific research platform developed in collaboration with marine biologist Brian Helmuth to study climate-change impacts on marine food webs. It models a complete ecosystem with realistic temperature dynamics and predator&ndash;prey interactions, generating data on how species adapt &mdash; or fail to adapt &mdash; to changing conditions.</p>
            <div class="data-grid">
                <div class="data-category">
                    <h3>Ecosystem</h3>
                    <ul>
                        <li>Marine species across 3 tiers</li>
                        <li>Thermal variants per species</li>
                        <li>Predator&ndash;prey dynamics</li>
                        <li>Arrhenius thermal performance</li>
                    </ul>
                </div>
                <div class="data-category">
                    <h3>Temperature Model</h3>
                    <ul>
                        <li>Seasonal cycles</li>
                        <li>Climate warming trends</li>
                        <li>Interannual variation</li>
                        <li>Daily weather persistence</li>
                    </ul>
                </div>
                <div class="data-category">
                    <h3>Research Output</h3>
                    <ul>
                        <li>Per-day population data</li>
                        <li>Per-species metrics</li>
                        <li>Aggregate &amp; bulk summaries</li>
                        <li>R-compatible CSV export</li>
                    </ul>
                </div>
            </div>
            <a href="/about/" class="btn-primary">Learn More &rarr;</a>
        </div>
    </div>
</section>

<?php include __DIR__ . '/includes/footer.php'; ?>
