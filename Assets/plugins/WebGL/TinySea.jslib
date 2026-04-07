mergeInto(LibraryManager.library, {

    TinySea_DownloadFile: function(namePtr, dataPtr, dataLen) {
        var name = UTF8ToString(namePtr);
        var data = new Uint8Array(dataLen);
        for (var i = 0; i < dataLen; i++) {
            data[i] = HEAPU8[dataPtr + i];
        }
        var blob = new Blob([data], { type: "application/octet-stream" });
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.href = url;
        link.download = name;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    },

    TinySea_InitZipDownload: function(zipFilenamePtr) {
        window._tsZipName = UTF8ToString(zipFilenamePtr);
        window._tsZipFiles = [];
    },

    TinySea_AddFileToZip: function(filenamePtr, contentPtr) {
        if (!window._tsZipFiles) { return; }
        window._tsZipFiles.push({
            name: UTF8ToString(filenamePtr),
            content: UTF8ToString(contentPtr)
        });
    },

    TinySea_FinalizeZipDownload: function() {
        if (!window._tsZipFiles) { return; }
        var files = window._tsZipFiles;
        var zipName = window._tsZipName;
        
        function doZip() {
            var zip = new JSZip();
            for (var i = 0; i < files.length; i++) {
                zip.file(files[i].name, files[i].content);
            }
            zip.generateAsync({type:"blob"}).then(function(blob) {
                var url = URL.createObjectURL(blob);
                var link = document.createElement("a");
                link.href = url;
                link.download = zipName;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                URL.revokeObjectURL(url);
            });
        }
        
        if (typeof JSZip === "undefined") {
            var s = document.createElement("script");
            s.src = "https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js";
            s.onload = doZip;
            document.head.appendChild(s);
        } else {
            doZip();
        }
        
        window._tsZipFiles = null;
        window._tsZipName = null;
    },

    TinySea_ClearZipDownload: function() {
        window._tsZipFiles = null;
        window._tsZipName = null;
    }

});
