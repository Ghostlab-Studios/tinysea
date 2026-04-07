<?php
/**
 * Minimal AWS S3 client using Signature Version 4.
 * Zero dependencies — uses PHP's built-in cURL and hash functions.
 * Supports PutObject, GetObject, ListObjectsV2.
 */
class S3Client {
    private $accessKey;
    private $secretKey;
    private $region;
    private $bucket;
    private $endpoint;

    public function __construct($accessKey, $secretKey, $region, $bucket) {
        $this->accessKey = $accessKey;
        $this->secretKey = $secretKey;
        $this->region = $region;
        $this->bucket = $bucket;
        $this->endpoint = "https://{$bucket}.s3.{$region}.amazonaws.com";
    }

    /**
     * Upload a string as an S3 object.
     */
    public function putObject($key, $content, $contentType = 'text/csv') {
        $url = $this->endpoint . '/' . $this->encodeKey($key);
        $headers = $this->sign('PUT', $key, $content, $contentType);

        $ch = curl_init($url);
        curl_setopt_array($ch, [
            CURLOPT_CUSTOMREQUEST => 'PUT',
            CURLOPT_POSTFIELDS => $content,
            CURLOPT_HTTPHEADER => $headers,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 60,
        ]);
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        $error = curl_error($ch);
        curl_close($ch);

        if ($httpCode < 200 || $httpCode >= 300) {
            return ['ok' => false, 'code' => $httpCode, 'error' => $error ?: $response];
        }
        return ['ok' => true, 'code' => $httpCode];
    }

    /**
     * Download an S3 object as a string.
     */
    public function getObject($key) {
        $url = $this->endpoint . '/' . $this->encodeKey($key);
        $headers = $this->sign('GET', $key, '', '');

        $ch = curl_init($url);
        curl_setopt_array($ch, [
            CURLOPT_HTTPHEADER => $headers,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 120,
        ]);
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        if ($httpCode !== 200) {
            return null;
        }
        return $response;
    }

    /**
     * List objects with a given prefix.
     * Returns array of ['Key' => ..., 'Size' => ...] items.
     */
    public function listObjects($prefix, $maxKeys = 1000) {
        $query = http_build_query([
            'list-type' => '2',
            'prefix' => $prefix,
            'max-keys' => $maxKeys,
        ]);
        $url = $this->endpoint . '/?' . $query;
        $headers = $this->sign('GET', '', '', '', $query);

        $ch = curl_init($url);
        curl_setopt_array($ch, [
            CURLOPT_HTTPHEADER => $headers,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 30,
        ]);
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        if ($httpCode !== 200) {
            return [];
        }

        // Parse XML response
        $xml = simplexml_load_string($response);
        if (!$xml) return [];

        $objects = [];
        if (isset($xml->Contents)) {
            foreach ($xml->Contents as $item) {
                $objects[] = [
                    'Key' => (string)$item->Key,
                    'Size' => (int)$item->Size,
                ];
            }
        }
        return $objects;
    }

    /**
     * Encode S3 key path segments (but not slashes).
     */
    private function encodeKey($key) {
        return implode('/', array_map('rawurlencode', explode('/', $key)));
    }

    /**
     * AWS Signature Version 4 signing.
     * Returns array of headers ready for cURL.
     */
    private function sign($method, $key, $body, $contentType, $queryString = '') {
        $now = gmdate('Ymd\THis\Z');
        $date = gmdate('Ymd');
        $payloadHash = hash('sha256', $body);

        $canonicalUri = '/' . $this->encodeKey($key);
        $canonicalQueryString = $this->canonicalQueryString($queryString);

        $headersList = [
            'host' => $this->bucket . '.s3.' . $this->region . '.amazonaws.com',
            'x-amz-content-sha256' => $payloadHash,
            'x-amz-date' => $now,
        ];
        if ($contentType) {
            $headersList['content-type'] = $contentType;
        }

        // Sort headers by name
        ksort($headersList);

        $canonicalHeaders = '';
        $signedHeaderNames = [];
        foreach ($headersList as $name => $value) {
            $canonicalHeaders .= $name . ':' . $value . "\n";
            $signedHeaderNames[] = $name;
        }
        $signedHeaders = implode(';', $signedHeaderNames);

        $canonicalRequest = implode("\n", [
            $method,
            $canonicalUri,
            $canonicalQueryString,
            $canonicalHeaders,
            $signedHeaders,
            $payloadHash,
        ]);

        $credentialScope = "{$date}/{$this->region}/s3/aws4_request";
        $stringToSign = implode("\n", [
            'AWS4-HMAC-SHA256',
            $now,
            $credentialScope,
            hash('sha256', $canonicalRequest),
        ]);

        // Derive signing key
        $kDate = hash_hmac('sha256', $date, 'AWS4' . $this->secretKey, true);
        $kRegion = hash_hmac('sha256', $this->region, $kDate, true);
        $kService = hash_hmac('sha256', 's3', $kRegion, true);
        $kSigning = hash_hmac('sha256', 'aws4_request', $kService, true);

        $signature = hash_hmac('sha256', $stringToSign, $kSigning);

        $authorization = "AWS4-HMAC-SHA256 Credential={$this->accessKey}/{$credentialScope}, " .
                         "SignedHeaders={$signedHeaders}, Signature={$signature}";

        // Build cURL header array
        $curlHeaders = [
            "Authorization: {$authorization}",
            "x-amz-content-sha256: {$payloadHash}",
            "x-amz-date: {$now}",
        ];
        if ($contentType) {
            $curlHeaders[] = "Content-Type: {$contentType}";
        }

        return $curlHeaders;
    }

    /**
     * Normalize query string for canonical request.
     */
    private function canonicalQueryString($queryString) {
        if (empty($queryString)) return '';

        parse_str($queryString, $params);
        ksort($params);
        return http_build_query($params, '', '&', PHP_QUERY_RFC3986);
    }
}
