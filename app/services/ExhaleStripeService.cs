using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace ExhaleCreativity
{
    public class ExhaleStripeService : IExhaleStripeService
    {
        private readonly ILogger<ExhaleStripeService> _logger;

        public ExhaleStripeService(ILogger<ExhaleStripeService> logger, IOptions<ExhaleOptions> options)
        {
            _logger = logger;
            StripeConfiguration.ApiKey = options.Value.StripeApiKey;
        }

        public async Task<List<ExhaleMember>> GetExhaleMembersAsync()
        {
            _logger.LogInformation("Getting members from Stripe");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            const long StripePageSize = 100;

            // start by adding all stripe customers
            var service = new CustomerService();
            var options = new CustomerListOptions { Limit = StripePageSize };
            options.AddExpand("data.subscriptions");

            var exhaleMembers = new List<ExhaleMember>();

            await foreach (var stripeCustomer in service.ListAutoPagingAsync(options))
            {
                try
                {
                    // Is this an active paying customer with a valid email?
                    if (stripeCustomer.Subscriptions?.Data?.FirstOrDefault()?.Status == Constants.StripeMemberStatus.Active &&
                        !string.IsNullOrEmpty(stripeCustomer.Email))
                    {
                        var stripeCustomerEmail = stripeCustomer.Email.ToLower();
                        var joinedDate = stripeCustomer.Subscriptions.First().StartDate;
                        exhaleMembers.Add(
                                        new ExhaleMember
                                        {
                                            UniqueId = Helpers.GetUniqueMemberId(stripeCustomerEmail),
                                            DisplayName = Helpers.FormatDefaultName(stripeCustomer.Description),
                                            Joined = new DateTime(joinedDate.Year, joinedDate.Month, 1, 0, 0, 0),
                                        }
                                    );
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "stripe member failure; move onto next member");
                }
            }

            stopWatch.Stop();

            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = stopWatch.Elapsed;

            _logger.LogInformation($"Total time taken at {StripePageSize} per page: {ts.GetElapsedTime()}");

            return exhaleMembers;
        }
    }
}