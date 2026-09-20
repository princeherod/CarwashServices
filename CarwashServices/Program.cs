using System;
using System.Windows.Forms;

using CarwashServices.Shell;

namespace CarwashServices
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Show login first
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return; // user closed the login form
            }

            // Login successful → open the CRM
            Application.Run(new MainForm());
        }
    }
}