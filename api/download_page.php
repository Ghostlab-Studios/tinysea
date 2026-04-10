<?php
/**
 * GET /api/download_page.php?session=...
 * Lists all batches in a session and provides per-batch download buttons
 * with a progress bar. Much more reliable than one giant ZIP.
 */
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/s3.php';

$session = $_GET['session'] ?? '';
if (!validate_session_id($session)) {
    http_response_code(400);
    echo 'Invalid session ID';
    exit;
}

$s3 = new S3Client(AWS_ACCESS_KEY, AWS_SECRET_KEY, AWS_REGION, S3_BUCKET);
$prefix = SESSION_PREFIX . $session . '/';
$objects = $s3->listObjects($prefix);

if (empty($objects)) {
    http_response_code(404);
    echo 'No files found for this session';
    exit;
}

// Group files by batch folder
$batches = [];
foreach ($objects as $obj) {
    $rel = substr($obj['Key'], strlen($prefix));
    if (empty($rel)) continue;
    $parts = explode('/', $rel, 2);
    $batchName = $parts[0];
    if (!isset($batches[$batchName])) {
        $batches[$batchName] = ['count' => 0, 'size' => 0];
    }
    $batches[$batchName]['count']++;
    $batches[$batchName]['size'] += $obj['Size'];
}
ksort($batches);
$totalBatches = count($batches);
$totalFiles = array_sum(array_column($batches, 'count'));
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>TinySea — Download Results</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: #0a1628;
            color: #e0e8f0;
            min-height: 100vh;
            padding: 2rem;
        }
        .container { max-width: 720px; margin: 0 auto; }
        h1 { font-size: 1.5rem; margin-bottom: 0.5rem; color: #7eb8da; }
        .subtitle { color: #8899aa; margin-bottom: 1.5rem; font-size: 0.9rem; }
        .progress-section {
            background: #1a2a3a;
            border-radius: 8px;
            padding: 1.2rem;
            margin-bottom: 1.5rem;
        }
        .progress-bar-bg {
            background: #0d1926;
            border-radius: 4px;
            height: 24px;
            overflow: hidden;
            margin-top: 0.5rem;
        }
        .progress-bar-fill {
            background: linear-gradient(90deg, #2a7ab5, #4ac0e0);
            height: 100%;
            width: 0%;
            transition: width 0.4s ease;
            border-radius: 4px;
        }
        .progress-text { font-size: 0.85rem; color: #8899aa; margin-top: 0.4rem; }
        .batch-list { list-style: none; }
        .batch-item {
            background: #1a2a3a;
            border-radius: 6px;
            padding: 0.8rem 1rem;
            margin-bottom: 0.5rem;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }
        .batch-name { font-weight: 600; }
        .batch-info { color: #8899aa; font-size: 0.8rem; }
        .batch-status {
            font-size: 0.8rem;
            padding: 0.3rem 0.7rem;
            border-radius: 4px;
            min-width: 90px;
            text-align: center;
        }
        .status-waiting { background: #1a2a3a; color: #667788; border: 1px solid #334455; }
        .status-downloading { background: #1a3a5a; color: #4ac0e0; border: 1px solid #2a6a8a; }
        .status-done { background: #1a3a2a; color: #4ae080; border: 1px solid #2a6a4a; }
        .status-error { background: #3a1a1a; color: #e04a4a; border: 1px solid #6a2a2a; }
        .btn {
            background: linear-gradient(135deg, #2a7ab5, #1a5a8a);
            color: white;
            border: none;
            padding: 0.7rem 1.5rem;
            border-radius: 6px;
            font-size: 0.95rem;
            cursor: pointer;
            margin-top: 1rem;
            display: inline-block;
        }
        .btn:hover { background: linear-gradient(135deg, #3a8ac5, #2a6a9a); }
        .btn:disabled { opacity: 0.5; cursor: not-allowed; }
    </style>
</head>
<body>
<div class="container">
    <h1>TinySea Simulation Results</h1>
    <p class="subtitle"><?= $totalBatches ?> batches, <?= $totalFiles ?> files total</p>

    <div class="progress-section">
        <div style="display:flex;justify-content:space-between;align-items:center;">
            <span id="progressLabel">Ready to download</span>
            <span id="progressCount">0 / <?= $totalBatches ?></span>
        </div>
        <div class="progress-bar-bg">
            <div class="progress-bar-fill" id="progressBar"></div>
        </div>
        <div class="progress-text" id="progressText">Click "Download All" to start</div>
    </div>

    <ul class="batch-list" id="batchList">
    <?php foreach ($batches as $name => $info): ?>
        <li class="batch-item" data-batch="<?= htmlspecialchars($name) ?>">
            <div>
                <div class="batch-name"><?= htmlspecialchars($name) ?></div>
                <div class="batch-info"><?= $info['count'] ?> files, <?= formatSize($info['size']) ?></div>
            </div>
            <span class="batch-status status-waiting" id="status-<?= htmlspecialchars($name) ?>">Waiting</span>
        </li>
    <?php endforeach; ?>
    </ul>

    <button class="btn" id="downloadAllBtn" onclick="downloadAll()">Download All</button>
</div>

<script>
const session = <?= json_encode($session) ?>;
const batches = <?= json_encode(array_keys($batches)) ?>;
let completed = 0;
let downloading = false;

async function downloadAll() {
    if (downloading) return;
    downloading = true;
    completed = 0;
    document.getElementById('downloadAllBtn').disabled = true;
    document.getElementById('progressBar').style.width = '0%';
    document.getElementById('progressCount').textContent = '0 / ' + batches.length;
    document.getElementById('progressLabel').textContent = 'Downloading...';
    document.getElementById('progressText').textContent = 'Downloading batch 1 of ' + batches.length + '...';
    // Reset all batch statuses
    batches.forEach(b => {
        const el = document.getElementById('status-' + b);
        el.textContent = 'Waiting';
        el.className = 'batch-status status-waiting';
    });

    for (let i = 0; i < batches.length; i++) {
        const batch = batches[i];
        const statusEl = document.getElementById('status-' + batch);
        statusEl.textContent = 'Downloading';
        statusEl.className = 'batch-status status-downloading';
        document.getElementById('progressText').textContent =
            'Downloading ' + batch + ' (' + (i + 1) + ' of ' + batches.length + ')...';

        try {
            const url = '/api/download.php?session=' + encodeURIComponent(session) +
                        '&batch=' + encodeURIComponent(batch);

            const response = await fetch(url);
            if (!response.ok) throw new Error('HTTP ' + response.status);

            const blob = await response.blob();
            const a = document.createElement('a');
            a.href = URL.createObjectURL(blob);
            a.download = batch + '.zip';
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(a.href);

            statusEl.textContent = 'Downloaded';
            statusEl.className = 'batch-status status-done';
        } catch (e) {
            statusEl.textContent = 'Error';
            statusEl.className = 'batch-status status-error';
            console.error('Failed to download ' + batch + ':', e);
        }

        completed++;
        const pct = Math.round((completed / batches.length) * 100);
        document.getElementById('progressBar').style.width = pct + '%';
        document.getElementById('progressCount').textContent = completed + ' / ' + batches.length;
    }

    document.getElementById('progressLabel').textContent = 'Complete';
    document.getElementById('progressText').textContent =
        'All ' + batches.length + ' batches downloaded.';
    document.getElementById('downloadAllBtn').disabled = false;
    document.getElementById('downloadAllBtn').textContent = 'Download All Again';
    downloading = false;
}
</script>
</body>
</html>
<?php
function formatSize($bytes) {
    if ($bytes >= 1048576) return round($bytes / 1048576, 1) . ' MB';
    if ($bytes >= 1024) return round($bytes / 1024, 1) . ' KB';
    return $bytes . ' B';
}
?>
