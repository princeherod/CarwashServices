namespace CarwashServices.Controls.Reports
{
    /// <summary>
    /// Implemented by every sub-report UserControl so the ReportsView shell
    /// can push filter values into it and ask it to reload.
    /// </summary>
    public interface IReportView
    {
        void ApplyFilters(string dateRange, string service, string vehicleType);
    }
}