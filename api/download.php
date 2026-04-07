<?php
/**
 * GET /api/download.php?session=...
 * Lists all files in the session's S3 prefix, creates a ZIP,
 * and streams it as a download. Memory-efficient: fetches and
 * adds one file at a time.
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

// List all files in this session
$objects = $s3->listObjects($prefix);
if (empty($objects)) {
    http_response_code(404);
    echo 'No files found for this session';
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
    // Strip the session prefix to get the relative path (e.g., "batch1/scenario_1.csv")
    $relativePath = substr($key, strlen($prefix));
    if (empty($relativePath)) continue;

    // Fetch file content from S3
    $content = $s3->getObject($key);
    if ($content === null) continue;

    $zip->addFromString($relativePath, $content);
    $fileCount++;

    // Free memory after adding to ZIP
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
$zipFilename = "tinysea_bulk_{$session}.zip";
$fileSize = filesize($tmpFile);

header('Content-Type: application/zip');
header("Content-Disposition: attachment; filename=\"{$zipFilename}\"");
header("Content-Length: {$fileSize}");
header('Cache-Control: no-cache, no-store, must-revalidate');

readfile($tmpFile);
unlink($tmpFile);
exit;
