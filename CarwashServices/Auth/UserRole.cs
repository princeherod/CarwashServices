namespace CarwashServices.Auth
{
    // Role IDs aligned with system specifications:
    // 1 = Admin, 2 = Manager, 3 = Staff/ServiceStaff, 4 = SuperAdmin
    public enum UserRole
    {
        Unknown = 0,
        Admin = 1,
        Manager = 2,
        ServiceStaff = 3,
        SuperAdmin = 4
    }
}