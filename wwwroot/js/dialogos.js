(() => {
    const app = document.getElementById("app");
    if (!app) return;

    const modalSelector = '[aria-modal="true"]:is([role="dialog"], [role="alertdialog"])';
    const closeSelector = [
        "[data-dialog-close]", ".dice-close", ".dialog-close", ".modal-close-btn",
        ".btn-fechar", ".mobile-menu-fechar", ".fechar-popup", ".attack-close",
        ".ficha-modal-close", ".em-close", ".confirm-cancel"
    ].join(", ");
    const focusSelector = 'a[href], button, input, select, textarea, summary, [tabindex]';
    const openers = new WeakMap();
    const inertByDialog = new Set();
    let current = null;

    const usable = element => element instanceof HTMLElement &&
        !element.matches(":disabled, [hidden]") && !element.closest("[inert]") &&
        element.getClientRects().length > 0 && getComputedStyle(element).visibility === "visible";

    const focusable = dialog => [...dialog.querySelectorAll(focusSelector)]
        .filter(element => element.tabIndex >= 0 && usable(element));

    function focusInitial(dialog) {
        const preferred = dialog.querySelector("[data-dialog-initial]") ??
            (dialog.matches('[role="alertdialog"]') ? dialog.querySelector(".confirm-cancel") : null);
        if (usable(preferred)) {
            preferred.focus();
            return;
        }

        const first = focusable(dialog)[0];
        if (first) {
            first.focus();
            return;
        }

        if (!dialog.hasAttribute("tabindex")) dialog.tabIndex = -1;
        dialog.focus();
    }

    function updateModal() {
        const visible = [...app.querySelectorAll(modalSelector)]
            .filter(dialog => dialog.getClientRects().length > 0);
        const next = visible.at(-1) ?? null;
        if (current === next) return;

        const opener = current && openers.get(current);
        const previous = current;
        if (previous && !visible.includes(previous)) openers.delete(previous);
        for (const element of inertByDialog) {
            element.inert = false;
        }
        inertByDialog.clear();
        current = next;

        if (!next) {
            if (usable(opener)) opener.focus();
            return;
        }

        if (!openers.has(next)) openers.set(next, document.activeElement);
        const returningToParent = previous && !visible.includes(previous) && next.contains(opener);
        if (returningToParent && usable(opener)) opener.focus();
        else focusInitial(next);

        for (let branch = next; branch !== app; branch = branch.parentElement) {
            for (const sibling of branch.parentElement.children) {
                if (sibling !== branch && !sibling.inert) {
                    sibling.inert = true;
                    inertByDialog.add(sibling);
                }
            }
        }
    }

    const containsDialog = node => node instanceof Element &&
        (node.matches(modalSelector) || node.querySelector(modalSelector));

    new MutationObserver(records => {
        if (records.some(record => [...record.addedNodes, ...record.removedNodes].some(containsDialog)))
            updateModal();
    }).observe(app, { childList: true, subtree: true });

    window.addEventListener("resize", updateModal);

    document.addEventListener("focusin", event => {
        if (current && !current.contains(event.target)) focusInitial(current);
    });

    document.addEventListener("keydown", event => {
        if (!current) return;
        if (event.key === "Escape") {
            const close = [...current.querySelectorAll(closeSelector)].find(usable);
            if (close) {
                event.preventDefault();
                close.click();
            }
        }
        if (event.key !== "Tab") return;
        const items = focusable(current);
        const first = items[0];
        const last = items.at(-1);
        const focused = document.activeElement;
        if (items.length === 0 || !items.includes(focused) ||
            (event.shiftKey && focused === first) || (!event.shiftKey && focused === last)) {
            event.preventDefault();
            (event.shiftKey ? last : first)?.focus();
        }
    });

    updateModal();
})();
