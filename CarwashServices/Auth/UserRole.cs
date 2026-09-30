namespace CarwashServices.Auth
{
    // Role IDs aligned with system specifications:
    // 1 = SuperAdmin, 2 = Admin, 3 = Manager, 4 = ServiceStaff
    public enum UserRole
    {
        Unknown = 0,
        SuperAdmin = 1,
        Admin = 2,
        Manager = 3,
        ServiceStaff = 4
    }
}