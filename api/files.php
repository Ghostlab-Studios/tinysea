<?php
/**
 * GET /api/files.php?session=...&batch=...
 * Returns JSON list of files in a batch with their sizes.
 */
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/s3.php';

header('Content-Type: application/json');

$session = $_GET['session'] ?? '';
$batch = $_GET['batch'] ?? '';

if (!validate_session_id($session)) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid session ID']);
    exit;
}
if (!preg_match('/^[a-zA-Z0-9_\-]+$/', $batch)) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid batch name']);
    exit;
}

$s3 = new S3Client(AWS_ACCESS_KEY, AWS_SECRET_KEY, AWS_REGION, S3_BUCKET);
$prefix = SESSION_PREFIX . $session . '/' . $batch . '/';
$objects = $s3->listObjects($prefix);

$files = [];
foreach ($objects as $obj) {
    $rel = substr($obj['Key'], strlen($prefix));
    if (empty($rel)) continue;
    $files[] = ['name' => $rel, 'size' => (int)$obj['Size']];
}

echo json_encode(['batch' => $batch, 'files' => $files]);
