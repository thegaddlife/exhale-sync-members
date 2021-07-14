using System.Collections.Generic;

namespace ExhaleCreativity
{
    internal struct Constants
    {
        internal struct StripeMemberStatus
        {
            internal const string Active = "active";
        }

        internal struct Tags
        {
            internal const string Newbie = "newbie";
            internal const string Team = "team";
            internal const string Founder = "founder";
        }

        internal const string LaunchDateAbbrev = "Mar, 18";
        internal const string UnknownMemberGroup = "zzz-unknown";
        internal const string MembersContainerName = "members-json";
        internal const string BlobGroupedName = "exhale-members.json";
        internal const string BlobSortedName = "exhale-members-sorted.json";
        internal const string BlobMapName = "exhale-members-map.json";
        internal const string BlobBadgeName = "exhale-members-badged.json";

        internal const string MainSheet = "Master";
        internal const string ExemptSheet = "Exempt";

        internal const int NewbieDays = 180;

        internal const string IndexName = "exhale_members";
    }
}