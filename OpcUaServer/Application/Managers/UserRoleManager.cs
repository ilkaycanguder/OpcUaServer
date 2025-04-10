using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpcUaServer.Application.Managers
{
    public enum UserRole
    {
        Admin,
        Guest
    }

    public class UserRoleManager
    {
        private readonly Dictionary<UserRole, List<string>> _rolePermissions = new();
        private readonly List<UserAccount> _users;

        public UserRoleManager(UserAccountManager userAccountManager)
        {
            _users = userAccountManager.GetAllUsers().ToList();
            LoadPermissionsFromJson();
        }

        private void LoadPermissionsFromJson()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "roles.json");

                if (!File.Exists(path))
                {
                    Console.WriteLine($"❌ roles.json bulunamadı: {path}");
                    return;
                }

                string json = File.ReadAllText(path);
                var permissions = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

                foreach (var entry in permissions)
                {
                    if (Enum.TryParse(entry.Key, out UserRole role))
                    {
                        _rolePermissions[role] = entry.Value;
                    }
                }

                Console.WriteLine("✅ Rol izinleri başarıyla yüklendi (roles.json)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Rol izinleri yüklenirken hata oluştu: {ex.Message}");
            }
        }
        public UserRole GetUserRole(string username)
        {
            var roleStr = _users
                .FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase))?.Role;

            if (Enum.TryParse(roleStr, true, out UserRole role))
                return role;

            return UserRole.Guest; // fallback
        }
        public bool HasPermission(UserRole role, string tagName)
        {
            if (_rolePermissions.TryGetValue(role, out List<string> permissions))
            {
                return permissions.Contains("*") || permissions.Contains(tagName);
            }

            return false;
        }

        public List<string> GetAllowedTags(UserRole role)
        {
            if (_rolePermissions.TryGetValue(role, out var tags))
                return tags.Contains("*") ? new List<string> { "*" } : new List<string>(tags);

            return new();
        }
    }
}
