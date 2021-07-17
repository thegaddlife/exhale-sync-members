using System.Collections.Generic;

namespace ExhaleCreativity
{
    public class ExhaleMemberComparer : IEqualityComparer<ExhaleMember>
    {
        public bool Equals(ExhaleMember item1, ExhaleMember item2)
        {
            return item1.UniqueId == item2.UniqueId;
        }

        public int GetHashCode(ExhaleMember item)
        {
            int hCode = item.UniqueId.Length;
            return hCode.GetHashCode();
        }
    }
}