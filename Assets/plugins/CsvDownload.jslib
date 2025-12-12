mergeInto(LibraryManager.library, {
  TinySea_DownloadFile: function (namePtr, dataPtr, dataLen) {
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
  }
});