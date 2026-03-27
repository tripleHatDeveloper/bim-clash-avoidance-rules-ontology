using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ClashAvoidancePlugin.Engine;

namespace ClashAvoidancePlugin.UI
{
    /// <summary>
    /// Dialog shown when one or more interior walls exceed the height limit.
    ///
    /// Title  : ClashAvoidanceTutorial - Wall Height Status
    /// Content: Height cannot be more than 10ft in this project.
    ///          Please change the height of the following walls 'WALL NAME'.
    ///
    /// All violating wall names are listed together so the designer can
    /// address all of them in a single corrective action.
    /// </summary>
    public partial class WallHeightDialog : Window
    {
        public bool IsOverride { get; private set; } = false;
        public string OverrideJustification { get; private set; } = string.Empty;

        public WallHeightDialog(List<RuleViolation> violations)
        {
            InitializeComponent();

            // Extract wall name from each violation's ElementDescription.
            // ElementDescription format: "Wall 'NAME' (ID 12345)"
            var wallNames = violations
                .Select(v => ExtractWallName(v.ElementDescription))
                .ToList();

            WallNamesList.ItemsSource = wallNames;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Extracts the wall name from the element description string.
        /// ElementDescription format: "Wall 'NAME' (ID 12345)"
        /// Returns just NAME, falling back to the full string if not parseable.
        /// </summary>
        private string ExtractWallName(string elementDescription)
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
