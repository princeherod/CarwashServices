using System.Windows.Forms;

namespace CarwashServices.Dialogs
{
    internal static class ArchiveConfirmDialog
    {
        public static bool ConfirmArchive(string recordLabel)
        {
            var result = MessageBox.Show(
                "Archive Record?\n\n" +
                $"This {recordLabel} will be moved to Archived Records and will no longer appear in the active list.",
                "Archive Record",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            return result == DialogResult.OK;
        }

        public static bool ConfirmRestore(string recordLabel)
        {
            var result = MessageBox.Show(
                "Restore Record?\n\n" +
                $"This {recordLabel} will be returned to the active list.",
                "Restore Record",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            return result == DialogResult.OK;
        }
    }
}