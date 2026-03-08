mergeInto(LibraryManager.library, {

    TinySea_InitDragDrop: function() {
        var canvas = document.querySelector('#unity-canvas') || document.querySelector('canvas');
        if (!canvas) {
            console.warn('[TinySea] Could not find canvas element for drag-drop.');
            return;
        }

        canvas.addEventListener('dragover', function(e) {
            e.preventDefault();
            e.stopPropagation();
            SendMessage('CSV Uploader', 'OnCsvDragOver');
        });

        canvas.addEventListener('dragleave', function(e) {
            e.preventDefault();
            e.stopPropagation();
            SendMessage('CSV Uploader', 'OnCsvDragLeave');
        });

        canvas.addEventListener('drop', function(e) {
            e.preventDefault();
            e.stopPropagation();

            if (!e.dataTransfer || !e.dataTransfer.files || e.dataTransfer.files.length === 0) {
                SendMessage('CSV Uploader', 'OnCsvDragLeave');
                return;
            }

            var file = e.dataTransfer.files[0];
            var name = file.name.toLowerCase();

            if (name.substring(name.length - 4) !== '.csv') {
                SendMessage('CSV Uploader', 'OnCsvUploadError', 'Only CSV files are accepted. Please drop a .csv file.');
                return;
            }

            var reader = new FileReader();
            reader.onload = function() {
                SendMessage('CSV Uploader', 'OnCsvFileReceived', reader.result);
            };
            reader.onerror = function() {
                SendMessage('CSV Uploader', 'OnCsvUploadError', 'Failed to read file.');
            };
            reader.readAsText(file);
        });
    }

});
