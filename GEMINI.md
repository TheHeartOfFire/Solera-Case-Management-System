# Solera Case Management System - Project Goals & Polish Roadmap

The active roadmap for completing this application is tracked in **GitHub Issue #76: Polish goals**.
Keep these goals in mind across all development tasks until marked complete.

---

### Progress & Checklist

#### 1. Dashboard
- [ ] Address various layout, sizing, and scrolling concerns
- [ ] Extract Notes, Forms, Dealers, and Contacts into their own user controls
- [ ] Extract misc controls to their own user control
- [ ] Make the new user controls drag-and-droppable to allow dashboard customization
- [ ] Add template tags

#### 2. Formgen Utilities
- [ ] Ensure all fields that should be edited in a formgen file can be (ref: Issue #72)
- [ ] Move editable fields to a sidebar and open up the center of the page for more complex file manipulation:
  - [ ] Prompt Copying
  - [ ] Field Copying
  - [x] Field Alignment
  - [ ] Code parsing for Fields and Code Lines
  - [ ] Bulk operations (bold all, align all, font size all, etc.)
- [ ] Unify layout

#### 3. Templates
- [ ] Use Dashboard template tags to filter templates as the new default filter
- [ ] Create a set of default templates for each Org Role
- [ ] Allow creation of template groups so templates for the same 'cause' can be grouped together for ease of access:
  - [ ] New Template Dialog layout and formatting
  - [ ] Ability to specify group tags selectable from the dashboard for filtering

#### 4. Code Snippets
- [x] Transition to scratch-like code block drag-and-drop system
- [x] Add database of formgen fields to include type and expected length info
- [x] Produce a Code Path tree showing all possible outcomes for a formula including value of each relevant variable at each fork
- [x] Using field database, automatically detect when a field is included in a formula and produce test data input box to show how data is used/changed
- [x] Allow pasting plain text code into the page so code blocks and other resources generate around it
- [x] Create a troubleshooter to detect common logical and syntactical issues

#### 5. Quick Links
- [x] Allow custom links to get saved to disk
- [x] Enable ability to create custom links
- [x] Complete UI overhaul and improvement

#### 6. Settings
- [x] Improve layout
- [ ] Add additional settings:
  - [x] User's Name, phone, title, and email address
  - [x] General Solera support info (support email & phone #)
  - [x] AM Forms mailing address
  - [ ] Dashboard control visibility settings

#### 7. General / Misc
- [x] Add User Roles that affect which features are available in the app
- [ ] Create a secondary interface in the form of screen-edge toolbars:
  - [ ] Include access to templates
  - [ ] Include access to dashboard
  - [ ] Include buttons for various tools that have dialog windows
- [ ] Add more themes (True Solera, Auto/Mate)
- [ ] Eliminate warnings
- [ ] Add an About page
- [ ] Set up a resource dictionary to enforce a unified UI

#### 8. Architecture & External
- [x] Finalize MVVM/DI Container stability
- [ ] Establish Named Pipe server for external app communication
- [x] Create Variable Registration System (Server ID, Dealership, Case #, etc.)
- [x] Finalize Azure Function Proxy for GitHub API / Bug Reporting
- [x] Configure VeloPack build tasks and GitHub Actions CI/CD pipeline
- [ ] Add ability to pull information from outside sources:
  - [x] Salesforce Page (Browser extension integration)
  - [ ] CST / Server list
- [x] Inject context menu items into Windows RMB menu for FGA copy/pasting

#### 9. Future Explorations & Backlog
- [ ] PDF Form Reader & Field Suggestion Tool: Read PDF forms, analyze visual fields/pages, and suggest needed FormGen fields
- [ ] Modern Reactive UI Mod for FormGen
- [ ] Smart Fields & Specialty Prompts in Code Blocks:
  - [ ] Support FormGen smart fields (specialty prompts and exposed prompt data)
  - [ ] Multi-drop tool for bulk smart prompts (e.g. Accessory prompts 1-7, 1-15)
- [ ] Dealership & Server Info Lookup Tool:
  - [ ] Cross-reference Name DB (Name, server ID, company #s) shared with CST and dev board
  - [ ] Multi-company selection checkboxes on Dashboard to parse cases across multiple companies
- [ ] Automatic Line Adjustment Simulation:
  - [ ] Compare impact FormGen file with scanned print photocopy
  - [ ] Scale, align with original .jpg, and simulate field movements for automated line adjustments
- [ ] Form Search via Elasticsearch
- [ ] Form Publishing & Multi-dealer Republishing:
  - [ ] Republish forms to all dealers/companies that currently have the form
  - [ ] Option to fork multi-dealer published forms so each dealer gets their own copy (UUID)
  - [ ] Form history revision revert with copy deployment to specific dealers
- [ ] Proposal & Proposal Logo Management Dashboard
- [ ] Rental Forms Wizard
- [ ] Custom Field Groups (e.g. bulk dropping `ACCESSORY01_POSPRICE` through `ACCESSORY15_POSPRICE`)
- [ ] Form Testing with Customer Data: Insert server/company/deal# to test live calculations
- [ ] Code Blocks Testing with Customer Data

#### 10. FormGen Mod Roadmap & Pre-NextGen Wishlist

##### A. Multi-App Capabilities (SCMT & FormGen Mod)
- [ ] **Bulk Prompt Operations** `[Both SCMT & FormGen Mod]`:
  - [ ] Prompt cloning (duplicate an existing prompt with types, labels, and options)
  - [ ] Bulk prompt reordering (multi-select prompts and move up/down together)
  - [ ] Bulk prompt deletion (multi-select prompts to remove)
  - [ ] Delete prompts via `Delete` key on click/highlight (currently only `Alt+D`)
  - [ ] Bulk editing of common attributes across prompts
- [ ] **Prompt & Code Transfer** `[Both SCMT & FormGen Mod]`:
  - [ ] Copy Init / Prompts / Post screen programming between forms (via copy-paste or cross-form import dialog)
- [ ] **Bulk Field Editing** `[Both SCMT & FormGen Mod]`:
  - [ ] Bulk operations across page fields (bold all, font size all, align all)
- [ ] **Vehicle Merchandising (VM) Safety Detection** `[Both SCMT & FormGen Mod]`:
  - [ ] Detect if form is VM-safe (restricted to subset of F fields) and display status indicator
- [ ] **Automatic PDF Sanitization** `[Both SCMT & FormGen Mod]`:
  - [ ] Strip interactive form fields and embedded JavaScript prior to loading in FormGen

##### B. FormGen Mod Only Candidates
- [ ] **FormGen UI & Shell Experience** `[FormGen Mod]`:
  - [ ] Background / Console Window Suppression (launch cleanly without lingering CLI console window via `javaw` or launcher mod)
  - [ ] Form Comparison Tool (side-by-side comparison of 2 forms for visual alignment and field/prompt/codeline migration)
  - [ ] Theme Customization (optional dark theme for FormGen via configuration/JSON; cross-ref Section 7)
- [ ] **Enhanced Undo / Redo Lifecycle** `[FormGen Mod]`:
  - [x] Code editor undo/redo support via `StyledTextUndoManager` *(Implemented in FormGenAgent)*
  - [ ] Deep undo/redo: field contents, addition/subtraction of prompts, prompt contents, and Init/Prompts/Post tabs
  - [ ] "Undo All / Reset to Checkout" revert action
  - [ ] "Preview Checkout" semi-transparent overlay to visually confirm changes against original checkout
- [ ] **PDF & Publishing Tools** `[FormGen Mod]`:
  - [ ] "Open in Adobe" button from FormGen toolbar/menu
  - [ ] PDF upload size check & compression warning before publishing
  - [ ] Advanced Publishing: "Publish to All" button to deploy updates to all dealers with the form on menu, with option to fork multi-dealer published forms with unique UUIDs
- [ ] **Search & Repository Management** `[FormGen Mod]`:
  - [ ] Form search crash prevention on large query results (500+ items) via paging/batching
  - [ ] Search by 'Active' forms filter
  - [ ] Show dealer-renamed form titles in search results
  - [ ] "Customized" badge to distinguish shared baseline forms from dealer-specific customized forms
  - [ ] Real-time Elasticsearch integration for instant form lookup
- [ ] **Forms Libraries & Custom Fields** `[FormGen Mod]`:
  - [ ] Global Forms Libraries: organize forms into OEM, Lender, State, Generic, and Licensed libraries with dealer-level access control
  - [ ] Custom Runtime Calculation Fields: user-defined composite fields in FormGen

##### C. SCMT Primary / Cross-Tool Integrations
- [ ] **Advanced Line Adjustment Simulation** `[SCMT Primary, FormGen Mod Secondary]`:
  - [ ] Import dealer scanned impact print photocopy as semi-transparent overlay
  - [ ] Automated line adjustment simulation comparing impact file to scanned image
- [ ] **Dealership Profiles & CST Integration** `[SCMT Foundation -> FormGen Mod]`:
  - [ ] SCMT profile selector for dealer group / company to auto-populate Server ID, Company #, and Name across tools
  - [ ] Shared CST name/server database integration & AMPS direct launcher
  - [ ] Automated form changelog notes generated from dealer profile / modifications

##### D. Completed & Already Integrated Features
- [x] **Release PDF file locks on closed tabs**: *Fixed in [PR #105](https://github.com/TheHeartOfFire/Solera-Case-Management-System/pull/105)*
- [x] **File Extension Associations**: *Completed via SCMT context menu integration & single-instance launcher*
- [x] **Scratch-like Formula Builder**: *Implemented in SCMT Code Blocks*
- [x] **Code Path Tree & Live Testing**: *Implemented in SCMT Code Blocks*
- [x] **Field Alignment & Distribution**: *Implemented in SCMT FormGen Utilities*
- [x] **Code Editor Undo/Redo**: *Implemented in FormGenAgent via `StyledTextUndoManager`*

##### E. Feasibility Investigation (Low Confidence / Architectural Review)
- [ ] **Multi-Revision Form History** `[Investigate Feasibility]`:
  - [ ] Unified form definition tracking multiple base image and code revisions under one entry (suspected to require central database updates which may not be feasible prior to next-gen)

##### F. Blocked / Not Doable in Mods (Requires Auto/Mate Proper DMS Updates)
- [ ] ~~**Dynamic Prompts**~~: *Requires updating Auto/Mate proper runtime to receive and manage conditional prompt visibility/labels*
- [ ] ~~**Required Prompts Expansion**~~: *Requires updating Auto/Mate proper runtime to evaluate complex requirement logic (OR relationships, dropdown default checks, date requirements)*
- [ ] ~~**Form Counters**~~: *Requires Auto/Mate proper print engine updates to persist and increment per-print invoice/serial counters*
- [ ] ~~**Barcode & QR Code Dynamic Generation**~~: *Requires Auto/Mate core print engine update to render programmatic barcodes/QRs at print time*
- [ ] ~~**Label Printer Support**~~: *Requires Auto/Mate core print engine update to support small Zebra/Dymo roll formats beyond 8.5x11 sheets*
- [ ] ~~**Extended DMS Field Formatting**~~ (Italics styling, suppressing `$` on money fields, left/center alignment for numeric fields): *Requires Auto/Mate print rendering engine changes*


