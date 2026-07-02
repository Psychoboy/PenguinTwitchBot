// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Download file helper function
window.downloadFile = function(filename, content, isBase64) {
    let blob;
    if (isBase64) {
        const binaryString = atob(content);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        blob = new Blob([bytes], { type: 'application/zip' });
    } else {
        blob = new Blob([content], { type: 'application/json' });
    }
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.style.display = 'none';
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
};

window.panScrollElementById = function (elementId, deltaX, deltaY) {
    const el = document.getElementById(elementId);
    if (!el) return;
    el.scrollBy({
        left: deltaX || 0,
        top: deltaY || 0,
        behavior: 'smooth'
    });
};
