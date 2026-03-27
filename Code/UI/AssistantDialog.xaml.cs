using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Autodesk.Revit.UI;

namespace ClashAvoidancePlugin.UI
{
    /// <summary>
    /// Code-behind for AssistantDialog.xaml.
    ///
    /// This is the entry window opened by the PluginLogic ribbon button.
    ///
    /// Window title : "ClashAvoidanceTutorial - Clash Avoidance Assistant"
    /// Content text : "Please select the appropriate course of action..."
    ///
    /// Two action buttons:
    ///
    ///   → View Clash Avoidance Tutorial
    ///       Opens tutorial_rules_library.html in the system default browser.
    ///       The window stays open so the designer can still access the check.
    ///
    ///   → Launch Clash Avoidance Check
    ///       Activates the event-driven rule checker as a background process.
    ///       The button label updates to "Stop Clash Avoidance Check" if already
    ///       running, acting as a toggle.
    ///       The window stays open (non-modal) so the designer can close it and
    ///       continue modelling — the background process keeps running.
    /// </summary>
    public partial class AssistantDialog : Window
    {
        private readonly UIApplication _uiApp;

        public AssistantDialog(UIApplication uiApp)
        {
            InitializeComponent();
            _uiApp = uiApp;

            // Reflect current checking state when the dialog opens
            UpdateLaunchButtonState();
        }

        // ------------------------------------------------------------------ //
        //  Button 1 — View Clash Avoidance Tutorial
        // ------------------------------------------------------------------ //

        private void TutorialBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string htmlPath = Path.Combine(
                    App.PluginDirectory, "Resources", "tutorial_rules_library.html");

                if (!File.Exists(htmlPath))
                {
                    MessageBox.Show(
                        $"Tutorial file not found at:\n{htmlPath}\n\n" +
                        "Ensure tutorial_rules_library.html is in the plugin Resources folder.",
                        "ClashAvoidanceTutorial - File Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Open in default browser — window stays open
                Process.Start(new ProcessStartInfo
                {
                    FileName        = htmlPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open tutorial:\n{ex.Message}",
                    "ClashAvoidanceTutorial - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ------------------------------------------------------------------ //
        //  Button 2 — Launch / Stop Clash Avoidance Check
        // ------------------------------------------------------------------ //

        private void LaunchCheckBtn_Click(object sender, RoutedEventArgs e)
        {
            var handler = App.EventHandler;

            if (handler.IsActive)
            {
                // ── Currently running → stop ──────────────────────────────── //
                handler.Deactivate(_uiApp);
                UpdateLaunchButtonState();

                MessageBox.Show(
                    "Clash Avoidance Check has been stopped.\n\n" +
                    "Click '→ Launch Clash Avoidance Check' again to re-activate.",
                    "ClashAvoidanceTutorial - Check Stopped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                // ── Not running → activate ────────────────────────────────── //
                handler.Activate(_uiApp);
                UpdateLaunchButtonState();

                MessageBox.Show(
                    "Clash Avoidance Check is now running in the background.\n\n" +
                    "The following rules are active:\n\n" +
                    "  → Linked model pin status\n" +
                    "  → Interior wall height vs. ceiling threshold\n" +
                    "  → Dimensional threshold (where shared parameters are set)\n\n" +
                    "Violations will appear as prompts as you model.\n" +
                    "You may close this window — the check continues running.",
                    "ClashAvoidanceTutorial - Check Activated",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Updates the Launch button label and the status banner to reflect
        /// whether the background check is currently active or inactive.
        /// </summary>
        private void UpdateLaunchButtonState()
        {
            bool isActive = App.EventHandler?.IsActive ?? false;

            if (isActive)
            {
                LaunchBtnLabel.Text    = "Stop Clash Avoidance Check";
                LaunchBtnSubtitle.Text = "Rule checking is currently active — click to deactivate";
                ActiveStatusBanner.Visibility = Visibility.Visible;
            }
            else
            {
                LaunchBtnLabel.Text    = "Launch Clash Avoidance Check";
                LaunchBtnSubtitle.Text =
                    "Activate real-time rule checking running in the background during modelling";
                ActiveStatusBanner.Visibility = Visibility.Collapsed;
            }
        }
    }
}
