// Inserts `text` at the current cursor position of the element with id=elementId.
// Used by EditTemplateModal so the PM can click a placeholder chip to drop
// {{ var_name }} where the cursor sits, instead of typing it by hand.
//
// After mutating the value we dispatch a synthetic `input` event so Blazor's
// @bind:event="oninput" binding picks up the change. Without that event the
// textarea would show the inserted text but the C# field would stay stale —
// and save would persist the pre-insert content.
window.dueMapTemplate = (function () {
    function insertAtCursor(elementId, text) {
        const el = document.getElementById(elementId);
        if (!el) return false;

        el.focus();
        const start = (typeof el.selectionStart === 'number') ? el.selectionStart : el.value.length;
        const end   = (typeof el.selectionEnd   === 'number') ? el.selectionEnd   : el.value.length;

        el.value = el.value.substring(0, start) + text + el.value.substring(end);

        // Restore caret immediately after the inserted text so successive
        // clicks chain naturally — click {{ tenant_name }}, then ", " typed,
        // then {{ amount_owed }}, all without re-clicking the textarea.
        const caret = start + text.length;
        el.setSelectionRange(caret, caret);

        el.dispatchEvent(new Event('input', { bubbles: true }));
        return true;
    }

    return { insertAtCursor };
})();
