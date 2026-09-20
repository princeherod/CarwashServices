using CarwashServices.Dtos;
namespace CarwashServices.Dtos
{
    // ============================================================
    // User DTOs
    //   UserDto      – compact list row (used by combo boxes)
    //   AuthUserDto  – response from POST api/auth/login
    // ============================================================

    public class UserDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public int RoleId { get; set; }
    }

    public class AuthUserDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public int RoleId { get; set; }
    }
}