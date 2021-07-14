using System.Collections.Generic;

namespace ExhaleCreativity
{
    public class ExhaleMemberGroup
    {
        public ExhaleMemberGroup(string stateProvince)
        {
            StateProvince = stateProvince;
            Members = new List<ExhaleMember>();
        }

        public string StateProvince { get; set; }

        public List<ExhaleMember> Members { get; set; }

    }
}