// Solera CMS & Citadel Assistant - Background Service Worker
// Communicates with native host com.solera.cms.bridge

const HOST_NAME = "com.solera.cms.bridge";

chrome.runtime.onInstalled.addListener(() => {
    setupContextMenus();
});

function setupContextMenus() {
    chrome.contextMenus.removeAll(() => {
        // --- 1. Selection Context (Send to Solera / Citadel) ---
        chrome.contextMenus.create({
            id: "solera_send_parent",
            title: "Send to Solera Dashboard",
            contexts: ["selection"]
        });

        chrome.contextMenus.create({
            id: "send_parse_case",
            parentId: "solera_send_parent",
            title: "Parse as New Case",
            contexts: ["selection"]
        });

        chrome.contextMenus.create({
            id: "send_set_casenumber",
            parentId: "solera_send_parent",
            title: "Set Case #",
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
            id: "send_citadel_capture",
            parentId: "solera_send_parent",
            title: "Citadel: Quick Capture Selection",
            contexts: ["selection"]
        });

        // --- 2. Editable Context (Paste from Solera) ---
        chrome.contextMenus.create({
            id: "solera_paste_parent",
            title: "Paste from Solera Dashboard",
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
            title: "Case #",
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
            title: "Active Template Text",
            contexts: ["editable"]
        });

        // --- 3. Page Context (Parse Full Case) ---
        chrome.contextMenus.create({
            id: "solera_parse_full_case",
            title: "Solera CMS: Parse Full Case Notes",
            contexts: ["page"]
        });
    });
}

chrome.contextMenus.onClicked.addListener((info, tab) => {
    const selection = (info.selectionText || "").trim();

    switch (info.menuItemId) {
        case "send_parse_case":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "LoadCase",
                rawData: selection
            });
            break;

        case "send_set_casenumber":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "SetDashboardField",
                payload: { field: "casenumber", value: selection }
            });
            break;

        case "send_set_serverid":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "SetDashboardField",
                payload: { field: "serverid", value: selection }
            });
            break;

        case "send_set_dealership":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "SetDashboardField",
                payload: { field: "dealership", value: selection }
            });
            break;

        case "send_set_contact":
            sendIpcMessage({
                target: "SoleraCMS",
                action: "SetDashboardField",
                payload: { field: "contactname", value: selection }
            });
            break;

        case "send_citadel_capture":
            sendIpcMessage({
                target: "Citadel",
                action: "BrainDump",
                payload: { note: selection, url: tab ? tab.url : "" }
            });
            break;

        case "paste_server_id":
        case "paste_dealership":
        case "paste_case_number":
        case "paste_contact_name":
        case "paste_template":
            handlePasteFromSolera(info.menuItemId, tab);
            break;

        case "solera_parse_full_case":
            if (tab && tab.id) {
                chrome.tabs.sendMessage(tab.id, { action: "EXTRACT_CASE_DATA" }, (res) => {
                    if (res && res.caseData) {
                        const d = res.caseData;
                        sendIpcMessage({
                            target: "SoleraCMS",
                            action: "LoadCase",
                            payload: {
                                caseNumber: d.caseNumber,
                                dealership: d.accountName,
                                contactName: d.contactName,
                                subject: d.subject
                            },
                            rawData: d.rawPageText || d.description
                        });
                    }
                });
            }
            break;
    }
});

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
            chrome.tabs.sendMessage(tab.id, {
                action: "INSERT_TEXT",
                text: textToInsert
            });
        }
    });
}

function sendIpcMessage(message, callback) {
    try {
        chrome.runtime.sendNativeMessage(HOST_NAME, message, (response) => {
            const err = chrome.runtime.lastError;
            if (err) {
                console.warn("Native Messaging error:", err.message);
                if (callback) callback({ success: false, message: err.message });
                return;
            }
            if (callback) callback(response);
        });
    } catch (ex) {
        console.error("Exception in sendNativeMessage:", ex);
        if (callback) callback({ success: false, message: ex.toString() });
    }
}

