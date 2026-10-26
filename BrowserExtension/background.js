// Solera Case Management Extension - Background Service Worker
// Communicates with native host com.solera.cms.bridge

const HOST_NAME = "com.solera.cms.bridge";

chrome.runtime.onInstalled.addListener(() => {
    setupContextMenus();
});

chrome.runtime.onStartup.addListener(() => {
    setupContextMenus();
});

function setupContextMenus() {
    chrome.contextMenus.removeAll(() => {
        // --- 1. Selection Context (Send to Solera) ---
        chrome.contextMenus.create({
            id: "solera_send_parent",
            title: "Send to Solera",
            contexts: ["selection"]
        });

        chrome.contextMenus.create({
            id: "send_parse_case",
            parentId: "solera_send_parent",
            title: "Import as New Case",
            contexts: ["selection"]
        });

        chrome.contextMenus.create({
            id: "send_set_casenumber",
            parentId: "solera_send_parent",
            title: "Set Case Number",
            contexts: ["selection"]
        });

        chrome.contextMenus.create({
            id: "send_set_serverid",
            parentId: "solera_send_parent",
            title: "Set Server ID",
            contexts: ["selection"]
        });

        chrome.contextMenus.create({
            id: "send_set_dealership",
            parentId: "solera_send_parent",
            title: "Set Dealership",
            contexts: ["selection"]
        });

        chrome.contextMenus.create({
            id: "send_set_contact",
            parentId: "solera_send_parent",
            title: "Set Contact Name",
            contexts: ["selection"]
        });

        chrome.contextMenus.create({
            id: "send_parse_codeblock",
            title: "Send to Code Snippets",
            contexts: ["selection"]
        });

        // --- 2. Editable Context (Paste from Solera) ---
        chrome.contextMenus.create({
            id: "solera_paste_parent",
            title: "Paste from Solera",
            contexts: ["editable"]
        });

        chrome.contextMenus.create({
            id: "paste_server_id",
            parentId: "solera_paste_parent",
            title: "Server ID",
            contexts: ["editable"]
        });

        chrome.contextMenus.create({
            id: "paste_dealership",
            parentId: "solera_paste_parent",
            title: "Dealership",
            contexts: ["editable"]
        });

        chrome.contextMenus.create({
            id: "paste_case_number",
            parentId: "solera_paste_parent",
            title: "Case Number",
            contexts: ["editable"]
        });

        chrome.contextMenus.create({
            id: "paste_contact_name",
            parentId: "solera_paste_parent",
            title: "Contact Name",
            contexts: ["editable"]
        });

        chrome.contextMenus.create({
            id: "paste_template",
            parentId: "solera_paste_parent",
            title: "Active Template",
            contexts: ["editable"]
        });

        // --- 3. Page Context (Import Case) ---
        chrome.contextMenus.create({
            id: "solera_parse_full_case",
            title: "Import Case to Solera",
            contexts: ["page"]
        });
    });
}

chrome.contextMenus.onClicked.addListener((info, tab) => {
    const selection = (info.selectionText || "").trim();
    const tabId = tab && tab.id ? tab.id : null;

    switch (info.menuItemId) {
        case "send_parse_case":
            extractAndParseFullCase(tabId, "✓ Case imported into Solera");
            break;

        case "send_set_casenumber":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "SetDashboardField",
                payload: { field: "casenumber", value: selection }
            }, null, tabId, "✓ Case number updated in Solera");
            break;

        case "send_set_serverid":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "SetDashboardField",
                payload: { field: "serverid", value: selection }
            }, null, tabId, "✓ Server ID updated in Solera");
            break;

        case "send_set_dealership":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "SetDashboardField",
                payload: { field: "dealership", value: selection }
            }, null, tabId, "✓ Dealership updated in Solera");
            break;

        case "send_set_contact":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "SetDashboardField",
                payload: { field: "contactname", value: selection }
            }, null, tabId, "✓ Contact updated in Solera");
            break;

        case "send_parse_codeblock":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "ParseCodeBlock",
                payload: { code: selection }
            }, null, tabId, "✓ Sent to Code Snippets");
            break;

        case "paste_server_id":
        case "paste_dealership":
        case "paste_case_number":
        case "paste_contact_name":
        case "paste_template":
            handlePasteFromSolera(info.menuItemId, tab);
            break;

        case "solera_parse_full_case":
            extractAndParseFullCase(tabId, "✓ Case imported into Solera");
            break;
    }
});

