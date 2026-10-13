// JS helpers called from Blazor components via IJSRuntime
window.secretSanta = {
    // Native share sheet – only on touch devices (phones/tablets) that support the Web Share API
    canShare: () =>
        typeof navigator.share === 'function' && window.matchMedia('(pointer: coarse)').matches,

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
