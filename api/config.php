<?php
/**
 * TinySea API Configuration
 * Loads .env and provides S3 config constants.
 */

// Load .env file (one directory up)
$envFile = dirname(__DIR__) . '/.env';
if (file_exists($envFile)) {
    $lines = file($envFile, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
    foreach ($lines as $line) {
        $line = trim($line);
        if ($line === '' || $line[0] === '#') continue;
        if (strpos($line, '=') === false) continue;
        list($key, $value) = explode('=', $line, 2);
        $_ENV[trim($key)] = trim($value);
    }
}

// S3 configuration
define('AWS_ACCESS_KEY', $_ENV['AWS_ACCESS_KEY_ID'] ?? '');
define('AWS_SECRET_KEY', $_ENV['AWS_SECRET_ACCESS_KEY'] ?? '');
define('AWS_REGION', $_ENV['AWS_REGION'] ?? 'us-east-1');
define('S3_BUCKET', $_ENV['S3_BUCKET'] ?? 'tinysea-simulation');

// Limits
define('MAX_UPLOAD_SIZE', 32 * 1024 * 1024); // 32 MB per file
define('SESSION_PREFIX', 'sessions/');

/**
 * Send JSON response and exit.
 */
function json_response($data, $code = 200) {
    http_response_code($code);
    header('Content-Type: application/json');
    echo json_encode($data);
    exit;
}

/**
 * Validate session ID format (alphanumeric + hyphens, 20-40 chars).
 */
function validate_session_id($id) {
    return preg_match('/^[a-zA-Z0-9\-]{20,40}$/', $id);
}
