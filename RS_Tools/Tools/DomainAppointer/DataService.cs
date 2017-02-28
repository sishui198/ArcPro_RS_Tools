using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Tools.Tools.DomainAppointer
{
    class DataService
    {
        public enum DomainCode
        {
            Code0 = 0,
            Code1,
            Code2,
            Code3,
            Code4,
            Code5,
            Code6,
            Code7,
            Code8,
            Code9
        };

        public delegate void ApplyDomainDelegate(DomainCode code);
    }
}
