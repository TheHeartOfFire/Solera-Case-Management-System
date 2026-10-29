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
##### A. Prompt Management & Programming
- [ ] Bulk Prompt Operations:
  - [ ] Prompt cloning (duplicate an existing prompt with types, labels, and options)
  - [ ] Bulk prompt reordering (multi-select prompts and move up/down together)
  - [ ] Bulk prompt deletion (multi-select prompts to remove)
  - [ ] Delete prompts via `Delete` key on click/highlight (currently only `Alt+D`)
  - [ ] Bulk editing of common attributes across prompts
- [ ] Prompt & Code Transfer:
  - [ ] Copy Init / Prompts / Post screen programming between forms (via copy-paste or cross-form import dialog)
- [ ] Dynamic Prompts:
  - [ ] Enable/disable prompts dynamically based on selected state of other prompts
  - [ ] Change prompt label dynamically based on state of other prompts
- [ ] Required Prompts Expansion:
  - [ ] Resolve validation for Date and Dropdown prompts (e.g. dropdown not equal to default value)
  - [ ] Expanded requirement operators (Equal / Not Equal, Blank / Not Blank)
  - [ ] Group requirement logic (OR relationships: Prompt A OR B OR C meets requirement)
- [ ] Form Counters:
  - [ ] Form print counter tracking (auto-incrementing per print for invoices, serials, and tracking)

##### B. Field & Document Operations
- [ ] Bulk Field Editing:
  - [ ] Bulk operations across page fields (bold all, italic all, font size all, align all)
- [x] Field Alignment & Distribution (Implemented in SCMT FormGen Utilities)
- [ ] Extended Field Formatting:
  - [ ] Italics font styling support
  - [ ] Money fields option to omit forced `$` prefix
  - [ ] Configurable numbers-as-words (include/exclude cents, "cents" suffix, "dollars" prefix, "and" conjunction, custom separators)
  - [ ] Left and Center alignment options for numeric fields
- [ ] Vehicle Merchandising (VM) Safety Detection:
  - [ ] Detect if form is VM-safe (restricted to subset of F fields) and display status indicator
- [ ] Barcode & QR Code Dynamic Generation:
  - [ ] Programmatic barcode / QR code generation at print time (customer profile, vehicle profile, AMPS links)
- [ ] Label Printer Support:
  - [ ] Small Zebra & Dymo label roll format support (beyond standard 8.5x11 sheet labels)

##### C. FormGen UI & Workflow Experience
- [ ] Background / Console Window Suppression:
  - [ ] Launch FormGen cleanly without lingering CLI console window (via `javaw` or SCMT launcher mod)
- [ ] File Extension Associations:
  - [ ] Associate `.formgen` files with FormGen singleton (open in active instance or launch fresh)
- [ ] Enhanced Undo / Redo Lifecycle:
  - [x] Code editor undo/redo support via `StyledTextUndoManager` (Implemented in FormGenAgent)
  - [ ] Deep undo/redo: field contents, addition/subtraction of prompts, prompt contents, and Init/Prompts/Post tabs
  - [ ] "Undo All / Reset to Checkout" revert action
  - [ ] "Preview Checkout" semi-transparent overlay to visually confirm changes against original checkout
- [ ] Theme Customization:
  - [ ] Dark theme support for FormGen via configuration/JSON
- [ ] Form Comparison Tool:
  - [ ] Side-by-side comparison of 2 forms for visual alignment and field/prompt/codeline migration

##### D. PDF Tools & Document Sanitization
- [x] Release PDF file locks on closed tabs (Fixed in PR #105)
- [ ] "Open in Adobe" button from FormGen toolbar/menu
- [ ] Automatic PDF sanitization (strip interactive form fields and embedded JavaScript prior to load)
- [ ] PDF upload size check & compression warning before publishing

##### E. Search & Repository Management
- [ ] Form Search Improvements & Crash Prevention:
  - [ ] Prevent UI freeze / crash on large query results (500+ items) via paging/batching
  - [ ] Search by 'Active' forms filter
  - [ ] Show dealer renamed form titles in search results
  - [ ] "Customized" badge to distinguish shared baseline forms from dealer-specific customized forms
  - [ ] Real-time Elasticsearch integration for instant form lookup
- [ ] Advanced Publishing:
  - [ ] "Publish to All" button to deploy updates to all dealers with the form on menu
  - [ ] Fork multi-dealer published forms with unique UUIDs
- [ ] Multi-Revision Form History:
  - [ ] Unified form definition tracking multiple base image and code revisions under one entry
- [ ] Global Forms Libraries:
  - [ ] Organize forms into OEM, Lender, State, Generic, and Licensed libraries with dealer-level access control

##### F. Dealership, CST & Formula Tooling
- [x] Scratch-like Formula Builder (Implemented in SCMT Code Blocks)
- [x] Code Path Tree & Live Testing (Implemented in SCMT Code Blocks)
- [ ] Custom Runtime Calculation Fields (User-defined composite fields)
- [ ] Dealership Profiles & CST Integration:
  - [ ] Select dealer group / company profile to auto-populate Server ID, Company #, and Name across tools
  - [ ] Shared CST name/server database integration & AMPS direct launcher
  - [ ] Automated form changelog notes generated from dealer profile / modifications
- [ ] Advanced Line Adjustment Simulation:
  - [ ] Import dealer scanned impact print photocopy as semi-transparent overlay
  - [ ] Automated line adjustment simulation comparing impact file to scanned image


