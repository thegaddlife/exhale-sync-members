using AutoMapper;

namespace ExhaleCreativity
{
    public class ExhaleMemberProfile : Profile
    {
        public ExhaleMemberProfile()
        {
            // map from a submission to an exhale member;
            // ForAllMembers allows us to merge fields to an existing member object
            CreateMap<MemberSubmission, ExhaleMember>()
              .ForAllMembers(o => o.Condition((source, destination, member) => member != null));
        }
    }

}