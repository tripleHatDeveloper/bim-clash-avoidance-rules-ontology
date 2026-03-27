using System;
using System.IO;
using System.Text;

namespace ClashAvoidancePlugin.Logging
{
    /// <summary>
    /// Writes rule override events to a CSV file located in the same folder
    /// as the Revit project file.
    ///
    /// CSV columns:
    ///
    ///   Timestamp | UserName | ElementId | ElementDescription |
    ///   RuleId | RuleLabel | ViolationDetail | Justification
    ///
    /// File naming: {ProjectName}_ClashAvoidance_Overrides.csv
    /// If the project has not been saved yet, the log is written to the
    /// plugin directory under "Unsaved_Project_Overrides.csv".
    /// </summary>
    public static class OverrideLogger
    {
        private static readonly string[] CsvHeaders = new[]
        {
            "Timestamp",
            "UserName",
            "ElementId",
            "ElementDescription",
            "RuleId",
            "RuleLabel",
            "ViolationDetail",
            "Justification"
        };

        /// <summary>
        /// Appends one override record to the project's CSV log.
        /// Creates the file (with header row) if it does not exist.
        /// Thread-safe via file append mode (no in-memory state required).
        /// </summary>
        public static void LogOverride(
            string projectPath,
            DateTime timestamp,
            string userName,
            string elementId,
            string elementDesc,
            string ruleId,
            string ruleLabel,
            string violationDetail,
            string justification)
        {
            try
            {
                string logPath = ResolveLogPath(projectPath);
                bool fileExists = File.Exists(logPath);

                using (StreamWriter writer = new StreamWriter(
                    logPath, append: true, encoding: Encoding.UTF8))
                {
                    // Write header on first creation
                    if (!fileExists)
                        writer.WriteLine(string.Join(",", CsvHeaders));

                    writer.WriteLine(FormatCsvRow(
                        timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        userName,
                        elementId,
                        elementDesc,
                        ruleId,
                        ruleLabel,
                        violationDetail,
                        justification));
                }
            }
            catch (Exception ex)
            {
                // Never throw from logging — surface silently
                System.Diagnostics.Debug.WriteLine(
                    $"[ClashAvoidance] OverrideLogger write error: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the full path of the log file for a given project.
        /// </summary>
        public static string ResolveLogPath(string projectPath)
        {
            string directory;
            string projectName;

            if (!string.IsNullOrEmpty(projectPath) && File.Exists(projectPath))
            {
                directory   = Path.GetDirectoryName(projectPath);
                projectName = Path.GetFileNameWithoutExtension(projectPath);
            }
            else
            {
                // Unsaved project — write to plugin directory
                directory   = App.PluginDirectory;
                projectName = "Unsaved_Project";
            }

            return Path.Combine(directory,
                $"{projectName}_ClashAvoidance_Overrides.csv");
        }

        // ------------------------------------------------------------------ //
        //  CSV formatting
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Formats a set of field values as a CSV row.
        /// Fields containing commas, quotes, or newlines are quoted and escaped.
        /// </summary>
        private static string FormatCsvRow(params string[] fields)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsvField(fields[i] ?? string.Empty));
            }
            return sb.ToString();
        }

        private static string EscapeCsvField(string field)
        {
            // RFC 4180: if field contains comma, double-quote, or newline — wrap in quotes
            if (field.Contains(",") || field.Contains("\"") ||
                field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }
    }
}
