<?php
$current_page = 'simulation';
$page_title = 'Tiny Sea Simulation - Marine Ecosystem Research Platform';

// Version handling. v1 is always the default. An optional ?v=N serves builds/vN/
// when that folder exists; otherwise we fall back to v1 and flag a notice.
$requestedV = preg_replace('/[^0-9]/', '', (string)($_GET['v'] ?? '1'));
if ($requestedV === '') { $requestedV = '1'; }

if (is_dir(__DIR__ . '/builds/v' . $requestedV)) {
    $versionNum = $requestedV;
    $versionFallback = false;
} else {
    $versionNum = '1';                       // default
    $versionFallback = ($requestedV !== '1'); // only notify if a non-default was asked for
}

include __DIR__ . '/includes/header.php';
?>

<section class="about-page">
    <?php if ($versionFallback): ?>
    <div class="version-notice" role="status">
        Version <?php echo htmlspecialchars($requestedV, ENT_QUOTES, 'UTF-8'); ?> isn't available, so you're seeing the default (version 1).
    </div>
    <?php endif; ?>

    <div class="page-hero">
        <div class="hero-copy">
            <p class="eyebrow">Marine Ecosystem Research Platform</p>
            <h1>Tiny Sea Simulation</h1>
            <p class="hero-text">Model how climate change reshapes marine food webs. Run multi-year ecosystem scenarios, track species across three trophic tiers, and export rich CSV datasets for analysis. It all runs on your own machine.</p>
            <div class="download-buttons">
                <a href="/builds/download.php?file=windows&amp;v=<?php echo $versionNum; ?>" class="download-btn windows">
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true"><path d="M3 12V6.75l8-1.25V12H3zm0 .5h8v6.5l-8-1.25V12.5zM11.5 5.35l9.5-1.6V12h-9.5V5.35zM11.5 12.5H21v6.25l-9.5 1.6V12.5z"/></svg>
                    Windows
                </a>
                <a href="/builds/download.php?file=macos&amp;v=<?php echo $versionNum; ?>" class="download-btn macos">
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true"><path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.8-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M13 3.5c.73-.83 1.94-1.46 2.94-1.5.13 1.17-.34 2.35-1.04 3.19-.69.85-1.83 1.51-2.95 1.42-.15-1.15.41-2.35 1.05-3.11z"/></svg>
                    macOS
                </a>
            </div>
            <p class="download-meta">Windows 10/11 (64-bit) &middot; macOS 11 Big Sur or later</p>
            <div class="setup-guide-link">
                <a href="/builds/download.php?file=macos-guide&amp;v=<?php echo $versionNum; ?>">macOS Setup Guide (PDF)</a>
            </div>
        </div>
        <div class="hero-art">
            <img class="scene" src="/assets/images/ocean-hero.jpg" alt="Illustration of the Tiny Sea underwater ecosystem" width="1200" height="675" loading="lazy">
            <img class="hero-creature c1" src="/assets/images/creatures/hexapod-c.png" alt="" aria-hidden="true">
            <img class="hero-creature c2" src="/assets/images/creatures/hexapod-a.png" alt="" aria-hidden="true">
            <img class="hero-creature c3" src="/assets/images/creatures/hexapod-t.png" alt="" aria-hidden="true">
            <img class="hero-creature c4" src="/assets/images/creatures/gelgi-a.png" alt="" aria-hidden="true">
            <img class="hero-creature c5" src="/assets/images/creatures/sheplik.png" alt="" aria-hidden="true">
            <img class="hero-creature c6" src="/assets/images/creatures/grabbler.png" alt="" aria-hidden="true">
            <img class="hero-creature c7" src="/assets/images/creatures/sploof.png" alt="" aria-hidden="true">
        </div>
    </div>

    <div class="content-section">
        <div class="card">
            <div class="card-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M9 6h11"/><path d="M9 12h11"/><path d="M9 18h11"/>
                    <circle cx="4" cy="6" r="1.6"/><circle cx="4" cy="12" r="1.6"/><circle cx="4" cy="18" r="1.6"/>
                </svg>
            </div>
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

        <div class="about-band">
            <div class="card-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M2 7c2 1.6 3.3 1.6 5 0s3.3-1.6 5 0 3.3 1.6 5 0 3.3-1.6 5 0"/>
                    <path d="M2 13c2 1.6 3.3 1.6 5 0s3.3-1.6 5 0 3.3 1.6 5 0 3.3-1.6 5 0"/>
                    <path d="M2 19c2 1.6 3.3 1.6 5 0s3.3-1.6 5 0 3.3 1.6 5 0 3.3-1.6 5 0"/>
                </svg>
            </div>
            <h2>About This Simulation</h2>
            <p>Tiny Sea is a scientific research platform developed in collaboration with marine biologist Brian Helmuth to study climate-change impacts on marine food webs. It models a complete ecosystem with realistic temperature dynamics and predator-prey interactions, generating data on how species adapt (or fail to adapt) to changing conditions.</p>
            <div class="data-grid">
                <div class="data-category">
                    <h3>Ecosystem</h3>
                    <ul>
                        <li>Marine species across 3 tiers</li>
                        <li>Thermal variants per species</li>
                        <li>Predator-prey dynamics</li>
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
