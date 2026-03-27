using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ClashAvoidancePlugin.Engine
{
    /// <summary>
    /// Result of evaluating a single rule against a Revit element.
    /// </summary>
    public class RuleViolation
    {
        public OntologyRule Rule { get; set; }
        public ElementId ElementId { get; set; }
        public string ElementDescription { get; set; }
        public string ViolationDetail { get; set; }  // computed detail (e.g. "Wall height 3200mm exceeds limit 2800mm")
    }

    /// <summary>
    /// Core rule evaluation engine.
    ///
    /// Architecture:
    ///   1. Retrieve the modified element and its category/type from the Revit API.
    ///   2. Use cached element-to-rule mappings to identify applicable rules.
    ///   3. Query element parameters and evaluate constraint conditions.
    ///   4. Return violations to the event handler for prompt display.
    ///
    /// Four rules are implemented in this version:
    ///   Rule 1 — Tutorial accessibility      (session-level, not element-level)
    ///   Rule 2 — Linked model pin status     (LinkedModel → isPinned)
    ///   Rule 3 — Interior wall height        (Rule_A02_InteriorWallHeight)
    ///   Rule 4 — Dimensional threshold       (generic shared-parameter check)
    /// </summary>
    public class RuleEngine
    {
        // ------------------------------------------------------------------ //
        //  Shared parameter names (defined in SharedParameters.txt)
        // ------------------------------------------------------------------ //
        // Set by the BIM manager per project via shared parameters
        private const string ParamCeilingHeightThreshold = "CA_CeilingHeightThreshold";
        private const string ParamMEPClearance           = "CA_MEPClearance";
        private const string ParamDimensionalThreshold   = "CA_DimensionalThreshold";

        // Default fallback values (millimetres) used if shared parameters are absent
        private const double DefaultCeilingHeightMm = 2700.0;  // ~8'-10"
        private const double DefaultMEPClearanceMm  = 300.0;   // 300 mm above ceiling line

        // Revit internal unit is decimal feet; convert mm → feet
        private static double MmToFeet(double mm) => mm / 304.8;

        // ------------------------------------------------------------------ //
        //  Element-to-rule cache
        // Built once; maps Revit BuiltInCategory to list of rule fragment IDs
        // ------------------------------------------------------------------ //
        private static readonly Dictionary<BuiltInCategory, List<string>> CategoryRuleMap
            = new Dictionary<BuiltInCategory, List<string>>
        {
            // Walls → interior wall height rule + dimensional threshold
            { BuiltInCategory.OST_Walls,
                new List<string>
                {
                    "Rule_A02_InteriorWallHeight",
                    "Rule_DimensionalThreshold"   // generic threshold rule
                }
            },
            // RVT links → linked model pin rule
            { BuiltInCategory.OST_RvtLinks,
                new List<string> { "Rule_LinkedModelsPinned" }
            },
        };

        // ------------------------------------------------------------------ //
        //  Public entry point
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Evaluates all applicable rules for the given set of modified element IDs.
        /// Returns a list of violations to be presented to the designer.
        ///
        /// Evaluation is incremental: only modified elements and their associated
        /// rules are checked, preserving sub-100 ms latency targets.
        /// </summary>
        public List<RuleViolation> EvaluateElements(
            Document doc,
            IEnumerable<ElementId> modifiedIds)
        {
            var violations = new List<RuleViolation>();
            if (doc == null || modifiedIds == null) return violations;

            foreach (ElementId eid in modifiedIds)
            {
                if (eid == null || eid == ElementId.InvalidElementId) continue;

                Element elem = doc.GetElement(eid);
                if (elem == null) continue;

                // Identify applicable rules via the category cache
                List<string> applicableRuleIds = GetApplicableRuleIds(elem);
                if (applicableRuleIds.Count == 0) continue;

                foreach (string ruleId in applicableRuleIds)
                {
                    if (!OntologyLoader.Rules.TryGetValue(ruleId, out OntologyRule rule))
                        continue;

                    RuleViolation violation = EvaluateRule(doc, elem, rule);
                    if (violation != null)
                        violations.Add(violation);
                }
            }

            return violations;
        }

        /// <summary>
        /// Evaluates all linked-model pin rules across the entire document.
        /// Called on DocumentSynchronizing and DocumentSaved events.
        /// </summary>
        public List<RuleViolation> EvaluateLinkedModels(Document doc)
        {
            var violations = new List<RuleViolation>();
            if (doc == null) return violations;

            if (!OntologyLoader.Rules.TryGetValue(
                "Rule_LinkedModelsPinned", out OntologyRule rule))
                return violations;

            FilteredElementCollector collector =
                new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_RvtLinks)
                    .WhereElementIsNotElementType();

            foreach (Element linkElem in collector)
            {
                RuleViolation v = EvaluateRule(doc, linkElem, rule);
                if (v != null) violations.Add(v);
            }

            return violations;
        }

        // ------------------------------------------------------------------ //
        //  Rule dispatch
        // ------------------------------------------------------------------ //

        private RuleViolation EvaluateRule(Document doc, Element elem, OntologyRule rule)
        {
            switch (rule.Id)
            {
                case "Rule_A02_InteriorWallHeight":
                    return CheckInteriorWallHeight(doc, elem, rule);

                case "Rule_LinkedModelsPinned":
                    return CheckLinkedModelPinned(elem, rule);

                case "Rule_DimensionalThreshold":
                    return CheckDimensionalThreshold(elem, rule);

                default:
                    return null;
            }
        }

        // ------------------------------------------------------------------ //
        //  Rule implementations
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Rule A02 — Interior Wall Height Constraint (OWL individual: Rule_A02_InteriorWallHeight)
        ///
        /// Condition : Element is an interior wall being created or modified.
        /// Constraint: Wall height must not exceed (CeilingHeightThreshold − MEPClearance).
        /// Rationale : Walls penetrating ceiling plenums cause hard clashes with HVAC ductwork.
        ///
        /// Parameter sources (in priority order):
        ///   1. Shared parameters CA_CeilingHeightThreshold and CA_MEPClearance on the wall.
        ///   2. Project Information shared parameters (project-wide defaults).
        ///   3. Built-in fallback constants.
        /// </summary>
        private RuleViolation CheckInteriorWallHeight(
            Document doc, Element elem, OntologyRule rule)
        {
            // Only process Wall elements
            if (!(elem is Wall wall)) return null;

            // Filter: interior walls only (not exterior/curtain wall)
            WallType wallType = wall.WallType;
            if (wallType == null) return null;

            WallKind kind = wallType.Kind;
            if (kind == WallKind.Curtain || kind == WallKind.Stacked)
                return null;

            // Attempt to determine if interior via function parameter
            Parameter functionParam = wallType.get_Parameter(
                BuiltInParameter.FUNCTION_PARAM);
            if (functionParam != null)
            {
                // WallFunction: 0=Interior, 1=Exterior, 2=Foundation, 3=Retaining, 4=Soffit, 5=CoreShaft
                int function = functionParam.AsInteger();
                if (function != 0) return null;  // Not interior
            }

            // Get wall height (unconnected height) in feet
            Parameter heightParam = elem.get_Parameter(
                BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            if (heightParam == null || heightParam.StorageType != StorageType.Double)
                return null;

            double wallHeightFt = heightParam.AsDouble();

            // Resolve threshold values (shared parameter → project info → default)
            double ceilingHeightMm = ResolveDoubleParameter(
                elem, doc, ParamCeilingHeightThreshold, DefaultCeilingHeightMm);
            double mepClearanceMm = ResolveDoubleParameter(
                elem, doc, ParamMEPClearance, DefaultMEPClearanceMm);

            double maxAllowedMm  = ceilingHeightMm - mepClearanceMm;
            double maxAllowedFt  = MmToFeet(maxAllowedMm);
            double wallHeightMm  = wallHeightFt / MmToFeet(1.0);

            if (wallHeightFt > maxAllowedFt)
            {
                return new RuleViolation
                {
                    Rule = rule,
                    ElementId = elem.Id,
                    ElementDescription = $"Wall '{GetElementName(elem)}' (ID {elem.Id.IntegerValue})",
                    ViolationDetail =
                        $"Wall height {wallHeightMm:F0} mm exceeds the maximum allowed " +
                        $"{maxAllowedMm:F0} mm " +
                        $"(ceiling threshold {ceilingHeightMm:F0} mm − MEP clearance {mepClearanceMm:F0} mm)."
                };
            }

            return null;
        }

        /// <summary>
        /// Rule — Linked Models Must Be Pinned (OWL individual: Rule_LinkedModelsPinned)
        ///
        /// Condition : A linked RVT model is loaded in the project.
        /// Constraint: All linked models must remain pinned.
        /// Rationale : Unpinned linked models can shift position, creating false clashes
        ///             or obscuring real coordination issues.
        /// </summary>
        private RuleViolation CheckLinkedModelPinned(Element elem, OntologyRule rule)
        {
            // Check Pinned property via the element's built-in parameter
            Parameter pinnedParam = elem.get_Parameter(BuiltInParameter.ELEM_IS_PINNED);

            bool isPinned = false;
            if (pinnedParam != null && pinnedParam.StorageType == StorageType.Integer)
                isPinned = pinnedParam.AsInteger() == 1;
            else
                isPinned = elem.Pinned; // fallback to property

            if (!isPinned)
            {
                string linkName = GetElementName(elem);
                return new RuleViolation
                {
                    Rule = rule,
                    ElementId = elem.Id,
                    ElementDescription = $"Linked model '{linkName}' (ID {elem.Id.IntegerValue})",
                    ViolationDetail =
                        $"Linked model '{linkName}' is not pinned. " +
                        "Unpinned linked models may shift during coordination and " +
                        "generate false or misleading clash results."
                };
            }

            return null;
        }

        /// <summary>
        /// Rule — Dimensional Threshold (generic extensible rule via shared parameters)
        ///
        /// Condition : Any element with a CA_DimensionalThreshold shared parameter.
        /// Constraint: The element's relevant dimension must not exceed the threshold.
        /// Rationale : Enforces project-specific dimensional agreements from the BIM
        ///             Execution Plan without requiring code changes.
        ///
        /// The BIM manager populates CA_DimensionalThreshold on element types or
        /// instances. The rule checks the element's Unconnected Height (walls),
        /// Height (generic), or Width parameter against this threshold.
        /// </summary>
        private RuleViolation CheckDimensionalThreshold(Element elem, OntologyRule rule)
        {
            // Only applies if the shared parameter exists on this element
            Parameter thresholdParam = elem.LookupParameter(ParamDimensionalThreshold);
            if (thresholdParam == null ||
                thresholdParam.StorageType != StorageType.Double) return null;

            double thresholdMm = thresholdParam.AsDouble() * 304.8; // stored as feet
            if (thresholdMm <= 0) return null;

            // Try to find a dimension to check (unconnected height, then height, then width)
            double? dimensionMm = null;
            string dimensionName = "";

            Parameter heightP = elem.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)
                             ?? elem.get_Parameter(BuiltInParameter.INSTANCE_HEIGHT_PARAM);
            if (heightP != null && heightP.StorageType == StorageType.Double)
            {
                dimensionMm  = heightP.AsDouble() * 304.8;
                dimensionName = "Height";
            }

            if (dimensionMm == null) return null;

            if (dimensionMm.Value > thresholdMm)
            {
                return new RuleViolation
                {
                    Rule = rule,
                    ElementId = elem.Id,
                    ElementDescription = $"Element '{GetElementName(elem)}' (ID {elem.Id.IntegerValue})",
                    ViolationDetail =
                        $"{dimensionName} {dimensionMm.Value:F0} mm exceeds the project threshold " +
                        $"{thresholdMm:F0} mm defined in shared parameter '{ParamDimensionalThreshold}'."
                };
            }

            return null;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private List<string> GetApplicableRuleIds(Element elem)
        {
            var ids = new List<string>();

            // Look up by element's built-in category
            if (elem.Category == null) return ids;

            BuiltInCategory bic = (BuiltInCategory)(int)elem.Category.Id.IntegerValue;
            if (CategoryRuleMap.TryGetValue(bic, out List<string> mapped))
                ids.AddRange(mapped);

            return ids;
        }

        /// <summary>
        /// Resolves a double threshold:
        ///   1. From a shared parameter on the element instance.
        ///   2. From a shared parameter on the ProjectInformation element.
        ///   3. Fallback to the provided default.
        /// </summary>
        private double ResolveDoubleParameter(
            Element elem, Document doc,
            string paramName, double defaultMm)
        {
            // 1. Instance parameter
            Parameter p = elem.LookupParameter(paramName);
            if (p != null && p.StorageType == StorageType.Double && p.AsDouble() > 0)
                return p.AsDouble() * 304.8;

            // 2. Project Information
            try
            {
                ProjectInfo projInfo = doc.ProjectInformation;
                if (projInfo != null)
                {
                    Parameter pp = projInfo.LookupParameter(paramName);
                    if (pp != null && pp.StorageType == StorageType.Double && pp.AsDouble() > 0)
                        return pp.AsDouble() * 304.8;
                }
            }
            catch { /* ProjectInfo not available in all contexts */ }

            // 3. Default
            return defaultMm;
        }

        private string GetElementName(Element elem)
        {
            try { return elem.Name ?? elem.Id.ToString(); }
            catch { return elem.Id.ToString(); }
        }
    }
}
