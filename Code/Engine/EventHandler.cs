using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using ClashAvoidancePlugin.Logging;
using ClashAvoidancePlugin.UI;

namespace ClashAvoidancePlugin.Engine
{
    /// <summary>
    /// Event-driven constraint enforcement layer.
    ///
    /// Architecture:
    ///   – Subscribes to four Revit application-level events:
    ///       1. DocumentChanged        → element creation / modification
    ///       2. DocumentSaving         → final pre-save check
    ///       3. DocumentSynchronizing  → WorkShared sync check
    ///       4. DocumentOpened         → initial linked model pin check
    ///
    ///   – On each event, retrieves modified element IDs, delegates to RuleEngine,
    ///     and routes violations to rule-specific dialogs:
    ///         LinkedModelPinnedDialog    for linked model pin violations
    ///         WallHeightDialog           for interior wall height violations
    ///         ViolationPromptDialog      for all other rule violations (fallback)
    ///
    ///   – The handler is activated / deactivated by the AssistantDialog
    ///     (opened via the PluginLogic ribbon button), allowing designers to
    ///     enable or disable checking per session.
    ///
    /// Performance strategy:
    ///   – Only modified elements are evaluated (incremental evaluation).
    ///   – CategoryRuleMap cache avoids iterating all rules for every element.
    ///   – WPF dialog is shown only when at least one violation is detected.
    ///   – Target: median < 30 ms, P95 < 100 ms.
    /// </summary>
    public class ClashAvoidanceEventHandler
    {
        private UIApplication _uiApp;
        private readonly RuleEngine _engine = new RuleEngine();
        private bool _isActive = false;

        // Expose activation state so the ribbon button can reflect it
        public bool IsActive => _isActive;

        // ------------------------------------------------------------------ //
        //  Activation / Deactivation
        // ------------------------------------------------------------------ //

        public void Activate(UIApplication uiApp)
        {
            if (_isActive) return;

            _uiApp = uiApp;
            var app = uiApp.Application;

            app.DocumentChanged        += OnDocumentChanged;
            app.DocumentSaving         += OnDocumentSaving;
            app.DocumentSynchronizingWithCentral += OnDocumentSynchronizing;
            app.DocumentOpened         += OnDocumentOpened;

            _isActive = true;
        }

        public void Deactivate(UIApplication uiApp)
        {
            if (!_isActive) return;

            var app = (uiApp ?? _uiApp)?.Application;
            if (app == null) { _isActive = false; return; }

            app.DocumentChanged        -= OnDocumentChanged;
            app.DocumentSaving         -= OnDocumentSaving;
            app.DocumentSynchronizingWithCentral -= OnDocumentSynchronizing;
            app.DocumentOpened         -= OnDocumentOpened;

            _isActive = false;
        }

