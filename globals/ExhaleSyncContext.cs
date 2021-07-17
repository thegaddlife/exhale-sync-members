using System.Collections.Generic;

namespace ExhaleCreativity
{
    public class ExhaleSyncContext
    {
        public ExhaleMemberGroup UnknownMemberGroup { get; set; }
        public List<ExhaleMemberGroup> MemberGroups { get; set; }
        public List<ExhaleMember> LastSavedMembersListAsync { get; set; }
        public List<MemberSubmission> LatestMemberSubmissionsAsync { get; set; }
    }
}