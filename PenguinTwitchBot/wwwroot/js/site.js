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

window.setMediaVolume = function(elementId, volume) {
    const el = document.getElementById(elementId);
    if (el) {
        el.volume = Math.max(0, Math.min(1, volume));
    }
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

window._markdownEditors = (window._markdownEditors instanceof Map) ? window._markdownEditors : new Map();

function getMarkdownEditorState(elementId) {
    if (!elementId || typeof elementId !== 'string') return null;
    if (!window._markdownEditors.has(elementId)) {
        window._markdownEditors.set(elementId, {
            selectionStart: null,
            selectionEnd: null,
            scrollTop: 0,
            scrollLeft: 0,
            height: null
        });
    }
    return window._markdownEditors.get(elementId);
}

window.saveMarkdownSelection = function (elementId) {
    const container = document.getElementById(elementId);
    if (!container) return;
    const textarea = container.tagName === 'TEXTAREA' ? container : container.querySelector('textarea');
    if (!textarea) return;

    const state = getMarkdownEditorState(elementId);
    if (!state) return;
    if (document.activeElement === textarea || state.selectionStart === null) {
        state.selectionStart = textarea.selectionStart;
        state.selectionEnd = textarea.selectionEnd;
    }
    state.scrollTop = textarea.scrollTop;
    state.scrollLeft = textarea.scrollLeft;
    if (textarea.style.height) {
        state.height = textarea.style.height;
    }
};

window.setupMarkdownTextarea = function (elementId) {
    const container = document.getElementById(elementId);
    if (!container) return;
    const textarea = container.tagName === 'TEXTAREA' ? container : container.querySelector('textarea');
    if (!textarea) return;

    const state = getMarkdownEditorState(elementId);
    if (!state) return;

    if (state.height && textarea.style.height !== state.height) {
        textarea.style.height = state.height;
    }

    if (!textarea._initializedMarkdown) {
        textarea._initializedMarkdown = true;

        const updateSelection = () => {
            if (document.activeElement === textarea) {
                state.selectionStart = textarea.selectionStart;
                state.selectionEnd = textarea.selectionEnd;
            }
            state.scrollTop = textarea.scrollTop;
            state.scrollLeft = textarea.scrollLeft;
        };

        textarea.addEventListener('keyup', updateSelection);
        textarea.addEventListener('mouseup', updateSelection);
        textarea.addEventListener('select', updateSelection);
        textarea.addEventListener('input', updateSelection);
        textarea.addEventListener('scroll', () => {
            state.scrollTop = textarea.scrollTop;
            state.scrollLeft = textarea.scrollLeft;
        });

        if (window.ResizeObserver) {
            const resizeObserver = new ResizeObserver(() => {
                if (textarea.style.height && textarea.offsetHeight > 0) {
                    state.height = textarea.style.height;
                }
            });
            resizeObserver.observe(textarea);
        }

        const editor = container.closest('.github-markdown-editor');
        const toolbar = editor ? editor.querySelector('.editor-toolbar') : null;
        if (toolbar && !toolbar._prevented) {
            toolbar._prevented = true;
            toolbar.addEventListener('mousedown', function (e) {
                if (e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA') {
                    if (document.activeElement === textarea) {
                        state.selectionStart = textarea.selectionStart;
                        state.selectionEnd = textarea.selectionEnd;
                        state.scrollTop = textarea.scrollTop;
                        state.scrollLeft = textarea.scrollLeft;
                    }
                    if (textarea.style.height) {
                        state.height = textarea.style.height;
                    }
                    e.preventDefault();
                }
            });
        }
    }
};

window.insertMarkdownText = function (elementId, prefix, suffix, defaultText) {
    const container = document.getElementById(elementId);
    if (!container) return null;
    const textarea = container.tagName === 'TEXTAREA' ? container : container.querySelector('textarea');
    if (!textarea) return null;

    const state = getMarkdownEditorState(elementId);
    if (!state) return null;
    const text = textarea.value || '';

    let start = textarea.selectionStart;
    let end = textarea.selectionEnd;

    if (typeof state.selectionStart === 'number' && (document.activeElement !== textarea || start === text.length || start === 0)) {
        start = state.selectionStart;
        end = typeof state.selectionEnd === 'number' ? state.selectionEnd : start;
    }

    if (typeof start !== 'number' || isNaN(start) || start < 0) start = text.length;
    if (typeof end !== 'number' || isNaN(end) || end < start) end = start;
    if (start > text.length) start = text.length;
    if (end > text.length) end = text.length;

    // Preserve scroll positions
    const savedScrollTop = typeof state.scrollTop === 'number' ? state.scrollTop : textarea.scrollTop;
    const savedScrollLeft = typeof state.scrollLeft === 'number' ? state.scrollLeft : textarea.scrollLeft;
    const scrollX = window.scrollX || window.pageXOffset || 0;
    const scrollY = window.scrollY || window.pageYOffset || 0;

    const selected = text.substring(start, end);
    const inner = selected.length > 0 ? selected : (defaultText || '');
    const replacement = (prefix || '') + inner + (suffix || '');

    const newText = text.substring(0, start) + replacement + text.substring(end);
    textarea.value = newText;

    const newCursor = start + (prefix || '').length + inner.length;
    state.selectionStart = newCursor;
    state.selectionEnd = newCursor;
    state.scrollTop = savedScrollTop;
    state.scrollLeft = savedScrollLeft;

    const applyCursorAndScroll = () => {
        try {
            textarea.focus({ preventScroll: true });
        } catch (e) {
            textarea.focus();
        }
        textarea.setSelectionRange(newCursor, newCursor);
        textarea.scrollTop = savedScrollTop;
        textarea.scrollLeft = savedScrollLeft;
        if (state.height && textarea.style.height !== state.height) {
            textarea.style.height = state.height;
        }
        window.scrollTo(scrollX, scrollY);
    };

    applyCursorAndScroll();

    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    textarea.dispatchEvent(new Event('change', { bubbles: true }));

    requestAnimationFrame(applyCursorAndScroll);
    setTimeout(applyCursorAndScroll, 0);
    setTimeout(applyCursorAndScroll, 50);

    return newText;
};

window.restoreMarkdownCursorAndScroll = function (elementId) {
    const container = document.getElementById(elementId);
    if (!container) return;
    const textarea = container.tagName === 'TEXTAREA' ? container : container.querySelector('textarea');
    if (!textarea) return;

    const state = getMarkdownEditorState(elementId);
    if (!state) return;
    const text = textarea.value || '';

    let start = typeof state.selectionStart === 'number' ? state.selectionStart : text.length;
    let end = typeof state.selectionEnd === 'number' ? state.selectionEnd : start;
    if (start > text.length) start = text.length;
    if (end > text.length) end = text.length;

    const scrollTop = typeof state.scrollTop === 'number' ? state.scrollTop : 0;
    const scrollLeft = typeof state.scrollLeft === 'number' ? state.scrollLeft : 0;

    const restore = () => {
        try {
            textarea.focus({ preventScroll: true });
        } catch (e) {
            textarea.focus();
        }
        textarea.setSelectionRange(start, end);
        textarea.scrollTop = scrollTop;
        textarea.scrollLeft = scrollLeft;
        if (state.height && textarea.style.height !== state.height) {
            textarea.style.height = state.height;
        }
    };

    restore();
    requestAnimationFrame(restore);
    setTimeout(restore, 0);
    setTimeout(restore, 50);
};

window.setupMarkdownDropZone = function (elementId, fileInputId) {
    const container = document.getElementById(elementId);
    if (!container) return;
    const editor = container.closest('.github-markdown-editor');
    if (!editor) return;

    if (editor._dropZoneInitialized) return;
    editor._dropZoneInitialized = true;

    let dragCounter = 0;

    const isFileDrag = (e) => {
        if (!e.dataTransfer || !e.dataTransfer.types) return false;
        for (let i = 0; i < e.dataTransfer.types.length; i++) {
            if (e.dataTransfer.types[i] === 'Files') return true;
        }
        return false;
    };

    editor.addEventListener('dragenter', function (e) {
        if (!isFileDrag(e)) return;
        e.preventDefault();
        dragCounter++;
        editor.classList.add('drag-active');
    });

    editor.addEventListener('dragover', function (e) {
        if (!isFileDrag(e)) return;
        e.preventDefault();
        if (e.dataTransfer) {
            e.dataTransfer.dropEffect = 'copy';
        }
        editor.classList.add('drag-active');
    });

    editor.addEventListener('dragleave', function (e) {
        if (!isFileDrag(e)) return;
        e.preventDefault();
        dragCounter--;
        if (dragCounter <= 0) {
            dragCounter = 0;
            editor.classList.remove('drag-active');
        }
    });

    editor.addEventListener('drop', function (e) {
        if (!isFileDrag(e)) return;
        e.preventDefault();
        e.stopPropagation();
        dragCounter = 0;
        editor.classList.remove('drag-active');

        const fileInput = document.getElementById(fileInputId);
        if (!fileInput) return;
        const files = e.dataTransfer ? e.dataTransfer.files : null;
        if (files && files.length > 0) {
            fileInput.files = files;
            fileInput.dispatchEvent(new Event('change', { bubbles: true }));
        }
    });

    const textarea = container.tagName === 'TEXTAREA' ? container : container.querySelector('textarea');
    if (textarea && !textarea._pasteInitialized) {
        textarea._pasteInitialized = true;
        textarea.addEventListener('paste', function (e) {
            const fileInput = document.getElementById(fileInputId);
            if (!fileInput) return;

            const clipboard = e.clipboardData || (window.clipboardData ? window.clipboardData : null);
            if (!clipboard) return;

            const items = clipboard.items;
            let hasImage = false;
            if (items) {
                for (let i = 0; i < items.length; i++) {
                    if (items[i].type && items[i].type.indexOf('image') !== -1) {
                        hasImage = true;
                        break;
                    }
                }
            }

            if (hasImage && clipboard.files && clipboard.files.length > 0) {
                e.preventDefault();
                fileInput.files = clipboard.files;
                fileInput.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
    }
};

window.replaceMarkdownText = function (elementId, target, replacement) {
    const container = document.getElementById(elementId);
    if (!container) return null;
    const textarea = container.tagName === 'TEXTAREA' ? container : container.querySelector('textarea');
    if (!textarea) return null;

    const state = getMarkdownEditorState(elementId);
    const text = textarea.value || '';
    if (!text.includes(target)) return text;

    const newText = text.replace(target, replacement);
    textarea.value = newText;

    if (state && typeof state.selectionStart === 'number') {
        const diff = replacement.length - target.length;
        state.selectionStart = Math.max(0, state.selectionStart + diff);
        state.selectionEnd = Math.max(0, state.selectionEnd + diff);
    }

    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    textarea.dispatchEvent(new Event('change', { bubbles: true }));
    return newText;
};

window.triggerFileInput = function (fileInputId) {
    const fileInput = document.getElementById(fileInputId);
    if (fileInput) {
        fileInput.click();
    }
};

window.copyToClipboard = async function (text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (e) {
            // fallback below
        }
    }
    const textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.style.position = "fixed";
    textArea.style.opacity = "0";
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    try {
        const successful = document.execCommand('copy');
        document.body.removeChild(textArea);
        return successful;
    } catch (err) {
        document.body.removeChild(textArea);
        return false;
    }
};

window.penguinTheme = {
    getPreference: function () {
        try {
            var raw = localStorage.getItem('penguin_theme_pref');
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    },
    setPreference: function (isDarkMode, themeId) {
        try {
            var json = JSON.stringify({ isDarkMode: isDarkMode, themeId: themeId });
            localStorage.setItem('penguin_theme_pref', json);
            document.cookie = 'penguin_theme_pref=' + encodeURIComponent(json) + '; path=/; max-age=31536000; SameSite=Lax';
        } catch (e) {
            // Ignore localStorage quota or private browsing errors
        }
    }
};
