mergeInto(LibraryManager.library, {

    TinySea_InitDragDrop: function() {
        var dragCounter = 0;

        // Prevent the browser from opening dropped files — must be on document level
        document.addEventListener('dragover', function(e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        });

        // Use dragenter/dragleave with a counter to handle nested elements correctly
        document.addEventListener('dragenter', function(e) {
            e.preventDefault();
            dragCounter++;
            if (dragCounter === 1) {
                SendMessage('CsvUploadHandler', 'OnCsvDragOver');
            }
        });

        document.addEventListener('dragleave', function(e) {
            e.preventDefault();
            dragCounter--;
            if (dragCounter <= 0) {
                dragCounter = 0;
                SendMessage('CsvUploadHandler', 'OnCsvDragLeave');
            }
        });

        // Listen on document so the drop works anywhere on the page
        document.addEventListener('drop', function(e) {
            e.preventDefault();
            e.stopPropagation();
            dragCounter = 0;

            if (!e.dataTransfer || !e.dataTransfer.files || e.dataTransfer.files.length === 0) {
                SendMessage('CsvUploadHandler', 'OnCsvDragLeave');
                return;
            }

            var file = e.dataTransfer.files[0];
            var name = file.name.toLowerCase();

            if (name.substring(name.length - 4) !== '.csv') {
                SendMessage('CsvUploadHandler', 'OnCsvUploadError', 'Only CSV files are accepted. Please drop a .csv file.');
                return;
            }

            var reader = new FileReader();
            reader.onload = function() {
                SendMessage('CsvUploadHandler', 'OnCsvFileReceived', reader.result);
            };
            reader.onerror = function() {
                SendMessage('CsvUploadHandler', 'OnCsvUploadError', 'Failed to read file.');
            };
            reader.readAsText(file);
        });

        console.log('[TinySea] Drag-and-drop initialized on document.');
    }

});
