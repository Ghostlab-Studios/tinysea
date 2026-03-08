<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title><?php echo htmlspecialchars(isset($page_title) ? $page_title : 'Tiny Sea Simulation', ENT_QUOTES, 'UTF-8'); ?></title>
    <link rel="stylesheet" href="/style.css">
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Montserrat:wght@400;500;600;700&family=Open+Sans:wght@400;600&display=swap" rel="stylesheet">
</head>
<body>
    <header>
        <div class="wave-animation"></div>
        <div class="container">
            <div class="logo">
                <img src="/assets/images/logo.png" alt="Tiny Sea Logo" height="55">
                <div class="logo-text">
                    <div class="logo-main">Tiny Sea</div>
                    <div class="logo-subtitle">Simulation</div>
                </div>
            </div>
            <nav>
                <a href="https://playtinysea.com/" <?php echo (isset($current_page) && $current_page === 'game') ? 'class="active"' : ''; ?>>Game</a>
                <a href="/" <?php echo (isset($current_page) && $current_page === 'simulation') ? 'class="active"' : ''; ?>>Simulation</a>
                <a href="/about/" <?php echo (isset($current_page) && $current_page === 'about') ? 'class="active"' : ''; ?>>About</a>
            </nav>
        </div>
    </header>
    <main>
