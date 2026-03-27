# BIM Coordination Rules, Ontology, and Clash Avoidance Plugin

## Overview

This repository provides a prototype implementation of an authoring-time BIM coordination clash avoidance system. It includes a validated set of expert-derived coordination rules, an OWL ontology formalising coordination knowledge, and a working Revit plugin that evaluates these rules in real time during BIM authoring.

---

## Repository Structure

### /Code
The Revit plugin implementation — a C# .NET add-in for Autodesk Revit 2024 that checks coordination rules in real time as designers model.

- Event-driven architecture that intercepts element creation and modification
- Loads and evaluates rules directly from the OWL ontology at runtime
- Shows targeted violation dialogs when a rule is breached
- Supports professional override with mandatory justification logging
- Includes a full installation and user guide (`USER GUIDE.md`)

### /Ontology
The OWL ontology formalising coordination knowledge and rule definitions.

- Represents coordination concepts, building components, clash types, and rules
- Encodes a subset of the 35 expert-validated rules as named individuals with condition, constraint, and rationale properties
- Enables logical consistency checking and is designed to be easily extended.

### /Rules
The validated set of 35 coordination rules derived from expert knowledge elicitation.

- Covers architectural, structural, and MEP disciplines
- Each rule is structured with a condition, constraint, rationale, and classification
- Includes an interactive HTML rule library (`tutorial_rules_library.html`) for browsing and filtering rules by discipline and automation type

---

## Scope

The system targets hard clash prevention during specialty model authoring across architectural, structural, and MEP disciplines. It operates at authoring time — before models are federated — to reduce coordination errors at the point where design decisions are made.

---

## Usage

- The Revit plugin (`/Code`) can be installed and used directly in Revit 2024 projects following the included installation guide
- The ontology (`/Ontology`) can be inspected, extended, or reused in other BIM coordination tools and workflows
- The rule set (`/Rules`) can be adapted for use in other projects, platforms, or coordination frameworks

---

## Notes

- The plugin requires Autodesk Revit 2024 and Visual Studio to build — see `/Code/USER GUIDE.md`
- No proprietary project data or BIM models are included
- The rules reflect expert-informed coordination practices and may require adaptation to specific project contexts

---

## Contact

**Omar Doukari**
School of Architecture and Built Environment
Northumbria University, Newcastle, United Kingdom
omar.doukari@northumbria.ac.uk