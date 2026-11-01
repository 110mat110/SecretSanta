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

    // Groups the user opened (e.g. from an invite link), kept in a cookie in this browser only
    getVisitedGroups: () => {
        const match = document.cookie.match(/(?:^|;\s*)ss_groups=([^;]*)/);
        if (!match) return [];
        return decodeURIComponent(match[1]).split(',').filter(id => /^[0-9a-f-]{36}$/i.test(id));
    },

    addVisitedGroup: (groupId) => window.secretSanta.saveVisitedGroups([groupId, ...window.secretSanta.getVisitedGroups()]),

    removeVisitedGroup: (groupId) => window.secretSanta.saveVisitedGroups(window.secretSanta.getVisitedGroups()
        .filter(id => id.toLowerCase() !== groupId.toLowerCase())),

    saveVisitedGroups: (groupIds) => {
        const ids = [...new Map(groupIds.map(id => [id.toLowerCase(), id])).values()];
        // ~50 GUIDs fit well under the 4 KB cookie limit
        const value = encodeURIComponent(ids.slice(0, 50).join(','));
        document.cookie = `ss_groups=${value}; path=/; max-age=${60 * 60 * 24 * 365}; SameSite=Lax; Secure`;
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
