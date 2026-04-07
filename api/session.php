<?php
/**
 * POST /api/session.php
 * Creates a new simulation session. Returns a unique session ID
 * that's used as the S3 prefix for all files in this run.
 */
require_once __DIR__ . '/config.php';

header('Content-Type: application/json');

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    json_response(['error' => 'Method not allowed'], 405);
}

// Generate a unique session ID: timestamp + random
$sessionId = date('Ymd-His') . '-' . bin2hex(random_bytes(8));

json_response([
    'session' => $sessionId,
    'prefix' => SESSION_PREFIX . $sessionId . '/',
]);
