// JS helpers called from Blazor components via IJSRuntime
window.secretSanta = {
    // Native share sheet – only on touch devices (phones/tablets) that support the Web Share API
    canShare: () =>
        typeof navigator.share === 'function' && window.matchMedia('(pointer: coarse)').matches,

    // Wakes up a sleeping API from the visitor's browser. Render refuses (HTTP 429) to wake a free service
    // when the request comes from another Render service, but requests from browsers wake it normally.
    // Fire-and-forget: the opaque no-cors response is ignored, only the request matters.
    wakeApi: (url) => {
        fetch(url, { mode: 'no-cors', cache: 'no-store', credentials: 'omit' }).catch(() => { });
    },

    // Returns false when the user cancels the share sheet or sharing fails
    share: async (title, text, url) => {
        try {
            await navigator.share({ title, text, url });
            return true;
        } catch {
            return false;
        }
    }
};
