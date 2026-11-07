// AI assistant composer: Enter sends, Shift+Enter inserts a newline; the textarea grows with its content.
export function registerComposer(textarea, dotnetRef) {
    if (!textarea) return;
    const resize = () => {
        textarea.style.height = "auto";
        textarea.style.height = Math.min(textarea.scrollHeight, 220) + "px";
    };
    textarea.addEventListener("input", resize);
    textarea.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
            e.preventDefault();
            dotnetRef.invokeMethodAsync("SubmitFromKeyboard", textarea.value);
        }
    });
    resize();
}

export function resetComposer(textarea) {
    if (!textarea) return;
    textarea.value = "";
    textarea.style.height = "auto";
    textarea.focus();
}

export function scrollToBottom(element) {
    if (element) element.scrollTo({ top: element.scrollHeight, behavior: "smooth" });
}
