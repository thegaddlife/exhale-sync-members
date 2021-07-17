using System;
using Newtonsoft.Json;

namespace ExhaleCreativity
{
    public class MemberSubmission
    {
        // Properties mappable onto the Member object
        public string DisplayName { get; set; }

        public string Title { get; set; }

        public string Blurb { get; set; }

        public string City { get; set; }

        public string StateProvince { get; set; }

        public string Country { get; set; }


        // Other properties useful to the sync function
        public string Insta { get; set; }

        public string Website { get; set; }

        public string VoxerName { get; set; }

        public DateTime Timestamp { get; set; }

        public string Email { get; set; }

        public bool BeListed { get; set; }

        public bool UseGravatar { get; set; }
    }
}