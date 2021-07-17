
namespace ExhaleCreativity
{
    public class MemberLink
    {
        public LinkType Type { get; set; }

        public string Url { get; set; }
    }

    public enum LinkType
    {
        Instagram,

        Website,

        Voxer
    }
}