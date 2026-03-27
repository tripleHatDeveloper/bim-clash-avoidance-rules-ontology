# ClashAvoidanceTutorial — Revit Plugin
## Installation and User Guide

**Version:** 1.0
**Compatible with:** Autodesk Revit 2024
**Developed by:** Northumbria University, School of Architecture and Built Environment
**Contact:** omar.doukari@northumbria.ac.uk

---

## Table of Contents

1. [Overview](#1-overview)
2. [System Requirements](#2-system-requirements)
3. [Package Contents](#3-package-contents)
4. [Step 1 — Install Visual Studio](#4-step-1--install-visual-studio)
5. [Step 2 — Open the Project](#5-step-2--open-the-project)
6. [Step 3 — Verify Revit API References](#6-step-3--verify-revit-api-references)
7. [Step 4 — Add the OWL Ontology File](#7-step-4--add-the-owl-ontology-file)
8. [Step 5 — Build the DLL](#8-step-5--build-the-dll)
9. [Step 6 — Deploy the Plugin](#9-step-6--deploy-the-plugin)
10. [Step 7 — Set Up Shared Parameters in Revit](#10-step-7--set-up-shared-parameters-in-revit)
11. [Step 8 — Using the Plugin](#11-step-8--using-the-plugin)
12. [Rules Reference](#12-rules-reference)
13. [Override Log](#13-override-log)
14. [Troubleshooting](#14-troubleshooting)

---

## 1. Overview

The ClashAvoidanceTutorial plugin brings **real-time BIM coordination rule
checking** directly into Autodesk Revit 2024. Rather than detecting clashes
after models are federated, the plugin evaluates coordination constraints as
you model — alerting you to potential issues at the moment they are introduced,
before they can propagate into later project stages.

**Key capabilities:**

- Loads coordination rules from an OWL ontology file at startup
- Intercepts Revit element creation and modification events automatically
- Evaluates applicable rules in under 100 milliseconds without disrupting
  the modelling workflow
- Shows targeted violation dialogs specific to each rule type
- Allows professional override of any rule with a mandatory justification
- Logs all overrides to a CSV file in the project folder for audit traceability
- Provides access to a validated rule library of 35 expert-derived coordination
  rules via an interactive HTML interface

---

## 2. System Requirements

| Requirement | Specification |
|-------------|---------------|
| Operating System | Windows 10 or Windows 11 (64-bit) |
| Revit Version | Autodesk Revit 2024 |
| .NET Framework | 4.8 (included with Windows 10 and 11) |
| Visual Studio | 2019 or 2022 (Community edition is sufficient) |
| Disk Space | ~50 MB for Visual Studio build output |
| RAM | No additional requirements beyond Revit's own |

> **Note:** Revit 2024 must be installed before building. The build
> process requires RevitAPI.dll and RevitAPIUI.dll from the Revit
> installation folder.

---

## 3. Package Contents

After extracting the ZIP you will find the following structure:

```
ClashAvoidancePlugin/
│
├── INSTALLATION_AND_USER_GUIDE.md         <- This file
│
├── ClashAvoidancePlugin.csproj            <- Visual Studio project file
├── ClashAvoidance.addin                   <- Revit manifest (update path, then deploy)
│
├── App.cs                                 <- Plugin entry point and ribbon setup
│
├── Commands/
│   └── Commands.cs                        <- PluginLogic ribbon button command
│
├── Engine/
│   ├── OntologyLoader.cs                  <- Parses the OWL file at runtime
│   ├── RuleEngine.cs                      <- Evaluates rules against Revit elements
│   └── EventHandler.cs                   <- Subscribes to Revit authoring events
│
├── UI/
│   ├── AssistantDialog.xaml(.cs)          <- Entry window (PluginLogic button)
│   ├── LinkedModelPinnedDialog.xaml(.cs)  <- Linked model pin violation dialog
│   ├── WallHeightDialog.xaml(.cs)         <- Wall height violation dialog
│   └── ViolationPromptDialog.xaml(.cs)   <- Generic violation fallback dialog
│
├── Logging/
│   └── OverrideLogger.cs                  <- Writes overrides to a CSV file
│
└── Resources/
    ├── clash_avoidance_ontology.owl       <- OWL ontology (ADD THIS — see Step 4)
    ├── tutorial_rules_library.html        <- Rule library HTML interface (included)
    ├── SharedParameters.txt               <- Revit shared parameter definitions
    └── BUILD_NOTE.txt                     <- Note about DLL compilation
```

> **Important:** The compiled DLL (`ClashAvoidancePlugin.dll`) is **not
> included** because it must be built against the Revit API DLLs installed
> on your machine. Follow Steps 1 to 5 below to compile it. This takes
> approximately 10 minutes the first time.

---

## 4. Step 1 — Install Visual Studio

If you do not already have Visual Studio installed:

1. Go to: **https://visualstudio.microsoft.com/downloads/**
2. Download **Visual Studio Community 2022** (free)
3. Run the installer
4. On the **Workloads** screen, select:
   - .NET desktop development
5. Click **Install**

This installs the C# compiler and .NET Framework 4.8 support needed to
build the plugin. Installation typically takes 10 to 20 minutes.

---

## 5. Step 2 — Open the Project

1. Open **Visual Studio**
2. Go to **File → Open → Project/Solution**
3. Navigate to the extracted `ClashAvoidancePlugin` folder
4. Select **ClashAvoidancePlugin.csproj** and click **Open**

Visual Studio will load the project. All source files will be listed in
the **Solution Explorer** panel on the right side.

---

## 6. Step 3 — Verify Revit API References

The project references two DLL files from your Revit 2024 installation.
You need to confirm these paths are correct on your machine.

1. In **Solution Explorer**, expand **References**
2. Look for **RevitAPI** and **RevitAPIUI**
3. If either shows a yellow warning triangle:
   - Right-click the reference and select **Properties**
   - Update the **Path** to point to your Revit installation:

```
C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll
C:\Program Files\Autodesk\Revit 2024\RevitAPIUI.dll
```

4. For each reference, confirm in the **Properties** panel:
   - **Copy Local = False**

If Revit is installed in a different location, adjust the paths
accordingly. The DLL files are always in the root of the Revit
installation folder.

---

## 7. Step 4 — Add the OWL Ontology File

The plugin loads coordination rules from an OWL ontology file at startup.
This file must be present in the Resources folder before building.

1. Obtain the OWL file from:
   **https://github.com/tripleHatDeveloper/bim-clash-avoidance-rules-ontology**

2. Download the file and rename it to exactly:
   ```
   clash_avoidance_ontology.owl
   ```

3. Place it in the Resources subfolder of the project:
   ```
   ClashAvoidancePlugin\Resources\clash_avoidance_ontology.owl
   ```

4. In Visual Studio Solution Explorer:
   - Right-click the **Resources** folder
   - Select **Add → Existing Item**
   - Select `clash_avoidance_ontology.owl` and click **Add**

5. Click the file in Solution Explorer, then in the **Properties** panel set:
   - **Build Action = Content**
   - **Copy to Output Directory = Always**

---

## 8. Step 5 — Build the DLL

1. In Visual Studio go to **Build → Build Solution** (or press Ctrl+Shift+B)
2. Watch the **Output** panel at the bottom for messages
3. A successful build ends with:
   ```
   ========== Build: 1 succeeded, 0 failed ==========
   ```
4. The compiled DLL is now at:
   ```
   ClashAvoidancePlugin\bin\Debug\ClashAvoidancePlugin.dll
   ```

### If the build fails

| Error message | Fix |
|---------------|-----|
| RevitAPI.dll not found | Update reference paths (Step 3) |
| clash_avoidance_ontology.owl not found | Add the OWL file (Step 4) |
| The type or namespace 'Autodesk' could not be found | Re-add Revit API references (Step 3) |
| Target framework not supported | In project properties confirm target is .NET Framework 4.8 |

---

## 9. Step 6 — Deploy the Plugin

Deploying the plugin has two parts: copying the compiled files to a
permanent folder, and registering the plugin with Revit via the manifest.

### Part A — Create the deployment folder

1. Create a permanent folder for the plugin, for example:
   ```
   C:\RevitPlugins\ClashAvoidance\
   ```

2. Copy the following into that folder:
   - `bin\Debug\ClashAvoidancePlugin.dll`
   - The entire `bin\Debug\Resources\` subfolder

   The deployment folder should look like this:
   ```
   C:\RevitPlugins\ClashAvoidance\
   ├── ClashAvoidancePlugin.dll
   └── Resources\
       ├── clash_avoidance_ontology.owl
       ├── tutorial_rules_library.html
       └── SharedParameters.txt
   ```

### Part B — Update and deploy the .addin manifest

1. Open `ClashAvoidance.addin` from the project folder in **Notepad**

2. Find this line and update it to match your deployment folder path:
   ```xml
   <Assembly>C:\RevitPlugins\ClashAvoidance\ClashAvoidancePlugin.dll</Assembly>
   ```

3. Copy the updated `ClashAvoidance.addin` file to the Revit add-ins folder:
   - Press **Windows key + R**, type `%AppData%` and press Enter
   - Navigate to: `Autodesk\Revit\Addins\2024\`
   - Paste `ClashAvoidance.addin` into this folder

   The full path is typically:
   ```
   C:\Users\YourUsername\AppData\Roaming\Autodesk\Revit\Addins\2024\
   ```

4. **Restart Revit 2024** if it was already open

---

## 10. Step 7 — Set Up Shared Parameters in Revit

The wall height rule reads dimensional thresholds from Revit shared
parameters. This only needs to be done once per project.

### Register the shared parameter file

1. Open Revit 2024 and open your project
2. Go to the **Manage** tab and click **Shared Parameters**
3. Click **Browse**, navigate to your deployment folder
4. Select `Resources\SharedParameters.txt` and click **Open**
5. Click **OK**

### Add parameters to the project

1. Go to **Manage** tab → **Project Parameters** → **Add**
2. Select **Shared parameter** and click **Select**
3. In the group **Clash Avoidance**, select `CA_CeilingHeightThreshold`
4. Under **Categories**, check **Walls**
5. Set as an **Instance** parameter and click **OK**
6. Repeat for `CA_MEPClearance` — bind to **Walls**
7. Repeat for `CA_DimensionalThreshold` — bind to any category
   where a dimensional limit applies

### Enter values on walls

1. Select a wall in your model
2. Open the **Properties** panel
3. Find `CA_CeilingHeightThreshold` — enter the ceiling height in mm
   (for example: `3048` for 10 ft)
4. Find `CA_MEPClearance` — enter the MEP clearance above ceiling in mm
   (for example: `300` for 300 mm)

If shared parameters are not set, the plugin uses these built-in defaults:
- Ceiling height threshold: 2700 mm
- MEP clearance: 300 mm
- Maximum wall height enforced: 2400 mm

Adjust values to match your project's BIM Execution Plan.

---

## 11. Step 8 — Using the Plugin

### Opening the plugin

1. Open Revit 2024
2. Click the **ClashAvoidanceTutorial** tab in the ribbon
3. Click the **PluginLogic** button

The Clash Avoidance Assistant window opens.

---

### The Clash Avoidance Assistant window

```
+---------------------------------------------------------+
|  ClashAvoidanceTutorial - Clash Avoidance Assistant     |
+---------------------------------------------------------+
|  Clash Avoidance Assistant                              |
|  Authoring-time coordination rule checker               |
+---------------------------------------------------------+
|  Please select the appropriate course of action.        |
|  By clicking the options below you will be directed     |
|  to the corresponding task.                             |
|  -------------------------------------------------------+
|  -> View Clash Avoidance Tutorial                       |
|     Open the validated rule library with 35 expert-     |
|     derived coordination rules                          |
|                                                         |
|  -> Launch Clash Avoidance Check                        |
|     Activate real-time rule checking running in the     |
|     background during modelling                         |
+---------------------------------------------------------+
```

---

### Option 1 — View Clash Avoidance Tutorial

Click **-> View Clash Avoidance Tutorial** to open the coordination rule
library.

- Opens `tutorial_rules_library.html` in your default web browser
- Works fully offline — no internet connection required
- Displays 35 expert-validated coordination rules
- Filter by discipline: Architectural, Structural, MEP
- Filter by automation classification: OWL/API Enforceable, API Warning,
  Process-Based
- Search by keyword across rule descriptions and rationale
- The assistant window stays open — you can launch the check at the same time

---

### Option 2 — Launch Clash Avoidance Check

Click **-> Launch Clash Avoidance Check** to activate background rule
checking.

- A confirmation message lists the active rules
- Close the assistant window — the check keeps running in the background
- Continue modelling as normal — violations appear automatically as you work
- Reopen the assistant at any time via the PluginLogic ribbon button
- When active the button label changes to **-> Stop Clash Avoidance Check**

---

### Violation Dialog — Linked Model Not Pinned

Shown when a linked model is detected as unpinned on save, sync, or open.

```
+---------------------------------------------------------+
|  ClashAvoidanceTutorial - Linked Model Pinned Status    |
+---------------------------------------------------------+
|  X  Error!                                              |
|     Please pin the model 'StructuralModel.rvt'.         |
|                                                         |
|  Unpinned linked models may shift position during       |
|  coordination, creating false clashes or obscuring      |
|  real design conflicts.                                 |
+---------------------------------------------------------+
|           [ Override Rule ]  [ OK - I will pin it ]     |
+---------------------------------------------------------+
```

**How to fix:** Select the linked model, then in Properties set
**Pinned = checked**. The check will not trigger again until
the model is unpinned.

---

### Violation Dialog — Wall Height Exceeded

Shown when an interior wall exceeds the project height limit.

```
+---------------------------------------------------------+
|  ClashAvoidanceTutorial - Wall Height Status            |
+---------------------------------------------------------+
|  X  Height cannot be more than 10ft in this project.   |
|                                                         |
|     Please change the height of the following walls:    |
|     'Interior Partition - GF_Wall_003'                  |
|                                                         |
|  Walls exceeding the ceiling height penetrate the MEP   |
|  plenum zone and cause hard clashes with HVAC ductwork. |
+---------------------------------------------------------+
|       [ Override Rule ]  [ OK - I will fix the walls ]  |
+---------------------------------------------------------+
```

**How to fix:** Select the wall, go to Properties, reduce
**Unconnected Height** to the project limit, and press Enter.

---

### Overriding a Rule

If your professional judgment indicates a rule does not apply in a specific
context you can override it:

1. Click **Override Rule** — a justification panel expands
2. Type a brief explanation of why the rule does not apply
3. Click **Confirm Override**

The override is recorded in the project log with your username and a
timestamp. A justification is mandatory — the Confirm Override button
will not proceed without one.

---

## 12. Rules Reference

Four rules are active in this version of the plugin:

| # | Rule | Trigger event | Dialog |
|---|------|--------------|--------|
| 1 | Tutorial accessibility | PluginLogic ribbon button | AssistantDialog |
| 2 | Linked models must be pinned | Document saved, synchronised, or opened | LinkedModelPinnedDialog |
| 3 | Interior wall height constraint | Wall created or modified | WallHeightDialog |
| 4 | Dimensional threshold | Any element with CA_DimensionalThreshold set | ViolationPromptDialog |

These rules are loaded from the following OWL individuals in the ontology:

| Rule | OWL Individual ID |
|------|-------------------|
| Linked model pin status | Rule_LinkedModelsPinned |
| Interior wall height | Rule_A02_InteriorWallHeight |
| Dimensional threshold | Rule_DimensionalThreshold |

Additional rules are defined in the ontology and available for future
implementation:

| OWL Individual ID | Description |
|-------------------|-------------|
| Rule_ModelSynchronization | Hourly central model synchronisation |
| Rule_StructuralOpenings | Structural openings must match MEP specifications |
| Rule_MEPInsulation | MEP insulation dimensions must be modelled precisely |

The full library of 35 coordination rules is available via
**-> View Clash Avoidance Tutorial** in the assistant window.

---

## 13. Override Log

Every rule override is automatically saved to a CSV file located in the
same folder as your Revit project file:

```
{ProjectFolder}\{ProjectName}_ClashAvoidance_Overrides.csv
```

For example, if your project is at `C:\Projects\BuildingA\BuildingA.rvt`,
the log will be at:
`C:\Projects\BuildingA\BuildingA_ClashAvoidance_Overrides.csv`

For unsaved projects the log is written to the plugin deployment folder.

### CSV columns

| Column | Description |
|--------|-------------|
| Timestamp | Date and time of the override (YYYY-MM-DD HH:MM:SS) |
| UserName | Revit username of the designer |
| ElementId | Revit element ID integer |
| ElementDescription | Human-readable element name and ID |
| RuleId | OWL individual fragment identifier |
| RuleLabel | Human-readable rule name |
| ViolationDetail | Computed details of the specific violation |
| Justification | Designer's override justification text |

The CSV can be opened in Excel, Notepad, or any spreadsheet application
and is suitable for inclusion in coordination review packages.

---

## 14. Troubleshooting

### Plugin tab does not appear in Revit

1. Confirm `ClashAvoidance.addin` is in:
   `%AppData%\Autodesk\Revit\Addins\2024\`
2. Open the file in Notepad and verify the `<Assembly>` path
3. Confirm `ClashAvoidancePlugin.dll` exists at that path
4. Restart Revit 2024

### "OWL ontology file not found" error on Revit startup

Ensure `clash_avoidance_ontology.owl` is in the `Resources` subfolder
next to `ClashAvoidancePlugin.dll`:
`{DLL folder}\Resources\clash_avoidance_ontology.owl`

### PluginLogic button does nothing

Check the Revit journal file for error messages:
`%LocalAppData%\Autodesk\Revit\Autodesk Revit 2024\Journals\`

Confirm .NET Framework 4.8 is installed via Windows Features.

### Wall height rule is not triggering

- Confirm the wall type has **Function = Interior** in its type properties
- Confirm the wall height exceeds `CA_CeilingHeightThreshold minus CA_MEPClearance`
- Confirm shared parameters are loaded and bound to Walls (Step 7)

### Linked model pin rule is not triggering

This rule fires on DocumentSaving and DocumentSynchronizing. Trigger it
by pressing Ctrl+S with an unpinned linked model loaded. Confirm the
check is active via the green status banner in the assistant window.

### Override log is not being created

Confirm the Revit project has been saved to disk at least once. Check
that Revit has write permission to the project folder.

### Build error: "The type or namespace 'Autodesk' could not be found"

Re-add the Revit API references via Solution Explorer:
References → Add Reference → Browse →
`C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll`
Repeat for `RevitAPIUI.dll`. Set **Copy Local = False** on both.

### Build fails — Revit 2024 is not installed on this machine

The plugin cannot be compiled without `RevitAPI.dll` and `RevitAPIUI.dll`
present on the build machine. Build on a machine where Revit 2024 is
installed, then copy the DLL and Resources folder to the deployment machine.

---

## Contact

**Omar Doukari**
School of Architecture and Built Environment
Northumbria University, Newcastle, United Kingdom
omar.doukari@northumbria.ac.uk

**Ontology and rule repository:**
https://github.com/tripleHatDeveloper/bim-clash-avoidance-rules-ontology
