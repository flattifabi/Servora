// Servora client helpers. Kept intentionally small: UI logic lives in Blazor components.
window.servora = (() => {
    const THEME_KEY = "servora.theme";
    const SIDEBAR_KEY = "servora.sidebar";

    // Light is the default theme; "dark" and "system" are opt-in preferences.
    function resolveTheme(pref) {
        if (pref === "system") {
            return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
        }
        return pref === "dark" ? "dark" : "light";
    }

    function applyTheme(pref) {
        document.documentElement.setAttribute("data-theme", resolveTheme(pref || localStorage.getItem(THEME_KEY) || "light"));
    }

    // Applies a saved accent colour without a reload (values are validated server-side as #rrggbb).
    function applyBrand(accent, contrast) {
        let style = document.getElementById("servora-brand");
        if (!accent) { if (style) style.remove(); return; }
        if (!/^#[0-9a-f]{6}$/i.test(accent) || !/^#[0-9a-f]{6}$/i.test(contrast || "")) return;
        if (!style) { style = document.createElement("style"); style.id = "servora-brand"; document.head.appendChild(style); }
        style.textContent = `:root { --brand: ${accent}; --primary: ${accent}; --primary-contrast: ${contrast}; }`;
    }

    let paletteRef = null;
    document.addEventListener("keydown", (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
            e.preventDefault();
            if (paletteRef) paletteRef.invokeMethodAsync("Open");
        }
    });

    return {
        getTheme: () => localStorage.getItem(THEME_KEY) || "light",
        getAppliedTheme: () => document.documentElement.getAttribute("data-theme") || "light",
        applyBrand,
        setTheme: (pref) => { localStorage.setItem(THEME_KEY, pref); applyTheme(pref); },
        applyTheme,
        getSidebarCollapsed: () => localStorage.getItem(SIDEBAR_KEY) === "1",
        setSidebarCollapsed: (v) => localStorage.setItem(SIDEBAR_KEY, v ? "1" : "0"),
        registerPalette: (ref) => { paletteRef = ref; },
        focus: (el) => { if (el) el.focus(); },
        focusById: (id) => { const el = document.getElementById(id); if (el) el.focus(); },
        copy: async (text) => { try { await navigator.clipboard.writeText(text); return true; } catch { return false; } },
        download: (url) => { const a = document.createElement("a"); a.href = url; a.rel = "noopener"; document.body.appendChild(a); a.click(); a.remove(); },
        downloadText: (fileName, content, mime) => {
            const blob = new Blob([content], { type: mime || "text/plain" });
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a"); a.href = url; a.download = fileName; document.body.appendChild(a); a.click(); a.remove();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        },
        scrollToBottom: (el) => { if (el) el.scrollTop = el.scrollHeight; },
        hideSplash: () => { const s = document.getElementById("splash"); if (s) { s.classList.add("hidden"); setTimeout(() => s.remove(), 400); } },
    };
})();
