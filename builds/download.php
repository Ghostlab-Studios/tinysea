<?php
/**
 * Serves build ZIPs with proper headers to reduce browser download warnings.
 */
$file    = $_GET['file'] ?? '';
$version = (($_GET['v'] ?? '1') === '2') ? 'v2' : 'v1';

$allowed = [
    'windows'     => 'TinySea-Windows.zip',
    'macos'       => 'TinySea-macOS.zip',
    'macos-guide' => 'TinySea macOS Setup Guide.pdf',
];

if (!isset($allowed[$file])) {
    http_response_code(404);
    echo 'File not found';
    exit;
}

$filename = $allowed[$file];
$path = __DIR__ . '/' . $version . '/' . $filename;

if (!file_exists($path)) {
    http_response_code(404);
    echo 'Build not available yet';
    exit;
}

$size = filesize($path);

$ext = pathinfo($filename, PATHINFO_EXTENSION);
$mime = $ext === 'pdf' ? 'application/pdf' : 'application/zip';
header('Content-Type: ' . $mime);
header('Content-Disposition: attachment; filename="' . $filename . '"');
header('Content-Length: ' . $size);
header('Cache-Control: public, max-age=3600');
header('X-Content-Type-Options: nosniff');

readfile($path);
exit;
