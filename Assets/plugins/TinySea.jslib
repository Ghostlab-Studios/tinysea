// TinySea.jslib
// Place this file in Assets/Plugins/WebGL/
// 
// Provides both single file CSV download and ZIP download functionality.
// ZIP uses JSZip library loaded from CDN on first use.
//
// IMPORTANT: Delete any other TinySea jslib files (CsvDownload.jslib, TinySeaZip.jslib)
// to avoid duplicate symbol errors.

mergeInto(LibraryManager.library, {
    
    // ==================== SINGLE FILE DOWNLOAD ====================
    
    TinySea_DownloadFile: function(namePtr, dataPtr, dataLen) {
        // Convert filename from Unity string
        var name = UTF8ToString(namePtr);
        
        // Get the memory buffer - works with Unity 2021+
        // Try to access via Module first, fallback to global scope
        var heap = Module.HEAPU8 || HEAPU8;
        
        // Create a view of the data
        var dataView = heap.subarray(dataPtr, dataPtr + dataLen);
        
        // Make a copy of the data (important: Unity may reuse this memory!)
        var dataCopy = new Uint8Array(dataView);
        
        // Create blob and download
        var blob = new Blob([dataCopy], { type: "text/csv;charset=utf-8" });
        var url = URL.createObjectURL(blob);
        var a = document.createElement("a");
        a.style.display = "none";
        a.href = url;
        a.download = name;
        document.body.appendChild(a);
        a.click();
        
        // Cleanup
        setTimeout(function() {
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        }, 100);
    },
    
    // ==================== ZIP DOWNLOAD ====================
    
    // State for building ZIP (stored on window to persist across calls)
    $TinySeaZipState: {
        zip: null,
        filename: ""
    },
    
    // Load JSZip from CDN if not already loaded
    TinySea_EnsureJSZip__deps: ['$TinySeaZipState'],
    TinySea_EnsureJSZip: function(callback) {
        if (window.JSZip) {
            dynCall('v', callback, []);
            return;
        }
        
        var script = document.createElement('script');
        script.src = 'https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js';
        script.onload = function() {
            console.log('JSZip loaded from CDN');
            dynCall('v', callback, []);
        };
        script.onerror = function() {
            console.error('Failed to load JSZip from CDN');
        };
        document.head.appendChild(script);
    },
    
    // Initialize a new ZIP file
    TinySea_InitZipDownload__deps: ['$TinySeaZipState'],
    TinySea_InitZipDownload: function(zipFilenamePtr) {
        var zipFilename = UTF8ToString(zipFilenamePtr);
        
        // Load JSZip if needed, then initialize
        if (!window.JSZip) {
            var script = document.createElement('script');
            script.src = 'https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js';
            script.onload = function() {
                console.log('JSZip loaded');
                TinySeaZipState.zip = new JSZip();
                TinySeaZipState.filename = zipFilename;
                console.log('ZIP initialized: ' + zipFilename);
            };
            script.onerror = function() {
                console.error('Failed to load JSZip');
            };
            document.head.appendChild(script);
        } else {
            TinySeaZipState.zip = new JSZip();
            TinySeaZipState.filename = zipFilename;
            console.log('ZIP initialized: ' + zipFilename);
        }
    },
    
    // Add a file to the ZIP
    TinySea_AddFileToZip__deps: ['$TinySeaZipState'],
    TinySea_AddFileToZip: function(filenamePtr, contentPtr) {
        if (!TinySeaZipState.zip) {
            console.error('ZIP not initialized - call TinySea_InitZipDownload first');
            return;
        }
        
        var filename = UTF8ToString(filenamePtr);
        var content = UTF8ToString(contentPtr);
        
        TinySeaZipState.zip.file(filename, content);
        console.log('Added to ZIP: ' + filename + ' (' + content.length + ' chars)');
    },
    
    // Finalize and download the ZIP
    TinySea_FinalizeZipDownload__deps: ['$TinySeaZipState'],
    TinySea_FinalizeZipDownload: function() {
        if (!TinySeaZipState.zip) {
            console.error('ZIP not initialized');
            return;
        }
        
        var filename = TinySeaZipState.filename;
        var zip = TinySeaZipState.zip;
        
        zip.generateAsync({ type: 'blob' }).then(function(blob) {
            // Create download link
            var url = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.style.display = 'none';
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            
            // Cleanup
            setTimeout(function() {
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            }, 100);
            
            console.log('ZIP download triggered: ' + filename);
        }).catch(function(err) {
            console.error('ZIP generation failed:', err);
        });
        
        // Reset state
        TinySeaZipState.zip = null;
        TinySeaZipState.filename = "";
    }
});
