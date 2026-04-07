<?php
/**
 * POST /api/upload.php
 * Uploads a single CSV file to S3 under the session prefix.
 *
 * Body: JSON { "session": "...", "filename": "batch1/scenario_1.csv", "content": "..." }
 */
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/s3.php';

header('Content-Type: application/json');

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    json_response(['error' => 'Method not allowed'], 405);
}

// Read JSON body
$input = json_decode(file_get_contents('php://input'), true);
if (!$input) {
    json_response(['error' => 'Invalid JSON body'], 400);
}

$session = $input['session'] ?? '';
$filename = $input['filename'] ?? '';
$content = $input['content'] ?? '';

// Validate session ID
if (!validate_session_id($session)) {
    json_response(['error' => 'Invalid session ID'], 400);
}

// Validate filename (no path traversal)
if (empty($filename) || strpos($filename, '..') !== false) {
    json_response(['error' => 'Invalid filename'], 400);
}

// Check size
if (strlen($content) > MAX_UPLOAD_SIZE) {
    json_response(['error' => 'File too large (max 32 MB)'], 413);
}

// Upload to S3
$s3 = new S3Client(AWS_ACCESS_KEY, AWS_SECRET_KEY, AWS_REGION, S3_BUCKET);
$key = SESSION_PREFIX . $session . '/' . $filename;
$result = $s3->putObject($key, $content);

if (!$result['ok']) {
    json_response([
        'error' => 'S3 upload failed',
        'details' => $result['error'],
        'code' => $result['code'],
    ], 502);
}

json_response([
    'ok' => true,
    'key' => $key,
    'size' => strlen($content),
]);
