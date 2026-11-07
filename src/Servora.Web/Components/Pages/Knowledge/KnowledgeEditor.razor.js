// Markdown editor helpers for the knowledge article editor (collocated JS module).

function notify(textarea) {
    textarea.dispatchEvent(new Event("input", { bubbles: true }));
    textarea.focus();
}

/** Wraps the current selection (or a placeholder) with before/after markers. */
export function wrap(textarea, before, after, placeholder) {
    if (!textarea) return;
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selected = textarea.value.substring(start, end) || placeholder;
    textarea.setRangeText(before + selected + after, start, end, "end");
    textarea.selectionStart = start + before.length;
    textarea.selectionEnd = start + before.length + selected.length;
    notify(textarea);
}

/** Inserts a block snippet on its own line at the cursor position. */
export function insertBlock(textarea, snippet) {
    if (!textarea) return;
    const start = textarea.selectionStart;
    const value = textarea.value;
    const needsBreak = start > 0 && value[start - 1] !== "\n";
    const text = (needsBreak ? "\n\n" : "") + snippet + "\n";
    textarea.setRangeText(text, start, textarea.selectionEnd, "end");
    notify(textarea);
}
