namespace CarwashServices.Auth
{
    // Keep these values in sync with the Roles table in the master DB.
    public enum UserRole
    {
        Unknown = 0,
        SuperAdmin = 1,
        Admin = 2,
        Manager = 3,
        ServiceStaff = 4
    }
}