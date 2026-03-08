<?php
$current_page = 'simulation';
$page_title = 'Tiny Sea Simulation - Marine Ecosystem Research Platform';
include __DIR__ . '/includes/header.php';
?>

<section class="unity-section">
    <div class="unity-container">
        <canvas id="unity-canvas" width="1920" height="1080" tabindex="-1"></canvas>
        <div id="drop-overlay">
            <div class="drop-overlay-content">
                <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="#00d9ff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                    <polyline points="7 10 12 15 17 10"></polyline>
                    <line x1="12" y1="15" x2="12" y2="3"></line>
                </svg>
                <p>Drop CSV file here</p>
            </div>
        </div>
        <div id="loading-overlay">
            <div class="spinner"></div>
            <p>Loading Tiny Sea Simulation...</p>
            <div class="progress-bar">
                <div class="progress-fill" id="progress-fill"></div>
            </div>
        </div>
    </div>

    <div class="fullscreen-controls">
        <button class="fullscreen-button" id="fullscreen-btn">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="15 3 21 3 21 9"></polyline>
                <polyline points="9 21 3 21 3 15"></polyline>
                <line x1="21" y1="3" x2="14" y2="10"></line>
                <line x1="3" y1="21" x2="10" y2="14"></line>
            </svg>
            <span>Fullscreen</span>
        </button>
    </div>

    <div class="info-card">
        <h2>🌊 About This Simulation</h2>
        <p>This is a scientific research platform studying climate change impacts on marine food webs through realistic temperature modeling and ecosystem dynamics.</p>
        <a href="/about/" class="btn-primary">Learn More →</a>
    </div>
</section>

<?php $buildVersion = (int)(filemtime(__DIR__ . '/build/TinySeaWebGL/Build/TinySeaWebGL.data.br') ?: time()); ?>
<script src="/build/TinySeaWebGL/Build/TinySeaWebGL.loader.js?v=<?php echo $buildVersion; ?>"></script>
<script>
    var unityInstance = null;
    var canvas = document.querySelector("#unity-canvas");
    var loadingOverlay = document.getElementById('loading-overlay');
    var progressFill = document.getElementById('progress-fill');
    var dropOverlay = document.getElementById('drop-overlay');
    var dragCounter = 0;

    // Helper: safely send message to Unity — silently ignores if object doesn't exist yet
    function unitySend(obj, method, arg) {
        if (!unityInstance) return;
        try {
            if (arg !== undefined) {
                unityInstance.SendMessage(obj, method, arg);
            } else {
                unityInstance.SendMessage(obj, method);
            }
        } catch (e) {
            console.warn('[TinySea] SendMessage failed:', e.message);
        }
    }

    // ── Drag-and-drop: prevent browser default IMMEDIATELY ──
    document.addEventListener('dragover', function(e) {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
    });

    document.addEventListener('dragenter', function(e) {
        e.preventDefault();
        dragCounter++;
        if (dragCounter === 1 && dropOverlay) {
            dropOverlay.style.display = 'flex';
        }
    });

    document.addEventListener('dragleave', function(e) {
        e.preventDefault();
        dragCounter--;
        if (dragCounter <= 0) {
            dragCounter = 0;
            if (dropOverlay) {
                dropOverlay.style.display = 'none';
            }
        }
    });

    document.addEventListener('drop', function(e) {
        e.preventDefault();
        e.stopPropagation();
        dragCounter = 0;
        if (dropOverlay) {
            dropOverlay.style.display = 'none';
        }

        if (!unityInstance) {
            alert('Unity is still loading. Please wait and try again.');
            return;
        }

        if (!e.dataTransfer || !e.dataTransfer.files || e.dataTransfer.files.length === 0) {
            return;
        }

        var file = e.dataTransfer.files[0];
        var name = file.name.toLowerCase();

        if (name.substring(name.length - 4) !== '.csv') {
            unitySend('CsvUploadHandler', 'OnCsvUploadError', 'Only CSV files are accepted. Please drop a .csv file.');
            return;
        }

        var reader = new FileReader();
        reader.onload = function() {
            unitySend('CsvUploadHandler', 'OnCsvFileReceived', reader.result);
        };
        reader.onerror = function() {
            unitySend('CsvUploadHandler', 'OnCsvUploadError', 'Failed to read file.');
        };
        reader.readAsText(file);
    });

    // ── Unity initialization ──
    var buildVersion = "<?php echo $buildVersion; ?>";
    var buildUrl = "/build/TinySeaWebGL/Build";
    var config = {
        dataUrl: buildUrl + "/TinySeaWebGL.data.br?v=" + buildVersion,
        frameworkUrl: buildUrl + "/TinySeaWebGL.framework.js.br?v=" + buildVersion,
        codeUrl: buildUrl + "/TinySeaWebGL.wasm.br?v=" + buildVersion,
        streamingAssetsUrl: "StreamingAssets",
        companyName: "TinySea",
        productName: "TinySea",
        productVersion: "1.0",
        matchWebGLToCanvasSize: false,
    };

    function onProgress(progress) {
        progressFill.style.width = (progress * 100) + '%';
    }

    createUnityInstance(canvas, config, onProgress).then(function(instance) {
        unityInstance = instance;
        loadingOverlay.style.display = 'none';
        console.log("Unity loaded successfully! Build version: " + buildVersion);

        // Wire up fullscreen button
        var fsBtn = document.getElementById('fullscreen-btn');
        if (fsBtn) {
            fsBtn.addEventListener('click', function() {
                unityInstance.SetFullscreen(1);
            });
        }
    }).catch(function(error) {
        console.error("Unity load error:", error);

        var errorMsg = '<div style="color: #ff6b6b; text-align: center; padding: 40px;">';
        errorMsg += '<h3>Failed to Load Simulation</h3>';
        errorMsg += '<p style="margin-bottom: 20px;">' + error + '</p>';

        if (error.toString().includes('allocate')) {
            errorMsg += '<div style="background: rgba(0,0,0,0.3); padding: 15px; border-radius: 8px; margin-top: 20px;">';
            errorMsg += '<strong>Possible solutions:</strong><br>';
            errorMsg += '1. Make sure .htaccess is in /build/TinySeaWebGL/Build/ folder<br>';
            errorMsg += '2. Clear your browser cache completely (Ctrl+Shift+Delete)<br>';
            errorMsg += '3. Try a different browser<br>';
            errorMsg += '4. The Unity build may need to be re-exported from Unity<br>';
            errorMsg += '</div>';
        }

        errorMsg += '<p style="margin-top: 20px; font-size: 14px;">Check browser console (F12) for detailed error information.</p>';
        errorMsg += '</div>';

        loadingOverlay.innerHTML = errorMsg;
    });
</script>

<?php include __DIR__ . '/includes/footer.php'; ?>
