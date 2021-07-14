// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AutoMapper;
using Geocoding;
using Geocoding.Google;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Address = Geocoding.Address;

namespace ExhaleCreativity
{
    public class SyncMembers
    {
        readonly ILogger<SyncMembers> _logger;
        readonly IHttpClientFactory _clientFactory;
        readonly IMapper _mapper;
        readonly IExhaleBlobService _exhaleBlobService;
        readonly IExhaleStripeService _exhaleStripeService;
        readonly IExhaleSheetsService _exhaleSheetsService;
        // readonly IExhaleIndexService _exhaleIndexService;
        readonly ExhaleOptions _secureSettings;
        ExhaleSyncContext ExhaleSyncCtx { get; set; }

        public SyncMembers(
            ILogger<SyncMembers> logger, IOptions<ExhaleOptions> options,
            IHttpClientFactory clientFactory, IMapper mapper,
            IExhaleBlobService exhaleBlobService, IExhaleStripeService exhaleStripeService,
            IExhaleSheetsService exhaleSheetsService) //, IExhaleIndexService exhaleIndexService)
        {
            _secureSettings = options.Value;
            _logger = logger;
            _clientFactory = clientFactory;
            _mapper = mapper;
            _exhaleBlobService = exhaleBlobService;
            _exhaleStripeService = exhaleStripeService;
            _exhaleSheetsService = exhaleSheetsService;
            // _exhaleIndexService = exhaleIndexService;
        }

        [Function(nameof(SyncMembers))]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("SyncMembers function is running");

                // set up all of our instance ctx props so we dont just pass things around
                await InitExhaleSync();

                // now create the member groups with only valid members
                await SyncExhaleMemberGroups();

                // save those members back to blob storage
                await CreateBlobAsync();

                var latestGroups = ExhaleSyncCtx.MemberGroups;

                // Commented for now: not sure we benefit much here because we have use Next SSG
                // update our search index
                // await _exhaleIndexService.UpdateSearchIndexAsync(Constants.IndexName,
                //     ExhaleSyncCtx.MemberGroups.SelectMany(x => x.Members).OrderBy(x => x.DisplayName).ToList());

                // respond with grouped members
                var okResponse = req.CreateResponse(HttpStatusCode.OK);
                await okResponse.WriteAsJsonAsync(ExhaleSyncCtx.MemberGroups);
                _logger.LogInformation("SyncMembers function has completed successfully");

                return okResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError("SyncMembers failed", ex);

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error: " + ex.Message);