        // ------------------------------------------------------------------ //
        //  Event 1: DocumentChanged
        //  Triggered on every element creation and modification during modelling.
        //  This is the primary real-time enforcement point.
        // ------------------------------------------------------------------ //

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            try
            {
                Document doc = e.GetDocument();
                if (doc == null || doc.IsFamilyDocument) return;

                // Collect all added and modified element IDs
                var modifiedIds = new List<ElementId>();
                modifiedIds.AddRange(e.GetAddedElementIds());
                modifiedIds.AddRange(e.GetModifiedElementIds());

                if (modifiedIds.Count == 0) return;

                // Evaluate rules (incremental — only changed elements)
                var violations = _engine.EvaluateElements(doc, modifiedIds);

                if (violations.Count > 0)
                    ShowViolationPrompts(doc, violations);
            }
            catch (Exception ex)
            {
                // Never crash Revit — log silently
                System.Diagnostics.Debug.WriteLine(
                    $"[ClashAvoidance] DocumentChanged error: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Event 2: DocumentSaving
        //  Checks linked model pin status before the model is saved.
        //  Ensures coordination state is valid at each save point.
        // ------------------------------------------------------------------ //

        private void OnDocumentSaving(object sender, DocumentSavingEventArgs e)
        {
            try
            {
                Document doc = e.Document;
                if (doc == null || doc.IsFamilyDocument) return;

                var violations = _engine.EvaluateLinkedModels(doc);

                if (violations.Count > 0)
                    ShowViolationPrompts(doc, violations);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ClashAvoidance] DocumentSaving error: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Event 3: DocumentSynchronizing (WorkShared)
        //  Checks linked model pin status before synchronising with central.
        //  Prevents coordination errors propagating to the central model.
        // ------------------------------------------------------------------ //

        private void OnDocumentSynchronizing(
            object sender, DocumentSynchronizingWithCentralEventArgs e)
        {
            try
            {
                Document doc = e.Document;
                if (doc == null || doc.IsFamilyDocument) return;

                var violations = _engine.EvaluateLinkedModels(doc);

                if (violations.Count > 0)
                    ShowViolationPrompts(doc, violations);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ClashAvoidance] DocumentSynchronizing error: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Event 4: DocumentOpened
        //  Runs an initial check when a project is opened to surface any
        //  pre-existing unresolved violations (e.g. unpinned linked models).
        // ------------------------------------------------------------------ //

        private void OnDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            try
            {
                Document doc = e.Document;
                if (doc == null || doc.IsFamilyDocument) return;

                var violations = _engine.EvaluateLinkedModels(doc);

                if (violations.Count > 0)
                    ShowViolationPrompts(doc, violations);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ClashAvoidance] DocumentOpened error: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Violation presentation — rule-specific dialogs
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Routes each violation to its dedicated dialog:
        ///
        ///   Rule_LinkedModelsPinned    → LinkedModelPinnedDialog
        ///     Title  : ClashAvoidanceTutorial - Linked Model Pinned Status
        ///     Content: Error! Please pin the model 'MODEL NAME'.
        ///
        ///   Rule_A02_InteriorWallHeight → WallHeightDialog
        ///     Title  : ClashAvoidanceTutorial - Wall Height Status
        ///     Content: Height cannot be more than 10ft in this project.
        ///              Please change the height of the following walls 'WALL NAME'.
        ///
        /// Violations of the same rule type are batched into one dialog so the
        /// designer is not interrupted with repeated pop-ups per element.
        /// Each dialog independently supports Override + justification logging.
        /// </summary>
        private void ShowViolationPrompts(Document doc, List<RuleViolation> violations)
        {
            try
            {
                string projectPath = doc.PathName;
                string userName    = doc.Application.Username;

                // ── Group violations by rule ID ──────────────────────────── //
                var linkedModelViolations = violations
                    .Where(v => v.Rule.Id == "Rule_LinkedModelsPinned")
                    .ToList();

                var wallHeightViolations = violations
                    .Where(v => v.Rule.Id == "Rule_A02_InteriorWallHeight")
                    .ToList();

                var otherViolations = violations
                    .Where(v => v.Rule.Id != "Rule_LinkedModelsPinned"
                             && v.Rule.Id != "Rule_A02_InteriorWallHeight")
                    .ToList();

                // ── 1. Linked model pin dialog ───────────────────────────── //
                if (linkedModelViolations.Count > 0)
                {
                    var dialog = new LinkedModelPinnedDialog(linkedModelViolations);
                    dialog.ShowDialog();

                    if (dialog.IsOverride)
                        LogOverrides(linkedModelViolations, projectPath,
                                     userName, dialog.OverrideJustification);
                }

                // ── 2. Wall height dialog ────────────────────────────────── //
                if (wallHeightViolations.Count > 0)
                {
                    var dialog = new WallHeightDialog(wallHeightViolations);
                    dialog.ShowDialog();

                    if (dialog.IsOverride)
                        LogOverrides(wallHeightViolations, projectPath,
                                     userName, dialog.OverrideJustification);
                }

                // ── 3. Any remaining rules (dimensional threshold, etc.) ─── //
                // These use the generic ViolationPromptDialog as a fallback.
                if (otherViolations.Count > 0)
                {
                    var dialog = new ViolationPromptDialog(otherViolations);
                    dialog.ShowDialog();

                    if (dialog.IsOverride)
                        LogOverrides(otherViolations, projectPath,
                                     userName, dialog.OverrideJustification);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ClashAvoidance] ShowViolationPrompts error: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Logging helper
        // ------------------------------------------------------------------ //

        private void LogOverrides(
            List<RuleViolation> violations,
            string projectPath,
            string userName,
            string justification)
        {
            foreach (var v in violations)
            {
                OverrideLogger.LogOverride(
                    projectPath    : projectPath,
                    timestamp      : DateTime.Now,
                    userName       : userName,
                    elementId      : v.ElementId?.IntegerValue.ToString() ?? "N/A",
                    elementDesc    : v.ElementDescription,
                    ruleId         : v.Rule.Id,
                    ruleLabel      : v.Rule.Label,
                    violationDetail: v.ViolationDetail,
                    justification  : justification);
            }
        }
    }
}
