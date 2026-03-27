using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace ClashAvoidancePlugin.Engine
{
    /// <summary>
    /// Represents a single clash avoidance rule as parsed from the OWL ontology.
    /// Maps directly to the ClashAvoidanceRule named individuals in the OWL file.
    /// </summary>
    public class OntologyRule
    {
        public string Id { get; set; }              // rdf:about fragment, e.g. "Rule_A02_InteriorWallHeight"
        public string Label { get; set; }           // rdfs:label
        public string ConditionText { get; set; }   // conditionText data property
        public string ConstraintText { get; set; }  // constraintText data property
        public string RationaleText { get; set; }   // rationaleText data property
        public string AppliesTo { get; set; }       // appliesTo object property target (class fragment)
        public string AppliesToSecondary { get; set; } // second appliesTo (e.g. MEP insulation applies to Pipe AND Duct)
        public string Prevents { get; set; }        // prevents object property target
        public string RelationshipType { get; set; }// hasRelationshipType
        public string RequirementType { get; set; } // hasRequirementType
        public string StageType { get; set; }       // hasStageType
        public string ClashType { get; set; }       // hasClashType
        public string WorkflowType { get; set; }    // hasWorkflowType

        public override string ToString() =>
            $"[{Id}] {Label} | AppliesTo={AppliesTo} | Req={RequirementType}";
    }

    /// <summary>
    /// Parses the OWL 2 ontology XML file at runtime and exposes a dictionary
    /// of OntologyRule objects keyed by their fragment identifier.
    ///
    /// The OWL file uses standard RDF/XML serialisation. We extract only
    /// owl:NamedIndividual elements that are typed as #ClashAvoidanceRule,
    /// then read their data and object property assertions.
    ///
    /// No external RDF library is required — the file is treated as XML,
    /// which is sufficient because the ontology uses direct triple assertions
    /// rather than blank-node encodings.
    /// </summary>
    public static class OntologyLoader
    {
        // Loaded rules, keyed by fragment id (e.g. "Rule_A02_InteriorWallHeight")
        public static Dictionary<string, OntologyRule> Rules { get; private set; }
            = new Dictionary<string, OntologyRule>();

        // Namespaces used in the OWL file
        private static readonly XNamespace Rdf  = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private static readonly XNamespace Owl  = "http://www.w3.org/2002/07/owl#";
        private static readonly XNamespace Rdfs = "http://www.w3.org/2000/01/rdf-schema#";

        // The ontology's own base namespace (fragment prefix)
        private const string OntologyBase =
            "https://github.com/tripleHatDeveloper/bim-clash-avoidance-rules-ontology#";

        /// <summary>
        /// Loads the ontology from the given OWL/XML file path.
        /// Must be called once before any rule evaluation.
        /// </summary>
        public static void LoadOntology(string owlFilePath)
        {
            if (!File.Exists(owlFilePath))
                throw new FileNotFoundException(
                    $"OWL ontology file not found at: {owlFilePath}\n" +
                    "Ensure clash_avoidance_ontology.owl is in the plugin Resources folder.");

            Rules.Clear();

            XDocument doc = XDocument.Load(owlFilePath);
            XElement root = doc.Root; // rdf:RDF

            // Iterate all owl:NamedIndividual elements
            foreach (XElement individual in root.Elements(Owl + "NamedIndividual"))
            {
                string aboutAttr = individual.Attribute(Rdf + "about")?.Value;
                if (string.IsNullOrEmpty(aboutAttr)) continue;

                // Check if this individual is typed as a ClashAvoidanceRule
                bool isRule = false;
                foreach (XElement typeEl in individual.Elements(Rdf + "type"))
                {
                    string typeResource = typeEl.Attribute(Rdf + "resource")?.Value ?? "";
                    if (typeResource.EndsWith("#ClashAvoidanceRule") ||
                        typeResource == OntologyBase + "ClashAvoidanceRule")
                    {
                        isRule = true;
                        break;
                    }
                }

                if (!isRule) continue;

                OntologyRule rule = new OntologyRule
                {
                    Id = FragmentOf(aboutAttr),
                    Label          = GetDataValue(individual, Rdfs + "label"),
                    ConditionText  = GetDataValue(individual, OntologyBase + "conditionText"),
                    ConstraintText = GetDataValue(individual, OntologyBase + "constraintText"),
                    RationaleText  = GetDataValue(individual, OntologyBase + "rationaleText"),
                    Prevents       = GetObjectFragment(individual, OntologyBase + "prevents"),
                    RelationshipType = GetObjectFragment(individual, OntologyBase + "hasRelationshipType"),
                    RequirementType  = GetObjectFragment(individual, OntologyBase + "hasRequirementType"),
                    StageType        = GetObjectFragment(individual, OntologyBase + "hasStageType"),
                    ClashType        = GetObjectFragment(individual, OntologyBase + "hasClashType"),
                    WorkflowType     = GetObjectFragment(individual, OntologyBase + "hasWorkflowType"),
                };

                // appliesTo can appear multiple times (e.g. Pipe AND Duct)
                var appliesToValues = GetAllObjectFragments(individual, OntologyBase + "appliesTo");
                if (appliesToValues.Count > 0) rule.AppliesTo = appliesToValues[0];
                if (appliesToValues.Count > 1) rule.AppliesToSecondary = appliesToValues[1];

                Rules[rule.Id] = rule;
            }

            if (Rules.Count == 0)
                throw new InvalidDataException(
                    "No ClashAvoidanceRule individuals found in the OWL file. " +
                    "Verify the file is the correct ontology.");
        }

        // ------------------------------------------------------------------ //
        //  XML helpers
        // ------------------------------------------------------------------ //

        /// <summary>Returns the fragment (after #) of a full URI.</summary>
        private static string FragmentOf(string uri)
        {
            int hash = uri.LastIndexOf('#');
            return hash >= 0 ? uri.Substring(hash + 1) : uri;
        }

        /// <summary>Gets the text value of a data property element.</summary>
        private static string GetDataValue(XElement parent, string propertyUri)
        {
            // Try as XName with namespace + local
            XName xn = XName.Get(
                propertyUri.Substring(propertyUri.LastIndexOf('#') + 1),
                propertyUri.Substring(0, propertyUri.LastIndexOf('#') + 1));

            XElement el = parent.Element(xn);
            return el?.Value?.Trim() ?? string.Empty;
        }

        /// <summary>Gets the fragment of the rdf:resource attribute of an object property.</summary>
        private static string GetObjectFragment(XElement parent, string propertyUri)
        {
            XName xn = XName.Get(
                propertyUri.Substring(propertyUri.LastIndexOf('#') + 1),
                propertyUri.Substring(0, propertyUri.LastIndexOf('#') + 1));

            XElement el = parent.Element(xn);
            if (el == null) return string.Empty;

            string resource = el.Attribute(Rdf + "resource")?.Value ?? string.Empty;
            return FragmentOf(resource);
        }

        /// <summary>Gets all fragments for a potentially multi-valued object property.</summary>
        private static List<string> GetAllObjectFragments(XElement parent, string propertyUri)
        {
            XName xn = XName.Get(
                propertyUri.Substring(propertyUri.LastIndexOf('#') + 1),
                propertyUri.Substring(0, propertyUri.LastIndexOf('#') + 1));

            var results = new List<string>();
            foreach (XElement el in parent.Elements(xn))
            {
                string resource = el.Attribute(Rdf + "resource")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(resource))
                    results.Add(FragmentOf(resource));
            }
            return results;
        }
    }
}
