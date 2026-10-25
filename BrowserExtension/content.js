// Solera Case Management - Content Script
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

function cleanFieldValue(text) {
    if (!text) return "";
    let clean = text.trim();
    clean = clean.replace(/^Edit\s+/i, "");
    clean = clean.replace(/\s+Help$/i, "");
    return clean.trim();
}

function isLabelText(text, patterns = []) {
    if (!text) return true;
    const lower = text.trim().toLowerCase();
    const commonLabels = [
        "case number", "case", "subject", "description", "contact name", "contact", 
        "account name", "account", "dealership", "dealer", "phone", "contact phone", 
        "mobile", "contact mobile", "business phone", "email", "contact email", 
        "server code", "server id", "automate server code", "auto/mate server code", 
        "am server code", "server"
    ];
    if (commonLabels.includes(lower)) return true;
    for (const p of patterns) {
        if (typeof p === "string" && lower === p.toLowerCase()) return true;
        if (p instanceof RegExp && p.test(lower) && lower.length < 35) return true;
    }
    return false;
}

function queryFirstVisibleText(selectors, invalidPatterns = []) {
    let fallbackText = "";
    for (const sel of selectors) {
        let elements;
        try {
            elements = document.querySelectorAll(sel);
        } catch (e) {
            continue;
        }
        for (const el of elements) {
            // Guard: Never pick up label elements or buttons
            if (el.closest('.test-id__field-label, .slds-form-element__label, label, button, .slds-button')) continue;
            const text = cleanFieldValue(el.innerText || el.textContent);
            if (!text || isLabelText(text, invalidPatterns)) continue;
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

function findFieldByLabel(labelPatterns, preferredSubSelectors = []) {
    // 1. Search records-record-layout-item elements by attribute field-label
    const layoutItems = document.querySelectorAll("records-record-layout-item[field-label]");
    for (const item of layoutItems) {
        if (!isElementVisible(item)) continue;
        const attr = (item.getAttribute("field-label") || "").trim();
        for (const pattern of labelPatterns) {
            const matches = typeof pattern === "string"
                ? attr.toLowerCase() === pattern.toLowerCase()
                : pattern.test(attr);
            if (matches) {
                // Look strictly inside the value container (.test-id__field-value or .slds-form-element__control)
                const valContainer = item.querySelector(".test-id__field-value, .slds-form-element__control, [data-field-value]");
                if (valContainer && isElementVisible(valContainer)) {
                    for (const subSel of preferredSubSelectors) {
                        const subEl = valContainer.querySelector(subSel);
                        if (subEl && isElementVisible(subEl)) {
                            const val = cleanFieldValue(subEl.innerText || subEl.textContent);
                            if (val && !isLabelText(val, labelPatterns)) return val;
                        }
                    }
                    const valEl = valContainer.querySelector("lightning-formatted-phone, lightning-formatted-email, lightning-formatted-text, .uiOutputText, .uiOutputTextArea, .slds-rich-text-editor__output, a:not(.slds-button)");
                    const val = cleanFieldValue(valEl ? (valEl.innerText || valEl.textContent) : (valContainer.innerText || valContainer.textContent));
                    if (val && !isLabelText(val, labelPatterns)) return val;
                }
            }
        }
    }

    // 2. Search .slds-form-element containers by label text
    const formElements = document.querySelectorAll(".slds-form-element");
    for (const fe of formElements) {
        if (!isElementVisible(fe)) continue;
        const labelEl = fe.querySelector(".slds-form-element__label, .test-id__field-label, label");
        if (!labelEl) continue;
        const labelText = (labelEl.innerText || labelEl.textContent || "").trim();
        for (const pattern of labelPatterns) {
            const matches = typeof pattern === "string"
                ? labelText.toLowerCase() === pattern.toLowerCase()
                : pattern.test(labelText);
            if (matches) {
                const control = fe.querySelector(".slds-form-element__control, .test-id__field-value");
                if (control && isElementVisible(control)) {
                    for (const subSel of preferredSubSelectors) {
                        const subEl = control.querySelector(subSel);
                        if (subEl && isElementVisible(subEl)) {
                            const val = cleanFieldValue(subEl.innerText || subEl.textContent);
                            if (val && !isLabelText(val, labelPatterns)) return val;
                        }
                    }
                    const valEl = control.querySelector("lightning-formatted-phone, lightning-formatted-email, lightning-formatted-text, .uiOutputText, .uiOutputTextArea, .slds-rich-text-editor__output, a:not(.slds-button)");
                    const val = cleanFieldValue(valEl ? (valEl.innerText || valEl.textContent) : (control.innerText || control.textContent));
                    if (val && !isLabelText(val, labelPatterns)) return val;
                }
            }
        }
    }

    return "";
}

function findFieldInPopovers(labelPatterns) {
    const popovers = document.querySelectorAll(
        '.slds-popover, [role="dialog"], force-hoverable-popover, .forceHoverPanel, .cuf-compact-feed, .uiPanel, .panel-content'
    );
    for (const popover of popovers) {
        try {
            const style = window.getComputedStyle(popover);
            if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") continue;
        } catch (e) {}

        const labelCandidates = popover.querySelectorAll(
            'dt, .slds-item_label, .slds-text-title, .slds-form-element__label, .test-id__field-label, [class*="label"], p, span, div'
        );
        for (const el of labelCandidates) {
            const txt = cleanFieldValue(el.innerText || el.textContent);
            if (!txt) continue;
            for (const pattern of labelPatterns) {
                const matches = typeof pattern === "string"
                    ? txt.toLowerCase() === pattern.toLowerCase()
                    : pattern.test(txt);
                if (matches) {
                    let sibling = el.nextElementSibling;
                    if (sibling) {
                        const val = cleanFieldValue(sibling.innerText || sibling.textContent);
                        if (val && !isLabelText(val, labelPatterns)) return val;
                    }
                    const parent = el.parentElement;
                    if (parent) {
                        const valEls = parent.querySelectorAll('dd, .slds-item_detail, .slds-text-body_regular, lightning-formatted-text, .uiOutputText');
                        for (const valEl of valEls) {
                            if (valEl !== el) {
                                const val = cleanFieldValue(valEl.innerText || valEl.textContent);
                                if (val && !isLabelText(val, labelPatterns)) return val;
                            }
                        }
                    }
                }
            }
        }
    }
    return "";
}

function extractSalesforceCase() {
    const data = {
        caseNumber: "",
        subject: "",
        description: "",
        contactName: "",
        contactPhone: "",
        contactEmail: "",
        accountName: "",
        accountId: "",
        serverCode: "",
        rawPageText: ""
    };

    // 1. Case Number
    data.caseNumber = findFieldByLabel(["Case Number"]) || queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.CaseNumber"] lightning-formatted-text',
        'records-record-layout-item[field-label="Case Number"] .test-id__field-value lightning-formatted-text',
        'records-record-layout-item[field-label="Case Number"] .slds-form-element__control lightning-formatted-text',
        '[data-field-name="CaseNumber"] lightning-formatted-text',
        '[data-field-name="CaseNumber"] .uiOutputText',
        '.slds-page-header__title lightning-formatted-text'
    ], ["Case Number"]);

    if ((!data.caseNumber || isLabelText(data.caseNumber)) && document.title) {
        const titleMatch = document.title.match(/^(\d{5,10})\s*\|/);
        if (titleMatch) {
            data.caseNumber = titleMatch[1];
        }
    }

    // 2. Subject
    data.subject = findFieldByLabel(["Subject"]) || queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.Subject"] lightning-formatted-text',
        'records-record-layout-item[field-label="Subject"] .test-id__field-value lightning-formatted-text',
        'records-record-layout-item[field-label="Subject"] .slds-form-element__control lightning-formatted-text',
        '[data-field-name="Subject"] lightning-formatted-text',
        '[data-field-name="Subject"] .uiOutputText'
    ], ["Subject"]);

    // 3. Description / Notes
    data.description = findFieldByLabel(["Description"]) || queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.Description"] lightning-formatted-text',
        'records-record-layout-item[field-label="Description"] .test-id__field-value lightning-formatted-text',
        'records-record-layout-item[field-label="Description"] .test-id__field-value .uiOutputTextArea',
        'records-record-layout-item[field-label="Description"] .slds-rich-text-editor__output',
        '[data-field-name="Description"] lightning-formatted-text',
        '[data-field-name="Description"] .uiOutputTextArea'
    ], ["Description"]);

    // 4. Contact Name (Case Contact Record)
    data.contactName = findFieldByLabel(["Contact Name", "Contact"], ["a[data-refid='recordId']", "a"]) || queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.ContactId"] a',
        'records-record-layout-item[field-label="Contact Name"] .test-id__field-value a',
        '[data-field-name="ContactId"] a',
        'a[data-refid="recordId"][href*="Contact"]'
    ], ["Contact Name", "Contact"]);

    // 5. Contact Phone (Case Contact Record Phone)
    let phoneVal = findFieldByLabel(
        ["Contact Phone", "Phone", "Contact Mobile", "Mobile", "Business Phone"],
        ["lightning-formatted-phone a", "a[href^='tel:']", "lightning-formatted-phone", "lightning-formatted-text"]
    ) || queryFirstVisibleText([
        '[data-target-selection-name*="Contact.Phone"] a',
        '[data-target-selection-name*="ContactPhone"] a',
        'records-record-layout-item[field-label*="Phone"] .test-id__field-value a[href^="tel:"]',
        'records-record-layout-item[field-label*="Phone"] .test-id__field-value lightning-formatted-phone',
        '[data-field-name="ContactPhone"] a',
        '[data-field-name="ContactPhone"] lightning-formatted-phone',
        'a[href^="tel:"]'
    ], ["Contact Phone", "Phone", "Mobile"]);
    if (phoneVal) {
        phoneVal = phoneVal.replace(/^tel:/i, "").trim();
    }
    data.contactPhone = isLabelText(phoneVal) ? "" : phoneVal;

    // 6. Contact Email (Case Contact Record Email)
    let emailVal = findFieldByLabel(
        ["Contact Email", "Email", "Contact Email Address"],
        ["lightning-formatted-email a", "a[href^='mailto:']", "lightning-formatted-email", "lightning-formatted-text"]
    ) || queryFirstVisibleText([
        '[data-target-selection-name*="Contact.Email"] a',
        '[data-target-selection-name*="ContactEmail"] a',
        'records-record-layout-item[field-label*="Email"] .test-id__field-value a[href^="mailto:"]',
        'records-record-layout-item[field-label*="Email"] .test-id__field-value lightning-formatted-email',
        '[data-field-name="ContactEmail"] a',
        '[data-field-name="ContactEmail"] lightning-formatted-email',
        'a[href^="mailto:"]'
    ], ["Contact Email", "Email"]);
    if (emailVal) {
        emailVal = emailVal.replace(/^mailto:/i, "").trim();
    }
    data.contactEmail = isLabelText(emailVal) ? "" : emailVal;

    // 7. Account / Dealership Name (Case Account Record)
    data.accountName = findFieldByLabel(["Account Name", "Account", "Dealership", "Dealership Name"], ["a[data-refid='recordId']", "a"]) || queryFirstVisibleText([
        '[data-target-selection-name="sfdc:RecordField.Case.AccountId"] a',
        'records-record-layout-item[field-label="Account Name"] .test-id__field-value a',
        '[data-field-name="AccountId"] a',
        'a[data-refid="recordId"][href*="Account"]'
    ], ["Account Name", "Account", "Dealership"]);

    // Robust Account ID extraction (15 or 18 char Salesforce ID starting with 001)
    const accountLinkSelectors = [
        '[data-target-selection-name*="AccountId"] a',
        'records-record-layout-item[field-label*="Account" i] a',
        '[data-field-name*="AccountId" i] a',
        'a[href*="/Account/"]',
        'a[href*="/001"]',
        'a[data-recordid^="001"]',
        'a[data-refid="recordId"]'
    ];
    for (const sel of accountLinkSelectors) {
        const links = document.querySelectorAll(sel);
        for (const a of links) {
            const idAttr = a.getAttribute('data-recordid') || a.getAttribute('data-id');
            if (idAttr && /^001[a-zA-Z0-9]{12,15}$/.test(idAttr)) {
                data.accountId = idAttr;
                break;
            }
            const href = a.getAttribute('href') || a.href || '';
            const hrefMatch = href.match(/[\/=](001[a-zA-Z0-9]{12,15})(?:[\/\?&#]|$)/);
            if (hrefMatch) {
                data.accountId = hrefMatch[1];
                break;
            }
        }
        if (data.accountId) break;
    }

    if (!data.accountId) {
        const el001 = document.querySelector('[data-recordid^="001"]');
        if (el001) {
            data.accountId = el001.getAttribute('data-recordid');
        }
    }

    // 8. Automate Server Code (from Case / Account record)
    data.serverCode = findFieldByLabel(
        [/automate\s*server/i, /auto\/mate\s*server/i, /server\s*code/i, /server\s*id/i, "Automate Server Code", "Auto/Mate Server Code", "Server Code", "Server ID", "AM Server Code", "Server"],
        ["lightning-formatted-text", ".uiOutputText", "span"]
    ) || queryFirstVisibleText([
        '[data-target-selection-name*="Server_Code"] lightning-formatted-text',
        '[data-target-selection-name*="Server"] lightning-formatted-text',
        'records-record-layout-item[field-label*="Server" i] .test-id__field-value lightning-formatted-text',
        '[data-field-name*="Server_Code"] lightning-formatted-text',
        '[data-field-name*="Server"] lightning-formatted-text'
    ], [/server/i, "Server", "Server Code", "Server ID"]);

    // If server code not found on main page, check any active compact layout popovers
    if (!data.serverCode) {
        data.serverCode = findFieldInPopovers([
            /automate\s*server/i, /auto\/mate\s*server/i, /server\s*code/i, /server\s*id/i,
            "Automate Server Code", "Auto/Mate Server Code", "Server Code", "Server ID"
        ]);
    }

    // Build structured text block compatible with DashboardViewModel.ParseCaseText
    // Note: Contact and Dealership records are NOT passed here so ParseCaseText parses
    // any description fields first; IpcService uses contact/account record values as fallbacks.
    const structuredLines = [];
    if (data.caseNumber && !isLabelText(data.caseNumber)) {
        structuredLines.push("Case Number", data.caseNumber);
    }
    if (data.subject && !isLabelText(data.subject)) {
        structuredLines.push("Subject", data.subject);
    }
    if (data.description && !isLabelText(data.description)) {
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

