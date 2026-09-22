namespace CarwashServices.Controls.Reports
{
    /// <summary>
    /// Implemented by every sub-report UserControl so the ReportsView shell
    /// can push filter values into it, ask it to reload, and switch between
    /// the chart-only / table-only / chart-and-table view modes.
    /// </summary>
    public interface IReportView
    {
        void ApplyFilters(string dateRange, string service, string vehicleType);

        /// <summary>Show KPI row + charts, hide the table.</summary>
        void ShowChartOnly();

        /// <summary>Show KPI row + table, hide the charts.</summary>
        void ShowTableOnly();

        /// <summary>Show KPI row + charts + table (default).</summary>
        void ShowChartAndTable();
    }
}