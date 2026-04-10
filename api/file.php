<?php
/**
 * GET /api/file.php?session=...&batch=...&file=...
 * Streams a single file from S3. Lightweight — no ZIP, no buffering.
 */
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/s3.php';

$session = $_GET['session'] ?? '';
$batch = $_GET['batch'] ?? '';
$file = $_GET['file'] ?? '';

if (!validate_session_id($session)) {
    http_response_code(400);
    echo 'Invalid session ID';
    exit;
}
if (!preg_match('/^[a-zA-Z0-9_\-]+$/', $batch)) {
    http_response_code(400);
    echo 'Invalid batch name';
    exit;
}
// Sanitize filename — allow alphanumeric, hyphens, underscores, dots, forward slashes
if (!preg_match('/^[a-zA-Z0-9_\-\.\/]+$/', $file) || strpos($file, '..') !== false) {
    http_response_code(400);
    echo 'Invalid file name';
    exit;
}

$s3 = new S3Client(AWS_ACCESS_KEY, AWS_SECRET_KEY, AWS_REGION, S3_BUCKET);
$key = SESSION_PREFIX . $session . '/' . $batch . '/' . $file;
$content = $s3->getObject($key);

if ($content === null) {
    http_response_code(404);
    echo 'File not found';
    exit;
}

header('Content-Type: application/octet-stream');
header('Content-Length: ' . strlen($content));
header('Cache-Control: no-cache');
echo $content;
