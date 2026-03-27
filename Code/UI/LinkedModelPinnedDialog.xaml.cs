using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ClashAvoidancePlugin.Engine;

namespace ClashAvoidancePlugin.UI
{
    /// <summary>
    /// Dialog shown when one or more linked models are not pinned.
    ///
    /// Title  : ClashAvoidanceTutorial - Linked Model Pinned Status
    /// Content: Error! Please pin the model 'MODEL NAME'.
    ///
    /// Each unresolved linked model appears as a separate message line.
    /// The designer can acknowledge (OK) or override with a justification.
    /// </summary>
    public partial class LinkedModelPinnedDialog : Window
    {
        public bool IsOverride { get; private set; } = false;
        public string OverrideJustification { get; private set; } = string.Empty;

        public LinkedModelPinnedDialog(List<RuleViolation> violations)
        {
            InitializeComponent();

            // Populate the list with the name of each unpinned linked model
            var modelNames = violations
                .Select(v => ExtractModelName(v.ElementDescription))
                .ToList();

            ModelNamesList.ItemsSource = modelNames;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Extracts the model name from the element description string.
        /// ElementDescription format: "Linked model 'NAME' (ID 12345)"
        /// Returns just NAME, falling back to the full string if not parseable.
        /// </summary>
        private string ExtractModelName(string elementDescription)
        {
            if (string.IsNullOrEmpty(elementDescription))
                return elementDescription;

            int start = elementDescription.IndexOf('\'');
            int end   = elementDescription.LastIndexOf('\'');

            if (start >= 0 && end > start)
                return elementDescription.Substring(start + 1, end - start - 1);

            return elementDescription;
        }

        // ------------------------------------------------------------------ //
        //  Button handlers
        // ------------------------------------------------------------------ //

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            IsOverride = false;
            DialogResult = true;
            Close();
        }

        private void OverrideToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            OverridePanel.Visibility      = Visibility.Visible;
            ConfirmOverrideBtn.Visibility = Visibility.Visible;
            OverrideToggleBtn.Visibility  = Visibility.Collapsed;
            JustificationBox.Focus();
        }

        private void ConfirmOverrideBtn_Click(object sender, RoutedEventArgs e)
        {
            string justification = JustificationBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(justification))
            {
                MessageBox.Show(
                    "Please enter a justification before confirming the override.",
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
