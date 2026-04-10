<?php
/**
 * GET /api/download_page.php?session=...
 * Lists all batches with individual download buttons, per-file progress,
 * cancel/retry support. ZIPs are built client-side via JSZip so the
 * server never needs to hold more than one file at a time.
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
    <script src="https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js"></script>
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

        .overall-section {
            background: #1a2a3a; border-radius: 8px;
            padding: 1.2rem; margin-bottom: 1.5rem;
        }
        .overall-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; }
        .bar-bg {
            background: #0d1926; border-radius: 4px;
            height: 20px; overflow: hidden;
        }
        .bar-fill {
            height: 100%; width: 0%; border-radius: 4px;
            transition: width 0.3s ease;
        }
        .bar-fill.blue { background: linear-gradient(90deg, #2a7ab5, #4ac0e0); }
        .bar-fill.green { background: #4ae080; }
        .bar-fill.red { background: #e04a4a; }
        .overall-text { font-size: 0.85rem; color: #8899aa; margin-top: 0.4rem; }

        .batch-list { list-style: none; }
        .batch-item {
            background: #1a2a3a; border-radius: 8px;
            margin-bottom: 0.6rem; overflow: hidden;
        }
        .batch-header {
            display: flex; align-items: center;
            justify-content: space-between; padding: 0.9rem 1rem;
        }
        .batch-left { flex: 1; }
        .batch-name { font-weight: 600; font-size: 0.95rem; }
        .batch-meta { color: #8899aa; font-size: 0.8rem; margin-top: 0.15rem; }

        /* Progress detail */
        .batch-detail {
            padding: 0 1rem 0.9rem 1rem;
            display: none;
        }
        .batch-detail.visible { display: block; }
        .detail-bar-bg {
            background: #0d1926; border-radius: 3px;
            height: 8px; overflow: hidden; margin-bottom: 0.4rem;
        }
        .detail-bar-fill {
            height: 100%; width: 0%; border-radius: 3px;
            transition: width 0.2s ease;
        }
        .detail-bar-fill.blue { background: linear-gradient(90deg, #2a7ab5, #4ac0e0); }
        .detail-bar-fill.green { background: #4ae080; }
        .detail-bar-fill.red { background: #e04a4a; }
        .detail-stats {
            display: flex; justify-content: space-between;
            font-size: 0.75rem; color: #8899aa;
        }
        .detail-stats .speed { color: #6899bb; }
        .detail-stats .eta { color: #aabb99; }
        .detail-stats .files { color: #aa99bb; }

        /* Buttons */
        .btn {
            border: none; padding: 0.45rem 1rem; border-radius: 5px;
            font-size: 0.82rem; cursor: pointer; transition: all 0.2s;
        }
        .btn-download {
            background: linear-gradient(135deg, #2a7ab5, #1a5a8a); color: white;
        }
        .btn-download:hover { background: linear-gradient(135deg, #3a8ac5, #2a6a9a); }
        .btn-cancel {
            background: transparent; color: #e04a4a; border: 1px solid #6a2a2a;
        }
        .btn-cancel:hover { background: #3a1a1a; }
        .btn-retry {
            background: transparent; color: #e0a040; border: 1px solid #6a5a2a;
        }
        .btn-retry:hover { background: #3a2a1a; }
        .btn-done {
            background: #1a3a2a; color: #4ae080; border: 1px solid #2a6a4a; cursor: default;
        }
        .btn-all {
            background: linear-gradient(135deg, #2a7ab5, #1a5a8a); color: white;
            border: none; padding: 0.7rem 1.5rem; border-radius: 6px;
            font-size: 0.95rem; cursor: pointer; margin-top: 1rem;
        }
        .btn-all:hover { background: linear-gradient(135deg, #3a8ac5, #2a6a9a); }
        .btn-all:disabled { opacity: 0.5; cursor: not-allowed; }
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
        <div class="bar-bg"><div class="bar-fill blue" id="overallBar"></div></div>
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
                <div id="actions-<?= htmlspecialchars($name) ?>">
                    <button class="btn btn-download" onclick="downloadBatch('<?= htmlspecialchars($name) ?>')">Download</button>
                </div>
            </div>
            <div class="batch-detail" id="detail-<?= htmlspecialchars($name) ?>">
                <div class="detail-bar-bg">
                    <div class="detail-bar-fill blue" id="bar-<?= htmlspecialchars($name) ?>"></div>
                </div>
                <div class="detail-stats">
                    <span id="size-<?= htmlspecialchars($name) ?>">0 B / 0 B</span>
                    <span class="files" id="files-<?= htmlspecialchars($name) ?>"></span>
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
const batchInfo = <?= json_encode($batches) ?>;
const batchNames = Object.keys(batchInfo).sort();
const abortControllers = {};
let overallDone = 0;
const PARALLEL = 4; // download 4 files at a time within a batch

function fmtBytes(b) {
    if (b >= 1073741824) return (b / 1073741824).toFixed(2) + ' GB';
    if (b >= 1048576) return (b / 1048576).toFixed(1) + ' MB';
    if (b >= 1024) return (b / 1024).toFixed(1) + ' KB';
    return b + ' B';
}
function fmtTime(s) {
    if (!isFinite(s) || s < 0) return '--';
    if (s < 60) return Math.ceil(s) + 's';
    return Math.floor(s/60) + 'm ' + Math.ceil(s%60) + 's';
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
        document.getElementById('overallBar').className = 'bar-fill green';
        document.getElementById('overallText').textContent = 'All batches downloaded!';
    }
}

async function downloadBatch(batch) {
    const detailEl = document.getElementById('detail-' + batch);
    const barEl = document.getElementById('bar-' + batch);
    const sizeEl = document.getElementById('size-' + batch);
    const filesEl = document.getElementById('files-' + batch);
    const speedEl = document.getElementById('speed-' + batch);
    const etaEl = document.getElementById('eta-' + batch);

    // Reset UI
    detailEl.classList.add('visible');
    barEl.className = 'detail-bar-fill blue';
    barEl.style.width = '0%';
    sizeEl.textContent = 'Fetching file list...';
    filesEl.textContent = '';
    speedEl.textContent = '';
    etaEl.textContent = '';
    setActions(batch, '<button class="btn btn-cancel" onclick="cancelBatch(\'' + batch + '\')">Cancel</button>');

    const controller = new AbortController();
    abortControllers[batch] = controller;

    try {
        // Step 1: Get file list
        const listResp = await fetch('/api/files.php?session=' + encodeURIComponent(session) +
            '&batch=' + encodeURIComponent(batch), { signal: controller.signal });
        if (!listResp.ok) throw new Error('Failed to list files: HTTP ' + listResp.status);
        const listData = await listResp.json();
        const files = listData.files;
        const totalSize = files.reduce((s, f) => s + f.size, 0);

        sizeEl.textContent = '0 B / ' + fmtBytes(totalSize);
        filesEl.textContent = '0 / ' + files.length + ' files';

        // Step 2: Download files in parallel batches and add to ZIP
        const zip = new JSZip();
        let downloaded = 0;
        let bytesReceived = 0;
        const startTime = Date.now();

        // Process files in parallel groups
        let fileIndex = 0;
        async function downloadNext() {
            while (fileIndex < files.length) {
                const i = fileIndex++;
                const f = files[i];
                const url = '/api/file.php?session=' + encodeURIComponent(session) +
                    '&batch=' + encodeURIComponent(batch) +
                    '&file=' + encodeURIComponent(f.name);

                const resp = await fetch(url, { signal: controller.signal });
                if (!resp.ok) throw new Error('Failed to download ' + f.name + ': HTTP ' + resp.status);
                const data = await resp.arrayBuffer();

                zip.file(f.name, data);
                downloaded++;
                bytesReceived += data.byteLength;

                // Update progress
                const pct = Math.round((bytesReceived / totalSize) * 100);
                barEl.style.width = pct + '%';
                sizeEl.textContent = fmtBytes(bytesReceived) + ' / ' + fmtBytes(totalSize);
                filesEl.textContent = downloaded + ' / ' + files.length + ' files';

                const elapsed = (Date.now() - startTime) / 1000;
                const speed = elapsed > 0 ? bytesReceived / elapsed : 0;
                speedEl.textContent = fmtBytes(Math.round(speed)) + '/s';
                const remaining = speed > 0 ? (totalSize - bytesReceived) / speed : 0;
                etaEl.textContent = 'ETA: ' + fmtTime(remaining);
            }
        }

        // Launch parallel workers
        const workers = [];
        for (let w = 0; w < PARALLEL; w++) {
            workers.push(downloadNext());
        }
        await Promise.all(workers);

        // Step 3: Generate ZIP and trigger download
        sizeEl.textContent = 'Creating ZIP...';
        speedEl.textContent = '';
        etaEl.textContent = '';

        const blob = await zip.generateAsync({ type: 'blob', compression: 'DEFLATE' });
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = batch + '.zip';
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(a.href);

        // Done
        barEl.className = 'detail-bar-fill green';
        barEl.style.width = '100%';
        sizeEl.textContent = fmtBytes(bytesReceived);
        filesEl.textContent = files.length + ' files';
        speedEl.textContent = '';
        etaEl.textContent = 'Complete';
        setActions(batch, '<span class="btn btn-done">Downloaded</span>');

        overallDone++;
        updateOverall();
        return true;

    } catch (e) {
        barEl.className = 'detail-bar-fill red';
        if (e.name === 'AbortError') {
            sizeEl.textContent = 'Cancelled';
        } else {
            barEl.style.width = '100%';
            sizeEl.textContent = 'Error: ' + e.message;
            console.error('Failed: ' + batch, e);
        }
        speedEl.textContent = '';
        etaEl.textContent = '';
        setActions(batch, '<button class="btn btn-retry" onclick="downloadBatch(\'' + batch + '\')">Retry</button>');
        return false;
    } finally {
        delete abortControllers[batch];
    }
}

function cancelBatch(batch) {
    if (abortControllers[batch]) abortControllers[batch].abort();
}

async function downloadAll() {
    const btn = document.getElementById('downloadAllBtn');
    btn.disabled = true;
    btn.textContent = 'Downloading...';
    overallDone = 0;
    document.getElementById('overallBar').className = 'bar-fill blue';
    updateOverall();
    document.getElementById('overallText').textContent = 'Downloading all batches...';

    for (const batch of batchNames) {
        // Skip already downloaded
        if (document.getElementById('actions-' + batch).innerHTML.includes('Downloaded')) {
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
