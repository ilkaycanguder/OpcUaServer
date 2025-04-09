using System.Collections.Generic;

namespace OpcUaServer
{
    public enum UserRole
    {
        Admin,
        Operator,
        Guest
    }

    public class UserRoleManager
    {
        private readonly Dictionary<string, UserRole> _userRoles = new Dictionary<string, UserRole>();
        private readonly Dictionary<UserRole, List<string>> _rolePermissions = new Dictionary<UserRole, List<string>>();

        public UserRoleManager()
        {
            // Kullanıcı rollerini tanımla
            _userRoles.Add("admin", UserRole.Admin);
            _userRoles.Add("operator", UserRole.Operator);
            _userRoles.Add("guest", UserRole.Guest);

            // Rol bazlı izinleri tanımla
            _rolePermissions.Add(UserRole.Admin, new List<string> { "*" }); // Tüm erişim
            _rolePermissions.Add(UserRole.Operator, new List<string>
            {
                "ayd_auto_mode", "ayd_status1", "ayd_setman1", "ayd_setauto2"
            });
            _rolePermissions.Add(UserRole.Guest, new List<string>
            {
                "ayd_status1", "ayd_status2" // Sadece durum bilgilerini görüntüleme
            });
        }

        public UserRole GetUserRole(string username)
        {
            if (_userRoles.TryGetValue(username, out UserRole role))
            {
                return role;
            }

            // Varsayılan olarak Guest rol döndür
            return UserRole.Guest;
        }

        public bool HasPermission(string username, string tagName)
        {
            UserRole role = GetUserRole(username);

            if (_rolePermissions.TryGetValue(role, out List<string> permissions))
            {
                // "*" tüm izinlere sahip olduğunu gösterir
                if (permissions.Contains("*"))
                    return true;

                return permissions.Contains(tagName);
            }

            return false;
        }
    }
}