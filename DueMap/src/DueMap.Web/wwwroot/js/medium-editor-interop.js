// Blazor <-> Medium Editor bridge for the template-edit modal.
//
// Why: PMs were shown the raw HTML body of a notice template (tags, inline
// styles, the whole lot) in a <textarea>. That's noise for a non-technical
// user. Medium Editor turns a contenteditable div into a Medium-style WYSIWYG
// surface with a floating toolbar (bold / italic / link / lists / headings), so
// the PM edits prose, not markup.
//
// Ownership model: the contenteditable's content is JS-owned. Blazor renders an
// empty <div id="..."> and NEVER touches its innerHTML via the render diff
// (the div has no @bind). C# pushes content in with setHtml() and gets edits
// back through the OnBodyHtmlChanged JSInvokable callback. Keeping Blazor out of
// the element's DOM is what stops the render loop from wiping the editor.
//
// Merge fields ({{ tenant_name }}) and Scriban control flow ({% if %}) ride
// through as literal text — readable to the PM and untouched on save. The
// "Pay Now" snippet is real HTML, so it renders as an actual button in-place.
window.dueMapEditor = (function () {
    // elementId -> { editor, dotNetRef }
    const instances = {};

    function available() {
        return typeof window.MediumEditor !== 'undefined';
    }

    function init(elementId, dotNetRef, initialHtml) {
        if (!available()) return false;
        const el = document.getElementById(elementId);
        if (!el) return false;

        // Re-init safety: tear down any prior instance bound to this id first
        // (tab switches and re-opens both re-run init).
        destroy(elementId);

        el.innerHTML = initialHtml || '';

        const editor = new window.MediumEditor(el, {
            toolbar: {
                buttons: ['bold', 'italic', 'underline', 'anchor', 'h2', 'h3',
                          'quote', 'unorderedlist', 'orderedlist', 'removeFormat']
            },
            placeholder: { text: 'Write your message…', hideOnClick: true },
            anchor: { linkValidation: true, targetCheckbox: false, placeholderText: 'Paste or type a link' },
            // Keep pasted content clean but allow basic formatting through.
            paste: { forcePlainText: false, cleanPastedHTML: true },
            // We supply our own pay-link snippet; don't let the editor autolink
            // bare URLs (it would mangle {{ pay_url }} merge fields).
            autoLink: false,
            imageDragging: false
        });

        // Debounce the round-trip to C#: editableInput fires on every keystroke.
        let timer;
        const push = () => {
            const ref = instances[elementId] && instances[elementId].dotNetRef;
            if (ref) ref.invokeMethodAsync('OnBodyHtmlChanged', el.innerHTML);
        };
        editor.subscribe('editableInput', () => {
            clearTimeout(timer);
            timer = setTimeout(push, 200);
        });

        instances[elementId] = { editor, dotNetRef };
        return true;
    }

    // Programmatic content set (prefill from system default / reset to default).
    // Dispatches a synthetic input so Medium Editor refreshes its placeholder
    // state and our subscriber syncs the new value back to C#.
    function setHtml(elementId, html) {
        const el = document.getElementById(elementId);
        if (!el) return false;
        el.innerHTML = html || '';
        el.dispatchEvent(new Event('input', { bubbles: true }));
        return true;
    }

    function getHtml(elementId) {
        const el = document.getElementById(elementId);
        return el ? el.innerHTML : '';
    }

    // Drop a placeholder token (asHtml=false -> text node) or a styled snippet
    // (asHtml=true -> parsed markup) at the caret. Falls back to end-of-content
    // when the selection sits outside the editable.
    function insertAtCursor(elementId, content, asHtml) {
        const el = document.getElementById(elementId);
        if (!el) return false;

        el.focus();
        const sel = window.getSelection();
        let range;
        if (sel && sel.rangeCount > 0 && el.contains(sel.anchorNode)) {
            range = sel.getRangeAt(0);
        } else {
            range = document.createRange();
            range.selectNodeContents(el);
            range.collapse(false); // caret at end
        }
        range.deleteContents();

        let lastNode;
        if (asHtml) {
            const frag = range.createContextualFragment(content);
            lastNode = frag.lastChild;
            range.insertNode(frag);
        } else {
            lastNode = document.createTextNode(content);
            range.insertNode(lastNode);
        }

        // Park the caret just after what we inserted so chained clicks flow.
        if (lastNode) {
            range.setStartAfter(lastNode);
            range.setEndAfter(lastNode);
            sel.removeAllRanges();
            sel.addRange(range);
        }

        el.dispatchEvent(new Event('input', { bubbles: true }));
        return true;
    }

    function destroy(elementId) {
        const inst = instances[elementId];
        if (inst && inst.editor) {
            try { inst.editor.destroy(); } catch (e) { /* already gone */ }
        }
        delete instances[elementId];
    }

    return { available, init, setHtml, getHtml, insertAtCursor, destroy };
})();
