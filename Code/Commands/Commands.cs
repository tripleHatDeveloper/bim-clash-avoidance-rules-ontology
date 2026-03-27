using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClashAvoidancePlugin.Logging;
using ClashAvoidancePlugin.UI;

namespace ClashAvoidancePlugin.Commands
{
    // ======================================================================= //
    //  PluginLogic — the single ribbon button command
    // ======================================================================= //

    /// <summary>
    /// Invoked when the designer clicks the "PluginLogic" button in the
    /// ClashAvoidanceTutorial ribbon panel.
    ///
    /// Opens the Clash Avoidance Assistant window, which presents two choices:
    ///   -> View Clash Avoidance Tutorial  (opens HTML rule library)
    ///   -> Launch Clash Avoidance Check   (activates background rule checking)
    ///
    /// After the window is closed the background check continues running because
    /// the event handler is subscribed to application-level Revit events, not
    /// tied to the dialog lifetime.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class PluginLogicCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                var dialog = new AssistantDialog(uiApp);
                dialog.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    // ======================================================================= //
    //  ViewOverrideLogCommand — retained, can be wired to ribbon if needed
    // ======================================================================= //

    /// <summary>
    /// Opens the CSV override log for the current project using the system
    /// default application (typically Excel or Notepad).
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ViewOverrideLogCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument?.Document;
                string projectPath = doc?.PathName ?? string.Empty;
                string logPath = OverrideLogger.ResolveLogPath(projectPath);

                if (!File.Exists(logPath))
                {
                    TaskDialog.Show(
                        "ClashAvoidanceTutorial - Override Log",
                        "No override log found for this project.\n\n" +
                        "The log is created automatically the first time a designer " +
                        "overrides a coordination rule violation.\n\n" +
                        $"Expected location:\n{logPath}");
                    return Result.Succeeded;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName        = logPath,
                    UseShellExecute = true
                });

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
