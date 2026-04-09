<?php
/**
 * GET /api/limits.php
 * Returns current PHP limits for debugging upload issues.
 * No credentials or sensitive data exposed.
 */
header('Content-Type: application/json');

echo json_encode([
    'post_max_size' => ini_get('post_max_size'),
    'upload_max_filesize' => ini_get('upload_max_filesize'),
    'memory_limit' => ini_get('memory_limit'),
    'max_execution_time' => ini_get('max_execution_time'),
    'max_input_vars' => ini_get('max_input_vars'),
    'user_ini_filename' => ini_get('user_ini.filename'),
    'user_ini_cache_ttl' => ini_get('user_ini.cache_ttl'),
]);
