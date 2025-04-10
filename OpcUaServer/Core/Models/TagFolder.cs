using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpcUaServer.Core.Models
{
    public class TagFolder
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
