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

            // Run the login/main loop until the user closes the app.
            // Using a loop instead of Application.Restart() removes the
            // tear-down + recreate flicker on successful login and after
            // a sign-out.
            while (true)
            {
                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                        return;   // user closed the login form
                }

                // Login succeeded — open the CRM. When the user clicks Sign Out
                // inside the CRM, SignOutRequested flips to true and MainForm
                // closes itself so we can loop back to the login screen.
                var main = new MainForm();
                Application.Run(main);

                if (!main.SignOutRequested)
                    return;   // app closed via the X button
            }
        }
    }
}