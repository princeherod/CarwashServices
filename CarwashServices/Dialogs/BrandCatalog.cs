using System;
using System.Collections.Generic;

namespace CarwashServices.Dialogs
{
    internal static class BrandCatalog
    {
        public static readonly string[] Makes =
        {
            "Toyota", "Honda", "Mitsubishi", "Nissan", "Ford", "Chevrolet",
            "Hyundai", "Kia", "Mazda", "Subaru", "Isuzu", "Suzuki",
            "Volkswagen", "BMW", "Mercedes-Benz", "Audi", "Lexus",
            "Peugeot", "Renault", "Fiat", "Jeep", "Land Rover",
            "Volvo", "Chery", "Geely", "MG", "BYD", "Other"
        };

        public static readonly string[] BodyTypes =
        {
            "", "Sedan", "Hatchback", "SUV", "MPV", "Pickup",
            "Van", "Truck", "Motorcycle", "Other"
        };

        public static readonly string[] Colors =
        {
            "", "White", "Black", "Silver", "Gray", "Red", "Blue",
            "Green", "Yellow", "Orange", "Brown", "Beige", "Gold",
            "Maroon", "Purple", "Pink", "Other"
        };

        public static readonly string[] Sources =
        {
            "", "Facebook", "Google", "Referral", "Walk-in",
            "Instagram", "TikTok", "Other"
        };

        // Newest year offered is the current calendar year — 2026 on a 2026
        // machine, 2027 on a 2027 machine, etc. Nothing is hard-coded.
        public static IEnumerable<int> Years()
        {
            int newest = DateTime.Today.Year;
            for (int y = newest; y >= 1970; y--)
                yield return y;
        }
    }
}