using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpcUaServer.Core.Models
{
    public class TagDefinition
    {
        public string TagName { get; set; }
        public int InitialValue { get; set; }
    }
}
