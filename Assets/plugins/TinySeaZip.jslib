// TinySeaZip.jslib
// Place this file in Assets/Plugins/WebGL/
// 
// Provides ZIP download functionality using JSZip library.
// JSZip is loaded from CDN on first use.

mergeInto(LibraryManager.library, {
    
    // State for building ZIP
    _tinySeaZip: null,
    _tinySeaZipFilename: "",
    _tinySeaJSZipLoaded: false,
    
    // Load JSZip from CDN if not already loaded
    TinySea_EnsureJSZip: function(callback) {
        if (window.JSZip) {
            callback();
            return;
        }
        
        var script = document.createElement('script');
        script.src = 'https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js';
        script.onload = function() {
            console.log('JSZip loaded');
            callback();
        };
        script.onerror = function() {
            console.error('Failed to load JSZip');
        };
        document.head.appendChild(script);
    },
    
    // Initialize a new ZIP file
    TinySea_InitZipDownload: function(zipFilenamePtr) {
        var zipFilename = UTF8ToString(zipFilenamePtr);
        
        var self = this;
        this.TinySea_EnsureJSZip(function() {
            self._tinySeaZip = new JSZip();
            self._tinySeaZipFilename = zipFilename;
            console.log('ZIP initialized: ' + zipFilename);
        });
    },
    
    // Add a file to the ZIP
    TinySea_AddFileToZip: function(filenamePtr, contentPtr) {
        if (!this._tinySeaZip) {
            console.error('ZIP not initialized');
            return;
        }
        
        var filename = UTF8ToString(filenamePtr);
        var content = UTF8ToString(contentPtr);
        
        this._tinySeaZip.file(filename, content);
        console.log('Added to ZIP: ' + filename);
    },
    
    // Finalize and download the ZIP
    TinySea_FinalizeZipDownload: function() {
        if (!this._tinySeaZip) {
            console.error('ZIP not initialized');
            return;
        }
        
        var filename = this._tinySeaZipFilename;
        var zip = this._tinySeaZip;
        
        zip.generateAsync({ type: 'blob' }).then(function(blob) {
            // Create download link
            var url = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            
            console.log('ZIP download triggered: ' + filename);
        }).catch(function(err) {
            console.error('ZIP generation failed:', err);
        });
        
        // Reset state
        this._tinySeaZip = null;
        this._tinySeaZipFilename = "";
    },
    
    // Single file download (existing function, kept for compatibility)
    TinySea_DownloadFile: function(filenamePtr, dataPtr, dataLen) {
        var filename = UTF8ToString(filenamePtr);
        var data = new Uint8Array(HEAPU8.buffer, dataPtr, dataLen);
        
        var blob = new Blob([data], { type: 'text/csv' });
        var url = URL.createObjectURL(blob);
        
        var a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
});
