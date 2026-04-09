<?php
/**
 * GET /api/download.php?session=...&batch=...
 * Downloads a single batch folder as a ZIP from S3.
 * The batch parameter scopes the download to one subfolder,
 * keeping ZIPs small and avoiding server timeouts.
 *
 * Without batch parameter, redirects to the download page.
 */
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/s3.php';

$session = $_GET['session'] ?? '';
$batch = $_GET['batch'] ?? '';

if (!validate_session_id($session)) {
    http_response_code(400);
    echo 'Invalid session ID';
    exit;
}

// Without batch parameter, redirect to the download page
if (empty($batch)) {
    header("Location: /api/download_page.php?session=" . urlencode($session));
    exit;
}

// Validate batch name (alphanumeric, hyphens, underscores only)
if (!preg_match('/^[a-zA-Z0-9_\-]+$/', $batch)) {
    http_response_code(400);
    echo 'Invalid batch name';
    exit;
}

$s3 = new S3Client(AWS_ACCESS_KEY, AWS_SECRET_KEY, AWS_REGION, S3_BUCKET);
$prefix = SESSION_PREFIX . $session . '/' . $batch . '/';

// List files in this batch
$objects = $s3->listObjects($prefix);
if (empty($objects)) {
    http_response_code(404);
    echo 'No files found for this batch';
    exit;
}

// Create ZIP in a temp file
$tmpFile = tempnam(sys_get_temp_dir(), 'tinysea_');
$zip = new ZipArchive();
if ($zip->open($tmpFile, ZipArchive::OVERWRITE) !== true) {
    http_response_code(500);
    echo 'Failed to create ZIP';
    exit;
}

$fileCount = 0;
foreach ($objects as $obj) {
    $key = $obj['Key'];
    $relativePath = substr($key, strlen($prefix));
    if (empty($relativePath)) continue;

    $content = $s3->getObject($key);
    if ($content === null) continue;

    $zip->addFromString($relativePath, $content);
    $fileCount++;
    unset($content);
}

$zip->close();

if ($fileCount === 0) {
    unlink($tmpFile);
    http_response_code(404);
    echo 'No files could be retrieved';
    exit;
}

// Stream the ZIP file
$zipFilename = "{$batch}.zip";
$fileSize = filesize($tmpFile);

header('Content-Type: application/zip');
header("Content-Disposition: attachment; filename=\"{$zipFilename}\"");
header("Content-Length: {$fileSize}");
header('Cache-Control: no-cache, no-store, must-revalidate');

readfile($tmpFile);
unlink($tmpFile);
exit;
