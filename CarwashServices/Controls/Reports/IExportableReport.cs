using System.Drawing.Printing;

namespace CarwashServices.Controls.Reports
{
    /// <summary>
    /// Implemented by every sub-report UserControl so the ReportsView shell can
    /// export whatever the sub-view is currently showing.
    /// </summary>
    public interface IExportableReport
    {
        /// <summary>True when there is at least one row to export.</summary>
        bool HasData { get; }

        /// <summary>Builds a CSV string (including the header row) for the current data.</summary>
        string BuildCsv();

        /// <summary>Builds a PrintDocument for PDF/preview printing.</summary>
        PrintDocument BuildPrintDocument();
    }
}