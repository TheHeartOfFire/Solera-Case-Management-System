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
- [ ] Allow custom links to get saved to disk
- [ ] Enable ability to create custom links
- [ ] Complete UI overhaul and improvement

#### 6. Settings
- [ ] Improve layout
- [ ] Add additional settings:
  - [ ] User's Name, phone, title, and email address
  - [ ] General Solera support info (support email & phone #)
  - [ ] AM Forms mailing address
  - [ ] Dashboard control visibility settings

#### 7. General / Misc
- [ ] Add User Roles that affect which features are available in the app
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
- [ ] Establish Named Pipe server for Project Citadel communication
- [x] Create Variable Registration System (Server ID, Dealership, Case #, etc.)
- [x] Finalize Azure Function Proxy for GitHub API / Bug Reporting
- [x] Configure VeloPack build tasks and GitHub Actions CI/CD pipeline
- [ ] Add ability to pull information from outside sources:
  - [ ] Salesforce Page (Browser extension / Citadel integration)
  - [ ] CST / Server list
- [ ] Inject context menu items into Windows RMB menu for FGA copy/pasting

