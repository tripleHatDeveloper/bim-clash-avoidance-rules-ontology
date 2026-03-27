using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using ClashAvoidancePlugin.Commands;
using ClashAvoidancePlugin.Engine;

namespace ClashAvoidancePlugin
{
    /// <summary>
    /// IExternalApplication entry point.
    ///
    /// Ribbon layout (per specification):
    ///   Tab name  : ClashAvoidanceTutorial
    ///   Panel name: ClashAvoidanceTutorial
    ///   Button    : PluginLogic
    ///
    /// Clicking PluginLogic opens the Clash Avoidance Assistant dialog, which
    /// lets the designer choose between the tutorial and the live rule checker.
    /// </summary>
    public class App : IExternalApplication
    {
        // Singleton event handler — kept alive for the full Revit session.
        // Inactive until the designer selects "Launch Clash Avoidance Check".
        internal static ClashAvoidanceEventHandler EventHandler { get; private set; }

        // Absolute path to the folder containing this DLL.
        // Used by all components to locate OWL, HTML, and CSV files.
        internal static string PluginDirectory { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                PluginDirectory = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location);

                // Parse the OWL ontology once at startup and cache rule objects.
                // Subsequent rule evaluations read from the in-memory dictionary.
                string owlPath = Path.Combine(
                    PluginDirectory, "Resources", "clash_avoidance_ontology.owl");

                OntologyLoader.LoadOntology(owlPath);

                // Build the ribbon (inactive until the designer clicks PluginLogic)
                CreateRibbonPanel(application);

                // Instantiate the event handler; it subscribes to Revit events
                // only after the designer clicks "Launch Clash Avoidance Check".
                EventHandler = new ClashAvoidanceEventHandler();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("ClashAvoidanceTutorial — Startup Error", ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            EventHandler?.Deactivate(null);
            return Result.Succeeded;
        }

        // ------------------------------------------------------------------ //
        //  Ribbon construction
        // ------------------------------------------------------------------ //

        private void CreateRibbonPanel(UIControlledApplication app)
        {
            // Tab: ClashAvoidanceTutorial
            const string tabName = "ClashAvoidanceTutorial";
            app.CreateRibbonTab(tabName);

            // Panel: ClashAvoidanceTutorial
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "ClashAvoidanceTutorial");

            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // ── Single entry-point button: PluginLogic ───────────────────── //
            PushButtonData pluginLogicData = new PushButtonData(
                name        : "PluginLogic",
                text        : "PluginLogic",
                assemblyName: assemblyPath,
                className   : typeof(PluginLogicCommand).FullName)
            {
                ToolTip = "Open the Clash Avoidance Assistant.",
                LongDescription =
                    "Opens the Clash Avoidance Assistant window where you can " +
                    "view the coordination rule tutorial or launch real-time " +
                    "clash avoidance checking as a background process.",
                Image      = LoadIcon("plugin_small.png"),
                LargeImage = LoadIcon("plugin_large.png")
            };

            panel.AddItem(pluginLogicData);
        }

        // ------------------------------------------------------------------ //
        //  Icon loader — gracefully handles missing image files
        // ------------------------------------------------------------------ //

        private BitmapImage LoadIcon(string filename)
        {
            try
            {
                string path = Path.Combine(PluginDirectory, "Resources", filename);
                if (!File.Exists(path)) return null;

                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                return bmp;
            }
            catch { return null; }
        }
    }
}