function extractAndParseFullCase(tabId, successMessage) {
    if (!tabId) return;
    sendTabMessageWithAutoInject(tabId, { action: "EXTRACT_CASE_DATA" }, (res) => {
        if (res && res.caseData) {
            const d = res.caseData;
            sendIpcMessage({
                target: "SoleraCMS",
                action: "LoadCase",
                payload: {
                    caseNumber: d.caseNumber || "",
                    dealerName: d.accountName || "",
                    dealership: d.accountName || "",
                    serverCode: d.serverCode || "",
                    contactName: d.contactName || "",
                    contactEmail: d.contactEmail || "",
                    contactPhone: d.contactPhone || "",
                    subject: d.subject || "",
                    notes: d.description || ""
                },
                rawData: d.rawPageText || d.description || ""
            }, null, tabId, successMessage || "✓ Case imported into Solera");
        } else {
            notifyTabToast(tabId, "⚠ Could not read page content for case extraction.", true);
        }
    });
}

function handlePasteFromSolera(menuItemId, tab) {
    if (!tab || !tab.id) return;

    sendIpcMessage({
        target: "SoleraCMS",
        action: "GetLiveVariables"
    }, (response) => {
        if (!response || !response.success || !response.data) {
            console.warn("Failed to get variables from Solera CMS:", response);
            return;
        }

        const data = response.data;
        let textToInsert = "";

        switch (menuItemId) {
            case "paste_server_id":
                textToInsert = data.ServerId || "";
                break;
            case "paste_dealership":
                textToInsert = data.DealershipName || "";
                break;
            case "paste_case_number":
                textToInsert = data.CaseNumber || "";
                break;
            case "paste_contact_name":
                textToInsert = data.ContactName || "";
                break;
            case "paste_template":
                textToInsert = data.ActiveTemplateText || "";
                break;
        }

        if (textToInsert) {
            sendTabMessageWithAutoInject(tab.id, {
                action: "INSERT_TEXT",
                text: textToInsert
            });
        } else {
            notifyTabToast(tab.id, "⚠ This field is currently empty in Solera.", true);
        }
    }, tab.id);
}

function sendTabMessageWithAutoInject(tabId, message, callback) {
    if (!tabId) return;
    chrome.tabs.sendMessage(tabId, message, (res) => {
        const err = chrome.runtime.lastError;
        if (!err) {
            if (callback) callback(res);
            return;
        }
        // Content script not injected yet (e.g., tab was already open before extension load or non-Salesforce tab)
        if (chrome.scripting) {
            chrome.scripting.executeScript({
                target: { tabId: tabId },
                files: ["content.js"]
            }, () => {
                if (chrome.runtime.lastError) {
                    console.warn("Could not inject content.js:", chrome.runtime.lastError.message);
                    if (callback) callback(null);
                    return;
                }
                chrome.tabs.sendMessage(tabId, message, (retryRes) => {
                    const retryErr = chrome.runtime.lastError;
                    if (callback) callback(retryErr ? null : retryRes);
                });
            });
        } else if (callback) {
            callback(null);
        }
    });
}

function notifyTabToast(tabId, text, isError) {
    if (!tabId) return;
    sendTabMessageWithAutoInject(tabId, {
        action: "SHOW_TOAST",
        text: text,
        isError: !!isError
    });
}

function setBadgeStatus(text, color, clearAfterMs) {
    try {
        if (!chrome.action) return;
        chrome.action.setBadgeBackgroundColor({ color: color });
        chrome.action.setBadgeText({ text: text });
        if (clearAfterMs) {
            setTimeout(() => {
                try { chrome.action.setBadgeText({ text: "" }); } catch (e) {}
            }, clearAfterMs);
        }
    } catch (e) {}
}

function sendIpcMessage(message, callback, tabId, successToast) {
    try {
        chrome.runtime.sendNativeMessage(HOST_NAME, message, (response) => {
            const err = chrome.runtime.lastError;
            if (err) {
                console.warn("Communication error:", err.message);
                setBadgeStatus("ERR", "#dc2626", 6000);
                notifyTabToast(tabId, "⚠ Solera Case Management is not running. Please open the Solera app.", true);
                if (callback) callback({ success: false, message: err.message });
                return;
            }
            if (response && response.success === false) {
                setBadgeStatus("ERR", "#dc2626", 5000);
                notifyTabToast(tabId, "⚠ Solera: " + (response.message || "Operation failed"), true);
            } else {
                setBadgeStatus("✓", "#16a34a", 2500);
                if (successToast) {
                    notifyTabToast(tabId, successToast, false);
                }
            }
            if (callback) callback(response);
        });
    } catch (ex) {
        console.error("Exception in sendNativeMessage:", ex);
        setBadgeStatus("ERR", "#dc2626", 6000);
        notifyTabToast(tabId, "⚠ Unable to communicate with Solera Case Management.", true);
        if (callback) callback({ success: false, message: ex.toString() });
    }
}

