<?php
/**
 * GET /api/download_page.php?session=...
 * Lists all batches in a session with individual download buttons,
 * per-batch progress bars, ETA, cancel/retry support.
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

function formatSize($bytes) {
    if ($bytes >= 1073741824) return round($bytes / 1073741824, 2) . ' GB';
    if ($bytes >= 1048576) return round($bytes / 1048576, 1) . ' MB';
    if ($bytes >= 1024) return round($bytes / 1024, 1) . ' KB';
    return $bytes . ' B';
}
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
        .container { max-width: 780px; margin: 0 auto; }
        h1 { font-size: 1.5rem; margin-bottom: 0.5rem; color: #7eb8da; }
        .subtitle { color: #8899aa; margin-bottom: 1.5rem; font-size: 0.9rem; }

        /* Overall progress */
        .overall-section {
            background: #1a2a3a;
            border-radius: 8px;
            padding: 1.2rem;
            margin-bottom: 1.5rem;
        }
        .overall-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; }
        .overall-bar-bg {
            background: #0d1926;
            border-radius: 4px;
            height: 20px;
            overflow: hidden;
        }
        .overall-bar-fill {
            background: linear-gradient(90deg, #2a7ab5, #4ac0e0);
            height: 100%;
            width: 0%;
            transition: width 0.4s ease;
            border-radius: 4px;
        }
        .overall-text { font-size: 0.85rem; color: #8899aa; margin-top: 0.4rem; }

        /* Batch list */
        .batch-list { list-style: none; }
        .batch-item {
            background: #1a2a3a;
            border-radius: 8px;
            margin-bottom: 0.6rem;
            overflow: hidden;
            transition: all 0.3s ease;
        }
        .batch-header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 0.9rem 1rem;
        }
        .batch-left { flex: 1; }
        .batch-name { font-weight: 600; font-size: 0.95rem; }
        .batch-meta { color: #8899aa; font-size: 0.8rem; margin-top: 0.15rem; }
        .batch-actions { display: flex; gap: 0.5rem; align-items: center; }

        /* Buttons */
        .btn-download {
            background: linear-gradient(135deg, #2a7ab5, #1a5a8a);
            color: white;
            border: none;
            padding: 0.45rem 1rem;
            border-radius: 5px;
            font-size: 0.82rem;
            cursor: pointer;
            transition: background 0.2s;
        }
        .btn-download:hover { background: linear-gradient(135deg, #3a8ac5, #2a6a9a); }
        .btn-cancel {
            background: transparent;
            color: #e04a4a;
            border: 1px solid #6a2a2a;
            padding: 0.45rem 0.8rem;
            border-radius: 5px;
            font-size: 0.82rem;
            cursor: pointer;
        }
        .btn-cancel:hover { background: #3a1a1a; }
        .btn-retry {
            background: transparent;
            color: #e0a040;
            border: 1px solid #6a5a2a;
            padding: 0.45rem 1rem;
            border-radius: 5px;
            font-size: 0.82rem;
            cursor: pointer;
        }
        .btn-retry:hover { background: #3a2a1a; }
        .btn-done {
            background: #1a3a2a;
            color: #4ae080;
            border: 1px solid #2a6a4a;
            padding: 0.45rem 1rem;
            border-radius: 5px;
            font-size: 0.82rem;
            cursor: default;
        }
        .btn-all {
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
        .btn-all:hover { background: linear-gradient(135deg, #3a8ac5, #2a6a9a); }
        .btn-all:disabled { opacity: 0.5; cursor: not-allowed; }

        /* Progress detail (expanded when downloading) */
        .batch-progress {
            padding: 0 1rem 0.9rem 1rem;
            display: none;
        }
        .batch-progress.visible { display: block; }
        .prog-bar-bg {
            background: #0d1926;
            border-radius: 3px;
            height: 8px;
            overflow: hidden;
            margin-bottom: 0.4rem;
        }
        .prog-bar-fill {
            height: 100%;
            width: 0%;
            border-radius: 3px;
            transition: width 0.3s ease;
        }
        .prog-bar-fill.active { background: linear-gradient(90deg, #2a7ab5, #4ac0e0); }
        .prog-bar-fill.error { background: #e04a4a; }
        .prog-bar-fill.done { background: #4ae080; }
        .prog-stats {
            display: flex;
            justify-content: space-between;
            font-size: 0.75rem;
            color: #8899aa;
        }
        .prog-stats .speed { color: #6899bb; }
        .prog-stats .eta { color: #aabb99; }
    </style>
</head>
<body>
<div class="container">
    <h1>TinySea Simulation Results</h1>
    <p class="subtitle"><?= $totalBatches ?> batches, <?= $totalFiles ?> files total</p>

    <div class="overall-section">
        <div class="overall-header">
            <span id="overallLabel">0 of <?= $totalBatches ?> batches downloaded</span>
            <span id="overallCount">0 / <?= $totalBatches ?></span>
        </div>
        <div class="overall-bar-bg">
            <div class="overall-bar-fill" id="overallBar"></div>
        </div>
        <div class="overall-text" id="overallText">Click a batch to download, or "Download All" for everything.</div>
    </div>

    <ul class="batch-list">
    <?php foreach ($batches as $name => $info): ?>
        <li class="batch-item" id="batch-<?= htmlspecialchars($name) ?>">
            <div class="batch-header">
                <div class="batch-left">
                    <div class="batch-name"><?= htmlspecialchars($name) ?></div>
                    <div class="batch-meta"><?= $info['count'] ?> files, <?= formatSize($info['size']) ?></div>
                </div>
                <div class="batch-actions" id="actions-<?= htmlspecialchars($name) ?>">
                    <button class="btn-download" onclick="downloadBatch('<?= htmlspecialchars($name) ?>')">Download</button>
                </div>
            </div>
            <div class="batch-progress" id="progress-<?= htmlspecialchars($name) ?>">
                <div class="prog-bar-bg">
                    <div class="prog-bar-fill active" id="bar-<?= htmlspecialchars($name) ?>"></div>
                </div>
                <div class="prog-stats">
                    <span id="downloaded-<?= htmlspecialchars($name) ?>">0 B / 0 B</span>
                    <span class="speed" id="speed-<?= htmlspecialchars($name) ?>"></span>
                    <span class="eta" id="eta-<?= htmlspecialchars($name) ?>"></span>
                </div>
            </div>
        </li>
    <?php endforeach; ?>
    </ul>

    <button class="btn-all" id="downloadAllBtn" onclick="downloadAll()">Download All</button>
</div>

<script>
const session = <?= json_encode($session) ?>;
const batchData = <?= json_encode($batches) ?>;
const batchNames = Object.keys(batchData).sort();
const controllers = {};  // AbortController per batch
let overallDone = 0;

function formatBytes(bytes) {
    if (bytes >= 1073741824) return (bytes / 1073741824).toFixed(2) + ' GB';
    if (bytes >= 1048576) return (bytes / 1048576).toFixed(1) + ' MB';
    if (bytes >= 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return bytes + ' B';
}

function formatTime(seconds) {
    if (seconds < 0 || !isFinite(seconds)) return '--';
    if (seconds < 60) return Math.ceil(seconds) + 's';
    const m = Math.floor(seconds / 60);
    const s = Math.ceil(seconds % 60);
    return m + 'm ' + s + 's';
}

function setActions(batch, html) {
    document.getElementById('actions-' + batch).innerHTML = html;
}

function updateOverall() {
    const pct = Math.round((overallDone / batchNames.length) * 100);
    document.getElementById('overallBar').style.width = pct + '%';
    document.getElementById('overallCount').textContent = overallDone + ' / ' + batchNames.length;
    document.getElementById('overallLabel').textContent = overallDone + ' of ' + batchNames.length + ' batches downloaded';
    if (overallDone === batchNames.length) {
        document.getElementById('overallText').textContent = 'All batches downloaded successfully!';
    }
}

async function downloadBatch(batch) {
    const progressEl = document.getElementById('progress-' + batch);
    const barEl = document.getElementById('bar-' + batch);
    const downloadedEl = document.getElementById('downloaded-' + batch);
    const speedEl = document.getElementById('speed-' + batch);
    const etaEl = document.getElementById('eta-' + batch);

    // Show progress section
    progressEl.classList.add('visible');
    barEl.className = 'prog-bar-fill active';
    barEl.style.width = '0%';
    downloadedEl.textContent = '0 B';
    speedEl.textContent = '';
    etaEl.textContent = '';

    // Set cancel button
    setActions(batch,
        '<button class="btn-cancel" onclick="cancelBatch(\'' + batch + '\')">Cancel</button>'
    );

    // Create abort controller
    const controller = new AbortController();
    controllers[batch] = controller;

    const url = '/api/download.php?session=' + encodeURIComponent(session) +
                '&batch=' + encodeURIComponent(batch);

    try {
        const response = await fetch(url, { signal: controller.signal });
        if (!response.ok) throw new Error('Server returned ' + response.status);

        const contentLength = response.headers.get('Content-Length');
        const totalBytes = contentLength ? parseInt(contentLength, 10) : 0;
        const reader = response.body.getReader();
        const chunks = [];
        let received = 0;
        const startTime = Date.now();

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            chunks.push(value);
            received += value.length;

            // Update progress
            const elapsed = (Date.now() - startTime) / 1000;
            const speed = elapsed > 0 ? received / elapsed : 0;

            if (totalBytes > 0) {
                const pct = Math.min(100, Math.round((received / totalBytes) * 100));
                barEl.style.width = pct + '%';
                downloadedEl.textContent = formatBytes(received) + ' / ' + formatBytes(totalBytes);
                const remaining = (totalBytes - received) / speed;
                etaEl.textContent = 'ETA: ' + formatTime(remaining);
            } else {
                // No content-length — show indeterminate progress
                downloadedEl.textContent = formatBytes(received);
                etaEl.textContent = '';
            }
            speedEl.textContent = formatBytes(Math.round(speed)) + '/s';
        }

        // Download complete — trigger file save
        const blob = new Blob(chunks);
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = batch + '.zip';
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(a.href);

        // Mark done
        barEl.className = 'prog-bar-fill done';
        barEl.style.width = '100%';
        downloadedEl.textContent = formatBytes(received);
        speedEl.textContent = '';
        etaEl.textContent = 'Complete';
        setActions(batch, '<span class="btn-done">Downloaded</span>');

        overallDone++;
        updateOverall();
        return true;

    } catch (e) {
        if (e.name === 'AbortError') {
            barEl.className = 'prog-bar-fill error';
            downloadedEl.textContent = 'Cancelled';
            speedEl.textContent = '';
            etaEl.textContent = '';
            setActions(batch,
                '<button class="btn-retry" onclick="downloadBatch(\'' + batch + '\')">Retry</button>'
            );
        } else {
            barEl.className = 'prog-bar-fill error';
            barEl.style.width = '100%';
            downloadedEl.textContent = 'Error: ' + e.message;
            speedEl.textContent = '';
            etaEl.textContent = '';
            setActions(batch,
                '<button class="btn-retry" onclick="downloadBatch(\'' + batch + '\')">Retry</button>'
            );
            console.error('Failed to download ' + batch + ':', e);
        }
        return false;
    } finally {
        delete controllers[batch];
    }
}

function cancelBatch(batch) {
    if (controllers[batch]) {
        controllers[batch].abort();
    }
}

async function downloadAll() {
    const btn = document.getElementById('downloadAllBtn');
    btn.disabled = true;
    btn.textContent = 'Downloading...';
    overallDone = 0;
    updateOverall();
    document.getElementById('overallText').textContent = 'Downloading all batches sequentially...';

    for (const batch of batchNames) {
        // Skip already downloaded
        const actionsHtml = document.getElementById('actions-' + batch).innerHTML;
        if (actionsHtml.includes('Downloaded')) {
            overallDone++;
            updateOverall();
            continue;
        }
        await downloadBatch(batch);
    }

    btn.disabled = false;
    btn.textContent = 'Download All';
}
</script>
</body>
</html>
