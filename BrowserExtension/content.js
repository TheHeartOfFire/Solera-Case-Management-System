// Solera CMS & Citadel Assistant - Content Script
// Handles DOM scraping for Salesforce Lightning/Classic and text insertion.

let lastActiveElement = null;

// Track the last focused editable element for context menu pastes
document.addEventListener("focusin", (e) => {
    if (isEditable(e.target)) {
        lastActiveElement = e.target;
    }
}, true);

document.addEventListener("contextmenu", (e) => {
    if (isEditable(e.target)) {
        lastActiveElement = e.target;
    }
}, true);

function isEditable(el) {
    if (!el) return false;
    const tagName = el.tagName ? el.tagName.toUpperCase() : "";
    return (
        tagName === "INPUT" ||
        tagName === "TEXTAREA" ||
        el.isContentEditable ||
        el.getAttribute("contenteditable") === "true" ||
        el.getAttribute("role") === "textbox"
    );
}

// Listen for messages from background script
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.action === "INSERT_TEXT") {
        insertTextIntoElement(lastActiveElement || document.activeElement, message.text);
        sendResponse({ success: true });
        return true;
    }

    if (message.action === "EXTRACT_CASE_DATA") {
        const caseData = extractSalesforceCase();
        sendResponse({ success: true, caseData: caseData });
        return true;
    }

    if (message.action === "SHOW_TOAST") {
        showSoleraToast(message.text, message.isError);
        sendResponse({ success: true });
        return true;
    }
});

function showSoleraToast(text, isError) {
    try {
        const existing = document.getElementById("solera-cms-toast");
        if (existing) existing.remove();

        const toast = document.createElement("div");
        toast.id = "solera-cms-toast";
        toast.textContent = text;
        Object.assign(toast.style, {
            position: "fixed",
            bottom: "24px",
            right: "24px",
            zIndex: "2147483647",
            padding: "12px 18px",
            borderRadius: "8px",
            fontFamily: "'Segoe UI', system-ui, sans-serif",
            fontSize: "13px",
            fontWeight: "600",
            color: "#ffffff",
            backgroundColor: isError ? "#dc2626" : "#1d4ed8",
            boxShadow: "0 8px 24px rgba(0,0,0,0.35)",
            border: isError ? "1px solid #f87171" : "1px solid #60a5fa",
            transition: "opacity 0.25s ease",
            pointerEvents: "none"
        });
        document.body.appendChild(toast);
        setTimeout(() => {
            toast.style.opacity = "0";
            setTimeout(() => toast.remove(), 300);
        }, isError ? 5000 : 2800);
    } catch (e) {}
}

function insertTextIntoElement(target, text) {
    if (!target) return;
    target.focus();

    if (target.isContentEditable || target.getAttribute("contenteditable") === "true") {
        document.execCommand("insertText", false, text);
        return;
    }

    if (typeof target.selectionStart === "number" && typeof target.selectionEnd === "number") {
        const start = target.selectionStart;
        const end = target.selectionEnd;
        const value = target.value || "";
        target.value = value.substring(0, start) + text + value.substring(end);
        target.selectionStart = target.selectionEnd = start + text.length;

        // Dispatch synthetic events so frameworks like React or Lightning detect the change
        target.dispatchEvent(new Event("input", { bubbles: true }));
        target.dispatchEvent(new Event("change", { bubbles: true }));
    } else {
        document.execCommand("insertText", false, text);
    }
}

function isElementVisible(el) {
    if (!el) return false;
    if (el.closest('[aria-hidden="true"], .slds-hide, .hidden')) return false;
    return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
}

function queryFirstVisibleText(selectors) {
    let fallbackText = "";
    for (const sel of selectors) {
        let elements;
        try {
            elements = document.querySelectorAll(sel);
        } catch (e) {
            continue;
        }
        for (const el of elements) {
            const text = (el.innerText || el.textContent || "").trim();
            if (!text) continue;
            if (isElementVisible(el)) {
                return text;
            }
            if (!fallbackText) {
                fallbackText = text;
            }
        }
    }
    return fallbackText;
}

function extractSalesforceCase() {
    const data = {
        caseNumber: "",
        subject: "",
        description: "",
        contactName: "",
        accountName: "",
        rawPageText: ""
    };

    // 1. Case Number
    data.caseNumber = queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.CaseNumber"] lightning-formatted-text',
        'records-record-layout-item[field-label="Case Number"] lightning-formatted-text',
        '[data-field-name="CaseNumber"] lightning-formatted-text',
        '[data-field-name="CaseNumber"] .uiOutputText',
        '.slds-page-header__title lightning-formatted-text',
        '.slds-page-header__title .uiOutputText',
        '.caseNumber',
        'span.test-id__field-value[data-aura-rendered-by*="CaseNumber"]'
    ]);

    if (!data.caseNumber && document.title) {
        const titleMatch = document.title.match(/^(\d{5,10})\s*\|/);
        if (titleMatch) {
            data.caseNumber = titleMatch[1];
        }
    }

    // 2. Subject
    data.subject = queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.Subject"] lightning-formatted-text',
        'records-record-layout-item[field-label="Subject"] lightning-formatted-text',
        '[data-field-name="Subject"] lightning-formatted-text',
        '[data-field-name="Subject"] .uiOutputText',
        'span.test-id__field-value[data-aura-rendered-by*="Subject"]'
    ]);

    // 3. Description / Notes
    data.description = queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.Description"] lightning-formatted-text',
        'records-record-layout-item[field-label="Description"] lightning-formatted-text',
        '[data-field-name="Description"] lightning-formatted-text',
        '[data-field-name="Description"] .uiOutputTextArea',
        '.slds-rich-text-editor__output',
        'span.test-id__field-value[data-aura-rendered-by*="Description"]'
    ]);

    // 4. Contact Name
    data.contactName = queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.ContactId"] a',
        'records-record-layout-item[field-label="Contact Name"] a',
        'records-record-layout-item[field-label="Contact Name"] lightning-formatted-text',
        '[data-field-name="ContactId"] a',
        '[data-field-name="ContactId"] lightning-formatted-text',
        'a[data-refid="recordId"][href*="Contact"]'
    ]);

    // 5. Account / Dealership Name
    data.accountName = queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.AccountId"] a',
        'records-record-layout-item[field-label="Account Name"] a',
        'records-record-layout-item[field-label="Account Name"] lightning-formatted-text',
        '[data-field-name="AccountId"] a',
        '[data-field-name="AccountId"] lightning-formatted-text',
        'a[data-refid="recordId"][href*="Account"]'
    ]);

    // Build structured text block compatible with DashboardViewModel.ParseCaseText
    const structuredLines = [];
    if (data.caseNumber) {
        structuredLines.push("Case Number", data.caseNumber);
    }
    if (data.contactName) {
        structuredLines.push("Contact Name", data.contactName);
    }
    if (data.subject) {
        structuredLines.push("Subject", data.subject);
    }
    if (data.description) {
        structuredLines.push("Description", data.description);
    }

    if (structuredLines.length > 0 && data.description) {
        data.rawPageText = structuredLines.join("\n");
    } else {
        const selectionText = window.getSelection ? window.getSelection().toString().trim() : "";
        const bodyText = document.body ? document.body.innerText.substring(0, 12000) : "";
        data.rawPageText = [structuredLines.join("\n"), selectionText || bodyText].filter(Boolean).join("\n\n").trim();
    }

    return data;
}

