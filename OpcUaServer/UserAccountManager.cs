using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpcUaServer
{
    public class UserAccountManager
    {
        private readonly Dictionary<string, string> _userAccounts = new Dictionary<string, string>();

        public UserAccountManager()
        {
            _userAccounts.Add("admin", "admin123");
            _userAccounts.Add("operator", "op123");
            _userAccounts.Add("guest", "guest123");
        }

        public bool ValidateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            if (_userAccounts.TryGetValue(username, out string storedPassword))
            {
                return storedPassword == password;
            }

            return false;
        }
    }
}
