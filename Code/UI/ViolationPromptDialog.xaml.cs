using System.Collections.Generic;
using System.Windows;
using ClashAvoidancePlugin.Engine;

namespace ClashAvoidancePlugin.UI
{
    /// <summary>
    /// Code-behind for ViolationPromptDialog.xaml.
    ///
    /// Displays one or more rule violations in a single batched dialog.
    /// The designer has two options:
    ///
    ///   Fix Model     — closes the dialog; designer corrects the element manually.
    ///                   No log entry is written.
    ///
    ///   Override Rule — expands the justification panel. Once a justification
    ///                   is entered and confirmed, IsOverride = true and
    ///                   OverrideJustification is set. The caller (EventHandler)
    ///                   writes the override to the CSV log.
    /// </summary>
    public partial class ViolationPromptDialog : Window
    {
        public bool IsOverride { get; private set; } = false;
        public string OverrideJustification { get; private set; } = string.Empty;

        public ViolationPromptDialog(List<RuleViolation> violations)
        {
            InitializeComponent();
            ViolationsList.ItemsSource = violations;
        }

        // ── Fix Model ────────────────────────────────────────────────────── //

        private void FixModelBtn_Click(object sender, RoutedEventArgs e)
        {
            IsOverride = false;
            DialogResult = true;
            Close();
        }

        // ── Override toggle ──────────────────────────────────────────────── //

        private void OverrideToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            // Show the justification panel and the Confirm Override button
            OverridePanel.Visibility   = Visibility.Visible;
            ConfirmOverrideBtn.Visibility = Visibility.Visible;
            OverrideToggleBtn.Visibility  = Visibility.Collapsed;
            JustificationBox.Focus();
        }

        // ── Confirm Override ─────────────────────────────────────────────── //

        private void ConfirmOverrideBtn_Click(object sender, RoutedEventArgs e)
        {
            string justification = JustificationBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(justification))
            {
                MessageBox.Show(
                    "Please enter a justification before confirming the override.\n\n" +
                    "A brief note describing why this rule does not apply in this " +
                    "context is required for audit traceability.",
                    "ClashAvoidanceTutorial - Justification Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                JustificationBox.Focus();
                return;
            }

            IsOverride = true;
            OverrideJustification = justification;
            DialogResult = true;
            Close();
        }
    }
}
