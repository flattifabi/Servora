// API Explorer helpers: requests run in the browser so that they use the signed-in user's cookie.
// Only same-origin relative URLs are accepted (the page passes paths below /api/ or /openapi/).

function assertRelative(url) {
    if (typeof url !== "string" || !url.startsWith("/") || url.startsWith("//")) {
        throw new Error("Only same-origin relative URLs are allowed.");
    }
}

export async function fetchText(url) {
    assertRelative(url);
    const response = await fetch(url, { credentials: "same-origin", headers: { "Accept": "application/json" } });
    return { status: response.status, statusText: response.statusText, durationMs: 0, headers: "", body: await response.text(), error: null };
}

export async function send(method, url, body, contentType) {
    assertRelative(url);
    const started = performance.now();
    try {
        const init = { method, credentials: "same-origin", headers: { "Accept": "application/json" } };
        if (body && method !== "GET" && method !== "HEAD") {
            init.body = body;
            init.headers["Content-Type"] = contentType || "application/json";
        }
        const response = await fetch(url, init);
        let text = await response.text();
        if (text.length > 200000) {
            text = text.slice(0, 200000) + "\n…[truncated]";
        }
        const headers = [];
        response.headers.forEach((value, key) => headers.push(key + ": " + value));
        return {
            status: response.status,
            statusText: response.statusText,
            durationMs: Math.round(performance.now() - started),
            headers: headers.join("\n"),
            body: text,
            error: null,
        };
    } catch (e) {
        return { status: 0, statusText: "", durationMs: Math.round(performance.now() - started), headers: "", body: "", error: String(e) };
    }
}
