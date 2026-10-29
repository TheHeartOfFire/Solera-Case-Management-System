# Solera Case Management System - Master Release Roadmap (Target: New Year)

The active roadmap tracks the journey from Beta (v2.x) to the **Production Release (v3.0)** targeted for the New Year.
Tracked broadly against **GitHub Issue #76: Polish goals**.

---

### Core Philosophy & Guiding Principles
- **Frictionless Adoption**: Ultra-intuitive, zero learning curve UX designed to overcome "change is bad" inertia and make returning to old manual workflows unthinkable.
- **Enterprise Polish**: Rock-solid stability, zero visual artifacts, unified design language, responsive performance, and clean memory management.
- **Modular & Multi-Team Scalability**: Role-driven modular architecture allowing SCMT to expand effortlessly across different company departments and support teams beyond forms.

---

### Sequential Development Phases (Path to Release)

```mermaid
flowchart LR
    P1["Phase 1: FormGen Utilities"] --> P2["Phase 2: Dashboard & Templates (CST)"]
    P2 --> P3["Phase 3: Proposal Toolkit"]
    P3 --> P4["Phase 4: Unified UI Pass"]
    P4 --> P5["Phase 5: Scalable Role System"]
    P5 --> P6["Phase 6: Secondary Dockable UI"]
    P6 --> P7["Phase 7: Themes & JSON Styling"]
    P7 --> REL["Production Release (New Year)"]
    REL --> POST["Post-Release: Deep FormGen Mods & Backlog"]
```

---

