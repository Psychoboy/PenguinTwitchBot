// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Download file helper function
window.downloadFile = async function(filename, content) {
    let blob;
    if (content && typeof content.arrayBuffer === 'function') {
        const buffer = await content.arrayBuffer();
        blob = new Blob([buffer], { type: 'application/octet-stream' });
    } else if (typeof content === 'string') {
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

window.setupMarkdownTextarea = function (elementId) {
    const container = document.getElementById(elementId);
    if (!container) return;
    const textarea = container.tagName === 'TEXTAREA' ? container : container.querySelector('textarea');
    if (!textarea || textarea._initializedMarkdown) return;

    textarea._initializedMarkdown = true;
    const saveSelection = () => {
        textarea._lastSelectionStart = textarea.selectionStart;
        textarea._lastSelectionEnd = textarea.selectionEnd;
    };

    textarea.addEventListener('keyup', saveSelection);
    textarea.addEventListener('mouseup', saveSelection);
    textarea.addEventListener('select', saveSelection);
    textarea.addEventListener('input', saveSelection);
    textarea.addEventListener('blur', saveSelection);

    const editor = container.closest('.github-markdown-editor');
    const toolbar = editor ? editor.querySelector('.editor-toolbar') : null;
    if (toolbar && !toolbar._prevented) {
        toolbar._prevented = true;
        toolbar.addEventListener('mousedown', function (e) {
            if (e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA') {
                e.preventDefault();
            }
        });
    }
};

window.insertMarkdownText = function (elementId, prefix, suffix, defaultText) {
    const container = document.getElementById(elementId);
    if (!container) return null;
    const textarea = container.tagName === 'TEXTAREA' ? container : container.querySelector('textarea');
    if (!textarea) return null;

    let start = textarea.selectionStart;
    let end = textarea.selectionEnd;

    if (typeof textarea._lastSelectionStart === 'number' && (start === textarea.value.length || start === 0)) {
        start = textarea._lastSelectionStart;
        end = typeof textarea._lastSelectionEnd === 'number' ? textarea._lastSelectionEnd : start;
    }

    const text = textarea.value || '';
    if (typeof start !== 'number' || isNaN(start) || start < 0) start = text.length;
    if (typeof end !== 'number' || isNaN(end) || end < start) end = start;

    const selected = text.substring(start, end);
    const inner = selected.length > 0 ? selected : (defaultText || '');
    const replacement = (prefix || '') + inner + (suffix || '');

    const newText = text.substring(0, start) + replacement + text.substring(end);
    textarea.value = newText;

    const newCursor = start + (prefix || '').length + inner.length;
    textarea.focus();
    textarea.setSelectionRange(newCursor, newCursor);
    textarea._lastSelectionStart = newCursor;
    textarea._lastSelectionEnd = newCursor;

    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    textarea.dispatchEvent(new Event('change', { bubbles: true }));
    return newText;
};
