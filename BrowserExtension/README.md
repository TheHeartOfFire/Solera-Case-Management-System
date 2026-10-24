# Solera CMS & Project Citadel Browser Extension

This extension integrates Google Chrome and Microsoft Edge with the **Solera Case Management System (CMS)** desktop application and **Project Citadel**.

---

### Features

1. **Text Selection Right-Click ("Send to Solera Dashboard"):**
   - **Parse as New Case:** Automatically parses selected text into a new case tab in the Solera Dashboard.
   - **Set Case #:** Sets the Case Number of the active Dashboard note.
   - **Set Server ID:** Sets the Server ID of the active note.
   - **Set Dealership:** Sets the active dealership.
   - **Set Contact Name:** Sets the active contact name.
   - **Citadel: Quick Capture Selection:** Forwards the selected snippet directly to Project Citadel's brain dump queue.

2. **Editable Inputs Right-Click ("Paste from Solera Dashboard"):**
   - **Server ID:** Pastes the active server code into the current input box.
   - **Dealership:** Pastes the active dealership name.
   - **Case #:** Pastes the case number.
   - **Contact Name:** Pastes the contact name.
   - **Active Template Text:** Pastes the full rendered text of the currently selected template.

3. **Page Action:**
   - **Solera CMS: Parse Full Case Notes:** Scrapes the visible Salesforce Case page (Case Number, Subject, Description, Contact, Account/Dealership) and funnels it into the Solera Dashboard as a parsed case.

---

### Installation & Setup

1. **Register the Native Messaging Host:**
   - Run `BrowserExtension/register_host.ps1` in PowerShell.
   - This registers `com.solera.cms.bridge` in `HKCU\Software\Google\Chrome\NativeMessagingHosts` and `HKCU\Software\Microsoft\Edge\NativeMessagingHosts`.

2. **Load the Extension into Chrome or Edge:**
   - Open Chrome or Edge and navigate to `chrome://extensions/` or `edge://extensions/`.
   - Enable **Developer mode** (toggle in the top-right corner).
   - Click **Load unpacked** and select the `BrowserExtension/` directory.

3. **Verify:**
   - Start `AMFormsCST.Desktop.exe`.
   - Right-click any text in Salesforce or any webpage to see the Solera Dashboard integration options.