#### Phase 1: FormGen Utilities Overhaul `[NEXT UP]`
- [ ] Move editable fields into a dedicated sidebar and open up the center workspace for complex file manipulation (Issue #72)
- [ ] Prompt manipulation:
  - [ ] Prompt copying & duplication between forms
  - [ ] Reordering and deletion shortcuts
- [ ] Field manipulation:
  - [x] Field alignment and distribution
  - [ ] Field copying and duplication
  - [ ] Bulk field operations (bold all, font size all, align all across pages)
- [ ] Code parsing engine for form fields and code lines
- [ ] Unify FormGen Utilities workspace layout and responsive sizing

#### Phase 2: Dashboard & Templates Integration (with CST Integration)
*Note: Dropped drag-and-drop customization in favor of a single rock-solid, cohesive layout.*
- [ ] **Dashboard Layout & Component Extraction**:
  - [ ] Address layout, sizing, and scrolling pain points across various resolutions
  - [ ] Extract Notes, Forms, Dealers, Contacts, and Misc controls into clean, dedicated user controls
- [ ] **Auto/Mate CST & Dealership Integration**:
  - [ ] Launch Auto/Mate support sessions directly for dealerships from CST database
  - [ ] Server ID, dealership name, and company number lookup tool
  - [ ] Direct injection of server/company details into active case notes
- [ ] **Template System Overhaul**:
  - [ ] Use Dashboard template tags to filter templates as default filter
  - [ ] Default template presets tailored for each Org Role
  - [ ] Template grouping: group templates by cause/workflow for rapid 1-click access
  - [ ] New Template Dialog layout and group tag assignment

#### Phase 3: Proposal Form Toolkit
- [ ] Dedicated 1-screen workspace for pulling and pushing proposal forms
- [ ] Proposal coding assistant to simplify and automate proposal-specific programming
- [ ] Proposal & Proposal Logo Management integration

#### Phase 4: Whole-App Unified UI Pass
- [ ] Set up comprehensive Resource Dictionary to enforce unified styling across all pages and dialogs
- [ ] Typography, spacing, button hierarchy, and scrollbar uniformity
- [ ] Eliminate all compiler and XAML warnings
- [ ] Add About Page and diagnostic versioning details
- [ ] **Internationalization & Localization (i18n / l10n)**:
  - [ ] Centralize hardcoded UI strings into Resource dictionaries / `.resx` architecture
  - [ ] Add Language Selection setting in Settings Page (English default)
  - [ ] Spanish translation (`es-ES` / `es-MX`)
  - [ ] Prominent Indian language translations (Hindi `hi-IN`, Telugu `te-IN`, Tamil `ta-IN`)

#### Phase 5: Scalable Role System & Modular Architecture
- [ ] Define extensible Role Schema governing feature visibility, views, and toolbars
- [ ] Modular packaging / on-demand component loading (download/load only what is needed for the user's role)
- [ ] Add Developer Role (debug-only / hidden mode granting full unrestricted visibility across all modules)
- [ ] Extensibility hooks to support non-forms departments and custom workflows across the company

#### Phase 6: Secondary Interface (Screen-Edge Dockable Toolbar)
- [ ] Ultra-compact, minimalist screen-edge toolbar with near-zero screen real estate impact
- [ ] Configurable screen docking (dock to left, right, top, bottom, or floating edge)
- [ ] 1-click access to templates, case notes, dashboard quick actions, and modal tool dialogs

#### Phase 7: Multi-Theme Engine & Custom JSON Themes
- [ ] Built-in themes:
  - [ ] Light Mode
  - [ ] Dark Mode
  - [ ] Solera Theme (corporate styling)
  - [ ] Auto/Mate Theme (classic DMS palette)
- [ ] Customizable theme engine: read and write theme profiles to/from JSON for shareable community theme files

---

### Foundation & Completed Milestones
- [x] **Code Snippets & Formula Builder**:
  - [x] Scratch-like code block drag-and-drop system
  - [x] Database of FormGen fields with types and expected lengths
  - [x] Code Path Tree showing outcomes and variable states at each fork
  - [x] Live test data input box and formula testing
  - [x] Plain-text code pasting with automatic block generation
  - [x] Troubleshooter for common syntax and logical errors
- [x] **Quick Links**:
  - [x] Custom link creation and disk persistence
  - [x] Complete UI overhaul, scrolling support, and dialog stability
- [x] **Architecture & Tooling**:
  - [x] MVVM / DI container architecture
  - [x] Variable Registration System (Server ID, Dealership, Case #, etc.)
  - [x] Azure Function Proxy for GitHub API / bug reporting
  - [x] VeloPack build tasks and GitHub Actions CI/CD pipeline
  - [x] Salesforce extension browser integration
  - [x] Windows context menu integration for `.formgen` file associations
  - [x] FormGen PDF file handle release on closed tabs (PR #105)
  - [x] Code editor undo/redo support via `StyledTextUndoManager`

---

### Post-Release Backlog & FormGen Mod Roadmap

#### Multi-App Capabilities `[Both SCMT & FormGen Mod]`
- [ ] **Bulk Prompt Operations**:
  - [ ] Prompt cloning (duplicate existing prompt with types, labels, and options)
  - [ ] Bulk prompt reordering (multi-select prompts and move up/down together)
  - [ ] Bulk prompt deletion (multi-select prompts to remove, `Delete` key support)
  - [ ] Bulk editing of common attributes across prompts
- [ ] **Prompt & Code Transfer**:
  - [ ] Copy Init / Prompts / Post screen programming between forms
- [ ] **Bulk Field Editing**:
  - [ ] Bulk operations across page fields (bold all, font size all, align all)
- [ ] **Vehicle Merchandising (VM) Safety Detection**:
  - [ ] Detect if form is VM-safe (restricted to subset of F fields) and display status indicator
- [ ] **Automatic PDF Sanitization**:
  - [ ] Strip interactive form fields and embedded JavaScript prior to loading in FormGen

#### FormGen Mod Only Candidates `[FormGen Mod]`
- [ ] **FormGen UI & Shell Experience**:
  - [ ] Background / Console Window Suppression (launch cleanly via `javaw` or launcher mod)
  - [ ] Form Comparison Tool (side-by-side comparison of 2 forms for visual alignment and migration)
  - [ ] Theme Customization (FormGen dark theme via config)
- [ ] **Enhanced Undo / Redo Lifecycle**:
  - [ ] Deep undo/redo: field contents, prompt addition/subtraction, prompt contents, and Init/Prompts/Post tabs
  - [ ] "Undo All / Reset to Checkout" revert action
  - [ ] "Preview Checkout" semi-transparent overlay to visually confirm changes against original checkout
- [ ] **PDF & Publishing Tools**:
  - [ ] "Open in Adobe" button from FormGen toolbar/menu
  - [ ] PDF upload size check & compression warning before publishing
  - [ ] Advanced Publishing: "Publish to All" button to deploy updates to all dealers with form on menu
- [ ] **Search & Repository Management**:
  - [ ] Form search crash prevention on large query results (500+ items) via paging/batching
  - [ ] Search by 'Active' forms filter
  - [ ] Show dealer-renamed form titles in search results
  - [ ] "Customized" badge to distinguish shared baseline forms from dealer-specific customized forms
  - [ ] Real-time Elasticsearch integration for instant form lookup
- [ ] **Forms Libraries & Custom Fields**:
  - [ ] Global Forms Libraries: organize forms into OEM, Lender, State, Generic, and Licensed libraries
  - [ ] Custom Runtime Calculation Fields: user-defined composite fields in FormGen

#### SCMT Cross-Tool Integrations
- [ ] **Advanced Line Adjustment Simulation**:
  - [ ] Import dealer scanned impact print photocopy as semi-transparent overlay
  - [ ] Automated line adjustment simulation comparing impact file to scanned image
- [ ] **Dealership Profiles & CST Integration**:
  - [ ] Auto-populate Server ID, Company #, and Name across tools
  - [ ] Shared CST name/server database integration & AMPS direct launcher
  - [ ] Automated form changelog notes generated from dealer profile / modifications

#### Feasibility Investigation (Low Confidence / Architectural Review)
- [ ] **Multi-Revision Form History**:
  - [ ] Unified form definition tracking multiple base image and code revisions under one entry (suspected DB update requirement)

#### Blocked / Not Doable in Mods (Requires Auto/Mate Core DMS Updates)
- [ ] ~~**Dynamic Prompts**~~: *Requires updating Auto/Mate proper runtime to receive and manage conditional prompt visibility/labels*
- [ ] ~~**Required Prompts Expansion**~~: *Requires updating Auto/Mate proper runtime to evaluate complex requirement logic (OR relationships, dropdown default checks, date requirements)*
- [ ] ~~**Form Counters**~~: *Requires Auto/Mate proper print engine updates to persist and increment per-print invoice/serial counters*
- [ ] ~~**Barcode & QR Code Dynamic Generation**~~: *Requires Auto/Mate core print engine update to render programmatic barcodes/QRs at print time*
- [ ] ~~**Label Printer Support**~~: *Requires Auto/Mate core print engine update to support small Zebra/Dymo roll formats beyond 8.5x11 sheets*
- [ ] ~~**Extended DMS Field Formatting**~~ (Italics styling, suppressing `$` on money fields, left/center alignment for numeric fields): *Requires Auto/Mate print rendering engine changes*
