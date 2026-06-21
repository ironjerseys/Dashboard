// CodeMirror-backed C# editor for the coding challenges.
// Text and syntax highlighting live in the SAME element, so the selection
// always matches the caret (unlike a transparent-textarea overlay).
window.codeEditor = (function () {

    var instances = {};

    function init(textareaId, value, readOnly) {
        var ta = document.getElementById(textareaId);
        if (!ta || typeof CodeMirror === 'undefined') return;
        if (instances[textareaId]) { update(textareaId, value, readOnly); return; }

        if (typeof value === 'string') ta.value = value;

        var cm = CodeMirror.fromTextArea(ta, {
            mode: 'text/x-csharp',
            theme: 'material-darker',
            lineNumbers: true,
            indentUnit: 4,
            tabSize: 4,
            indentWithTabs: false,
            smartIndent: true,
            lineWrapping: false,
            readOnly: readOnly ? 'nocursor' : false
        });
        cm.setSize('100%', 'auto');
        instances[textareaId] = cm;

        // Push every change back to the underlying textarea and notify Blazor's
        // @oninput binding so the server-side value stays in sync.
        cm.on('change', function () {
            cm.save();
            ta.dispatchEvent(new Event('input', { bubbles: true }));
        });
    }

    function update(textareaId, value, readOnly) {
        var cm = instances[textareaId];
        if (!cm) return;
        if (typeof value === 'string' && value !== cm.getValue()) {
            cm.setValue(value);
        }
        cm.setOption('readOnly', readOnly ? 'nocursor' : false);
    }

    function dispose(textareaId) {
        var cm = instances[textareaId];
        if (cm) {
            try { cm.toTextArea(); } catch (e) { /* element may already be gone */ }
            delete instances[textareaId];
        }
    }

    return { init: init, update: update, dispose: dispose };
})();
