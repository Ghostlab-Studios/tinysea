<?php
/**
 * Serves build ZIPs with proper headers to reduce browser download warnings.
 */
$file = $_GET['file'] ?? '';

$allowed = [
    'windows' => 'TinySea-Windows.zip',
    'macos'   => 'TinySea-macOS.zip',
];

if (!isset($allowed[$file])) {
    http_response_code(404);
    echo 'File not found';
    exit;
}

$filename = $allowed[$file];
$path = __DIR__ . '/' . $filename;

if (!file_exists($path)) {
    http_response_code(404);
    echo 'Build not available yet';
    exit;
}

$size = filesize($path);

header('Content-Type: application/zip');
header('Content-Disposition: attachment; filename="' . $filename . '"');
header('Content-Length: ' . $size);
header('Cache-Control: public, max-age=3600');
header('X-Content-Type-Options: nosniff');

readfile($path);
exit;
