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
});

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
    const caseNumSelectors = [
        '[data-field-name="CaseNumber"] lightning-formatted-text',
        '[data-field-name="CaseNumber"] .uiOutputText',
        '.slds-page-header__title .uiOutputText',
        '.caseNumber',
        'span.test-id__field-value[data-aura-rendered-by*="CaseNumber"]'
    ];
    for (const sel of caseNumSelectors) {
        const el = document.querySelector(sel);
        if (el && el.innerText.trim()) {
            data.caseNumber = el.innerText.trim();
            break;
        }
    }

    // 2. Subject
    const subjectSelectors = [
        '[data-field-name="Subject"] lightning-formatted-text',
        '[data-field-name="Subject"] .uiOutputText',
        'span.test-id__field-value[data-aura-rendered-by*="Subject"]'
    ];
    for (const sel of subjectSelectors) {
        const el = document.querySelector(sel);
        if (el && el.innerText.trim()) {
            data.subject = el.innerText.trim();
            break;
        }
    }

    // 3. Description / Notes
    const descSelectors = [
        '[data-field-name="Description"] lightning-formatted-text',
        '[data-field-name="Description"] .uiOutputTextArea',
        '.slds-rich-text-editor__output',
        'span.test-id__field-value[data-aura-rendered-by*="Description"]'
    ];
    for (const sel of descSelectors) {
        const el = document.querySelector(sel);
        if (el && el.innerText.trim()) {
            data.description = el.innerText.trim();
            break;
        }
    }

    // 4. Contact Name
    const contactSelectors = [
        '[data-field-name="ContactId"] a',
        '[data-field-name="ContactId"] lightning-formatted-text',
        'a[data-refid="recordId"][href*="Contact"]'
    ];
    for (const sel of contactSelectors) {
        const el = document.querySelector(sel);
        if (el && el.innerText.trim()) {
            data.contactName = el.innerText.trim();
            break;
        }
    }

    // 5. Account / Dealership Name
    const accountSelectors = [
        '[data-field-name="AccountId"] a',
        '[data-field-name="AccountId"] lightning-formatted-text',
        'a[data-refid="recordId"][href*="Account"]'
    ];
    for (const sel of accountSelectors) {
        const el = document.querySelector(sel);
        if (el && el.innerText.trim()) {
            data.accountName = el.innerText.trim();
            break;
        }
    }

    // Full text fallback
    data.rawPageText = (data.subject + "\n\n" + data.description).trim();
    if (!data.rawPageText) {
        data.rawPageText = document.body ? document.body.innerText.substring(0, 4000) : "";
    }

    return data;
}

