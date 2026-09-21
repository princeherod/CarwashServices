using System.Collections.Generic;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Static lookup lists for the customer dialog.
    /// Kept in one place so Make/Model/Color lists can be extended without
    /// touching the UI code.
    /// </summary>
    internal static class BrandCatalog
    {
        // ---- Vehicle makes ----
        public static readonly string[] Makes =
        {
            "Toyota", "Honda", "Mitsubishi", "Nissan", "Ford", "Chevrolet",
            "Hyundai", "Kia", "Mazda", "Subaru", "Isuzu", "Suzuki",
            "Volkswagen", "BMW", "Mercedes-Benz", "Audi", "Lexus",
            "Peugeot", "Renault", "Fiat", "Jeep", "Land Rover",
            "Volvo", "Chery", "Geely", "MG", "BYD", "Other"
        };

        // ---- Vehicle body types ----
        public static readonly string[] BodyTypes =
        {
            "", "Sedan", "Hatchback", "SUV", "MPV", "Pickup",
            "Van", "Truck", "Motorcycle", "Other"
        };

        // ---- Colours ----
        public static readonly string[] Colors =
        {
            "", "White", "Black", "Silver", "Gray", "Red", "Blue",
            "Green", "Yellow", "Orange", "Brown", "Beige", "Gold",
            "Maroon", "Purple", "Pink", "Other"
        };

        // ---- Sources ----
        public static readonly string[] Sources =
        {
            "", "Facebook", "Google", "Referral", "Walk-in",
            "Instagram", "TikTok", "Other"
        };

        // ---- Year range (newest → oldest) ----
        public static IEnumerable<int> Years()
        {
            for (int y = 2027; y >= 1970; y--)
                yield return y;
        }
    }
}