                return errorResponse;
            }
        }

        private async Task InitExhaleSync()
        {
            // go and grab the last JSON that we stored in a blob from prior sync
            var existingGroups = await _exhaleBlobService.GetBlobAsListAsync<ExhaleMemberGroup>(Constants.BlobGroupedName)
                .ConfigureAwait(false);
            var lastSavedMembersListAsync = existingGroups.SelectMany(x => x.Members).ToList();

            // pull together all of the member submissions we have on file
            var latestMemberSubmissionsAsync = await GetLatestExhaleMemberSubmissionsAsync();

            // set up our member groups which is where the members will be added
            var unknownMemberGroup = new ExhaleMemberGroup(Constants.UnknownMemberGroup);

            var memberGroups = new List<ExhaleMemberGroup> {
                unknownMemberGroup
            };

            // new up the sync context which helps orchestrate the entire sync
            ExhaleSyncCtx = new ExhaleSyncContext
            {
                LastSavedMembersListAsync = lastSavedMembersListAsync,
                LatestMemberSubmissionsAsync = latestMemberSubmissionsAsync,
                MemberGroups = memberGroups,
                UnknownMemberGroup = unknownMemberGroup
            };
        }

        private async Task SyncExhaleMemberGroups()
        {
            _logger.LogInformation("GetGroupedMembersAsync function is running");

            // first go grab all valid members from stripe and exempt list
            var exhaleMembers = await GetStripeAndExemptCustomersAsync();

            _logger.LogInformation($"Processing {exhaleMembers.Count} members");
            var remaining = exhaleMembers.Count;
            _logger.LogInformation($"{remaining} remaining");

            foreach (var newMember in exhaleMembers.OrderBy(x => x.DisplayName))
            {
                await ProcessMember(newMember);
                _logger.LogInformation($"{remaining--} remaining");
            }

            _logger.LogInformation("GetGroupedMembersAsync function has completed");
        }

        private async Task ProcessMember(ExhaleMember newMember)
        {
            _logger.LogInformation($"Processing member {newMember.UniqueId}");

            // a valid member directory entry must be:
            // a stripe customer with no form submission OR
            // a stripe customer where latest entry is not opted out AND
            // On the master sheet and an active customer in Stripe OR
            // On the email exept list sheet

            // get their latest submission
            var submission = ExhaleSyncCtx.LatestMemberSubmissionsAsync
                .Where(x => Helpers.GetUniqueMemberId(x.Email.ToLower()) == newMember.UniqueId)
                .OrderBy(x => x.Timestamp)
                .LastOrDefault();

            // look for the last saved record of this member in the blob using the encrypted member email
            var lastSavedRecord = ExhaleSyncCtx.LastSavedMembersListAsync
                .FirstOrDefault(x => !string.IsNullOrEmpty(x.UniqueId) && x.UniqueId == newMember.UniqueId);

            if (submission != null)
            {
                // if this member has submitted a response, we can setup their profile;
                // otherwise, they will just be listed with their name and a default photo

                // since the directory is opt-out, check if they submitted and opted out before going further
                if (!submission.BeListed)
                    return;

                // title, blurb, city, stateProvince, country, facebook, twitter, insta, website, voxer
                // Use automapper to take any matching properties off of the submission and map them onto the member object;
                // this helps flatten our object model when transferred back to the client
                _mapper.Map(submission, newMember);

                // comment for now; can we just use all caps? or all lower?
                //newMember.DisplayName = Helpers.EnglishTextInfo.ToTitleCase(submission.DisplayName);

                // generate memberlinks
                newMember.Links = GenerateMemberLinks(submission);

                // setup their gravatar profile if neccessary
                await SetupGravatarAsync(newMember, submission.UseGravatar, lastSavedRecord?.GravatarConfirmed);
            }

            // get member tags
            newMember.Tags = GetMemberTags(newMember);

            // add member to the located group and geocode their location if neccessary
            await GroupAndGeocodeMember(newMember, lastSavedRecord);
        }

        private List<MemberLink> GenerateMemberLinks(MemberSubmission submission)
        {
            var links = new List<MemberLink>();

            if (!string.IsNullOrEmpty(submission.Insta))
            {
                links.Add(new MemberLink { Type = LinkType.Instagram, Url = submission.Insta });
            }

            if (!string.IsNullOrEmpty(submission.Website))
            {
                links.Add(new MemberLink { Type = LinkType.Website, Url = submission.Website });
            }

            if (!string.IsNullOrEmpty(submission.VoxerName))
            {
                links.Add(new MemberLink { Type = LinkType.Voxer, Url = submission.VoxerName });
            }

            return links;
        }

        private async Task SetupGravatarAsync(ExhaleMember exhaleMember, bool useGravatar, bool? gravatarConfirmed)
        {
            // other possible options:
            // https://getavataaars.com/
            // Bitmoji
            // Apple memoji

            // if they opted in to Gravatar and we havent verified their gravatar yet
            // then we'll verify their gravatar membership

            if (!useGravatar)
            {
                // reset this field
                exhaleMember.GravatarConfirmed = false;
                return;
            }

            if (gravatarConfirmed.GetValueOrDefault())
            {
                // keep this field stored in new record
                exhaleMember.GravatarConfirmed = true;
                return;
            }

            // Here we have a member who wants to use gravatar but we have yet to
            // confirm that they have a gravatar profile

            var gravatarClient = _clientFactory.CreateClient("gravatar");
            var requestUri = $"https://www.gravatar.com/{exhaleMember.UniqueId}.json";
            // need to give user agent otherwise gravatar wont allow it
            var productValue = new ProductInfoHeaderValue("AzureMembersSync", "1.0");

            gravatarClient.DefaultRequestHeaders.UserAgent.Add(productValue);

            var response = await gravatarClient.GetAsync(requestUri);
            if (response.IsSuccessStatusCode)
            {
                //var profile = GravatarProfile.FromJson(response.ToJSON());
                exhaleMember.GravatarConfirmed = true;
            }
            else
            {
                _logger.LogWarning("Unable to retrieve gravatar profile for {0};Status={1};Message={2}",
                exhaleMember.UniqueId, response.StatusCode, response.ReasonPhrase);
            }

        }

        private async Task GroupAndGeocodeMember(ExhaleMember newMember, ExhaleMember lastSavedRecord)
        {

            // assign group and geocoding
            ExhaleMemberGroup memberGroup = null;

            // grouping and geocoding
            if (!string.IsNullOrWhiteSpace(newMember.StateProvince))
            {
                // see if we can/should geocode this
                _logger.LogInformation($"Checking geocode for member");

                await CheckGeoCodeAsync(newMember, lastSavedRecord);

                var stateProvince = newMember.StateProvince.Trim().ToLower();

                // find a matching state from the group if its exists; otherwise add it
                memberGroup = ExhaleSyncCtx.MemberGroups.Find(x => x.StateProvince.Trim().ToLower() == stateProvince);
                if (memberGroup == null)
                {
                    // group doesnt exist yet in our list; add it here
                    memberGroup = new ExhaleMemberGroup(Helpers.EnglishTextInfo.ToTitleCase(stateProvince));
                    ExhaleSyncCtx.MemberGroups.Add(memberGroup);
                }
            }
            else
            {
                // set their group as the "unknown" member group
                memberGroup = ExhaleSyncCtx.UnknownMemberGroup;
            }

            memberGroup.Members.Add(newMember);
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="member"></param>
        /// <param name="previousSubmission"></param>
        /// <returns></returns>
        private async Task CheckGeoCodeAsync(ExhaleMember newMemberRecord, ExhaleMember lastSavedRecord)
        {
            //TODO: Update this to geocode all members at once or in batches

            try
            {
                // if this is a new member submission OR
                // if the user doesnt have geocode OR
                // if we have a city and it has changed, geocode it

                // its possible that the member submitted their state but not their city; we only geocode if city is present
                if (string.IsNullOrEmpty(newMemberRecord.City))
                    return;

                // see if the submitted city,state,country has changed;
                // also check if the member didnt have a lat/lng previously
                if (lastSavedRecord != null &&
                string.Compare(lastSavedRecord.LocationCompareString, newMemberRecord.LocationCompareString, ignoreCase: true) == 0 &&
                lastSavedRecord.Lat.GetValueOrDefault() != 0 && lastSavedRecord.Lng.GetValueOrDefault() != 0)
                {
                    // set the previous Lat and Lng
                    newMemberRecord.Lat = lastSavedRecord.Lat;
                    newMemberRecord.Lng = lastSavedRecord.Lng;
                    return;
                }

                _logger.LogInformation($"Geocoding member");
                // geocode the request and get the lat lng
                IGeocoder geocoder = new GoogleGeocoder() { ApiKey = _secureSettings.GoogleApiKey };
                IEnumerable<Address> addresses = await geocoder.GeocodeAsync(newMemberRecord.LocationCompareString);

                if (addresses?.Count() > 0)
                {
                    // now set new lat long
                    newMemberRecord.Lat = addresses.First().Coordinates.Latitude;
                    newMemberRecord.Lng = addresses.First().Coordinates.Longitude;
                }
            }
            catch (Exception ex)
            {
                // todo log and continue
                _logger.LogError(ex, "Error processing GeoCodes");
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private async Task<List<MemberSubmission>> GetLatestExhaleMemberSubmissionsAsync()
        {
            _logger.LogInformation("Collecting Prior Member Submissions");

            var submissionsResponse = await _exhaleSheetsService.GetSheetDataAsync<ExhaleMemberMasterList>(_secureSettings.FormSheetId);

            var exhaleMemberSubmissions = submissionsResponse.Values
                    .Select(x =>
                    {
                        try
                        {
                            var memberValues = ((Newtonsoft.Json.Linq.JArray)x).Values<string>().ToArray();

                            // for google sheets api, the ending columns with no values will not be returned
                            if (memberValues[0] == "Timestamp")
                                return null;

                            return new MemberSubmission
                            {
                                Timestamp = DateTime.ParseExact(memberValues[0], "M/d/yyyy H:mm:ss", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal),
                                Email = memberValues[1],
                                BeListed = memberValues[2].ToLower().StartsWith("yes"),
                                DisplayName = memberValues.ElementAtOrDefault(3),
                                Title = memberValues.ElementAtOrDefault(4),
                                Blurb = memberValues.ElementAtOrDefault(5),
                                Insta = memberValues.ElementAtOrDefault(6),
                                Website = memberValues.ElementAtOrDefault(7),
                                City = memberValues.ElementAtOrDefault(9),
                                StateProvince = memberValues.ElementAtOrDefault(10),
                                Country = memberValues.ElementAtOrDefault(11),
                                VoxerName = memberValues.ElementAtOrDefault(12),
                                UseGravatar = (memberValues.ElementAtOrDefault(13) ?? "no").ToLower().StartsWith("yes")
                            };
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Cant parse the submission response");
                        }
                        return null;
                    })
                    .Where(x => x != null)
                    .ToList();

            return exhaleMemberSubmissions;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private async Task<List<ExhaleMember>> GetStripeAndExemptCustomersAsync()
        {
            _logger.LogInformation("GetStripeAndExemptCustomersAsync is running");

            var stripeMembers = await _exhaleStripeService.GetExhaleMembersAsync();
            var exemptMembers = await GetExemptMembersAsync();

            var exhaleMembers = new List<ExhaleMember>();
            // de-dupe anybody who may have been on both lists
            exhaleMembers.AddRange(stripeMembers.Union(exemptMembers, new ExhaleMemberComparer()));

            return exhaleMembers;
        }

        private async Task<List<ExhaleMember>> GetExemptMembersAsync()
        {
            // now look for exempt customers who are not in stripe already
            var exemptList = await GetExhaleExemptListAsync();

            var exhaleMembers = new List<ExhaleMember>();

            foreach (var exemptCustomer in exemptList)
            {
                var exemptEmailCleaned = exemptCustomer[0].Trim().ToLower();

                exhaleMembers.Add(
                new ExhaleMember
                {
                    UniqueId = Helpers.GetUniqueMemberId(exemptEmailCleaned),
                    DisplayName = exemptCustomer[1],
                    Joined = DateTime.Parse(exemptCustomer[2]),
                    IsTeamMember = exemptCustomer[3].ToUpper() == "TRUE",
                }
            );
            }

            return exhaleMembers;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private async Task<List<string[]>> GetExhaleExemptListAsync()
        {
            var exemptList = await _exhaleSheetsService.GetSheetDataAsync<ExhaleMemberMasterList>(
                _secureSettings.FormSheetId, Constants.ExemptSheet
            );

            var members = new List<string[]>();
            foreach (var value in exemptList.Values)
            {
                try
                {
                    // 0 Email, 1 DisplayName, 2 Joined String, 3 IsTeamMember
                    var memberValues = ((Newtonsoft.Json.Linq.JArray)value).Values<string>().ToArray();
                    members.Add(new string[] { memberValues[0], memberValues[1], memberValues[2], memberValues[3] });
                }
                catch (Exception ex)
                {
                    // failed to get exempt member;
                    // log and continue
                    _logger.LogError(ex, $"Error processing exempt member {value}");
                }
            }

            return members;
        }

        private static IEnumerable<string> GetMemberTags(ExhaleMember member)
        {
            var tags = new List<string>();

            // CC Team
            if (member.IsTeamMember)
            {
                tags.Add(Constants.Tags.Team);
            }

            // founding member
            if (member.IsOriginalMember)
            {
                tags.Add(Constants.Tags.Founder);
            }

            if (member.IsNewbie)
            {
                tags.Add(Constants.Tags.Newbie);
            }

            // TODO: Group 2 - would need to pull this in from a metadata google sheet
            // because Stripe doesn't really tell us anything else;
            // We could also gather it from the member somehow creatively

            return tags;
        }

        /// <summary>
        /// If membersData is null, get the latest members list from storage and deserialize and return;
        /// If it is not null, store in the various shapes (sorted, grouped, map, badged)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task CreateBlobAsync()
        {
            _logger.LogInformation("Processing blobs");

            // order up the groups for cleaner storage
            var orderedGroups = ExhaleSyncCtx.MemberGroups.OrderBy(x => x.StateProvince).ToList();

            // 1) store members grouped by region
            _logger.LogInformation("Uploading grouped members json");
            await _exhaleBlobService.UploadAsync(Constants.BlobGroupedName, orderedGroups);

            // // 2) save members sorted alpha
            // List<ExhaleMember> membersSortedData = await SaveSortedMembersBlob(membersData);

            // // 3) save map json grouped by coordinates
            // await SaveMappedMembersBlob(membersSortedData);

            // // 4) save members json grouped by badge; exlude non badged members
            // await SaveBadgedMembersBlob(membersSortedData);
        }

        #region Additional old blob code if we want it back
        /*
                private async Task SaveBadgedMembersBlob(List<ExhaleMember> membersSortedData)
                {
                    var badgedBlobClient = containerClient.GetBlobClient(Constants.BlobBadgeName);

                    var membersBadgedData = membersSortedData
                        .Where(x => x.Badges.Any())
                        .GroupBy(x => x.Badges.First().Text)
                        .Select(x => new MemberBadgeGroup
                        {
                            OrderNumber = Constants.BadgedOrder[x.First().Badges.First().Text],
                            BadgeGroupName = $"{x.First().Badges.First().Text}s",
                            Members = x.ToList()
                        })
                        .OrderBy(x => x.OrderNumber)
                        .ToList();

                    _log.LogInformation("Uploading badged members json");
                    var membersBadgedJson = JsonConvert.SerializeObject(membersBadgedData);
                    using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(membersBadgedJson)))
                    {
                        await badgedBlobClient.UploadAsync(stream, overwrite: true);
                    }
                }

                private async Task SaveMappedMembersBlob(BlobContainerClient containerClient, List<ExhaleMember> membersSortedData)
                {
                    var mappedBlobClient = containerClient.GetBlobClient(Constants.BlobMapName);

                    var mapMarkersData = membersSortedData
                        .Where(x => x.Lat.HasValue && x.Lng.HasValue)
                        .GroupBy(x => x.LatLng)
                        .Select(x => new MapMarker
                        {

                            Lat = x.First().Lat.Value,
                            Lng = x.First().Lng.Value,
                            City = x.First().Submission?.City,
                            Members = string.Join("|", x.Select(y => y.DisplayName))

                        })
                        .ToList();

                    _log.LogInformation("Uploading mapped members json");
                    var memberMapJson = JsonConvert.SerializeObject(mapMarkersData);
                    using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(memberMapJson)))
                    {
                        await mappedBlobClient.UploadAsync(stream, overwrite: true);
                    }
                }

                private async Task<List<ExhaleMember>> SaveSortedMembersBlob(List<ExhaleMemberGroup> membersData, BlobContainerClient containerClient)
                {
                    var sortedBlobClient = containerClient.GetBlobClient(Constants.BlobSortedName);
                    var membersSortedData = membersData.SelectMany(x => x.Members).OrderBy(x => x.DisplayName).ToList();
                    var memberSortedJson = JsonConvert.SerializeObject(membersSortedData);

                    _log.LogInformation("Uploading sorted members json");
                    using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(memberSortedJson)))
                    {
                        await sortedBlobClient.UploadAsync(stream, overwrite: true);
                    }

                    return membersSortedData;
                }
        */
        #endregion
    }


}