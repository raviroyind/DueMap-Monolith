/*
 * Submit-time busy state for full-page-POST forms (the Razor Pages auth
 * screens, sign-out, SSO handoffs).
 *
 * Blazor pages don't need this — their buttons own an explicit busy flag via
 * the LoadingButton component, because only the component knows when the async
 * work finished. A full POST navigates away, so here the state never has to be
 * cleared: the next document is a fresh page.
 *
 * Scope note: this deliberately only fires on real form submissions. Attaching
 * to every click would spin filter chips and pagination, which complete far too
 * fast to warrant it.
 */
(function () {
    'use strict';

    // Capture phase, so we set the state even if a later handler stops
    // propagation on its way up.
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement)) return;

        // Let a form opt out: <form data-no-busy>.
        if (form.hasAttribute('data-no-busy')) return;

        // If the browser rejected the form on constraint validation, nothing is
        // being sent and the user stays here — don't lock the button.
        if (typeof form.checkValidity === 'function' && !form.checkValidity()) return;

        // Guard against a second submit slipping through before navigation.
        if (form.dataset.busy === '1') {
            event.preventDefault();
            return;
        }
        form.dataset.busy = '1';

        // event.submitter tells us which button was actually used, which
        // matters on forms with more than one (e.g. asp-page-handler pairs).
        var button = event.submitter
            || form.querySelector('button[type="submit"], input[type="submit"]');
        if (!button) return;

        if (button.classList.contains('btn')) {
            button.classList.add('is-loading');
        }

        // Disabling a submitter before the browser serialises the form would
        // drop its name/value from the POST, so defer to the next task — by
        // which point the submission has been assembled.
        window.setTimeout(function () {
            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
        }, 0);
    }, true);

    // Restoring from bfcache (back button) replays the old DOM, busy state and
    // all, leaving a permanently spinning button. Clear it.
    window.addEventListener('pageshow', function (event) {
        if (!event.persisted) return;
        document.querySelectorAll('form[data-busy="1"]').forEach(function (form) {
            delete form.dataset.busy;
        });
        document.querySelectorAll('.btn.is-loading').forEach(function (button) {
            button.classList.remove('is-loading');
            button.disabled = false;
            button.removeAttribute('aria-busy');
        });
    });
})();
