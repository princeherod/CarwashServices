using System.Collections.Generic;

namespace CRM.domain.Entities
{
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } // Admin, Super Admin, Manager, Service Staff

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}