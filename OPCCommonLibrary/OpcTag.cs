using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCCommonLibrary
{
    public class OpcTag
    {
        public int Id { get; set; }
        public string TagName { get; set; }
        public int TagValue { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.Now;
    }

}
