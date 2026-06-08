<?php
/**
 * Serves build ZIPs with proper headers to reduce browser download warnings.
 */
$file    = $_GET['file'] ?? '';

// v1 is the default. Any ?v=N is honored if that build exists, else we fall back to v1.
$v = preg_replace('/[^0-9]/', '', (string)($_GET['v'] ?? '1'));
if ($v === '') { $v = '1'; }
$version = 'v' . $v;

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

// Fall back to the default v1 if the requested version/file isn't published.
if (!file_exists($path)) {
    $path = __DIR__ . '/v1/' . $filename;
}

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
