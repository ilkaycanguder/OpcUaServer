using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpcUaServer.Application.Managers
{
    public class UserAccount
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }

    public class UserAccountManager
    {
        private readonly List<UserAccount> _users;

        public UserAccountManager()
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "users.json");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ users.json bulunamadı: {filePath}");
                _users = new List<UserAccount>();
                return;
            }

            string json = File.ReadAllText(filePath);
            _users = JsonConvert.DeserializeObject<List<UserAccount>>(json) ?? new List<UserAccount>();
        }

        public bool ValidateUser(string username, string password)
        {
            return _users.Any(user =>
                string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase) &&
                user.Password == password);
        }

    

        public IEnumerable<UserAccount> GetAllUsers()
        {
            return _users;
        }
    }
}
