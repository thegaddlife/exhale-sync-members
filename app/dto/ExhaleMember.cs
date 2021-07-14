using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ExhaleCreativity
{
    public class ExhaleMember
    {
        public string UniqueId { get; set; }

        // for Algolia indexing
        public string ObjectID => UniqueId;

        // Properties mappable from the Submission object

        public string DisplayName { get; set; }

        public string Title { get; set; }

        public string Blurb { get; set; }

        public string City { get; set; }

        public string StateProvince { get; set; }

        public string Country { get; set; }


        // Other properties useful to the sync function

        public bool GravatarConfirmed { get; set; }

        [JsonIgnore]
        public DateTime? Joined { get; set; }

        public string JoinedString => Joined.ToJoinedString();

        public double? Lat { get; set; }

        public double? Lng { get; set; }

        public string LatLng => $"{Lat.GetValueOrDefault()},{Lng.GetValueOrDefault()}";

        public bool IsTeamMember { get; set; }

        public bool IsOriginalMember => JoinedString == Constants.LaunchDateAbbrev;

        public int AnniversaryCount => Joined.GetValueOrDefault().ToYearsSince();

        public IEnumerable<string> Tags { get; set; }

        public IEnumerable<MemberLink> Links { get; set; }

        public string LocationCompareString => $"{City?.Trim()},{StateProvince?.Trim()},{Country?.Trim()}";

        public bool IsNewbie => Joined.GetValueOrDefault().ToDaysSince() <= Constants.NewbieDays;
    }
}