using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Threading.Tasks;

namespace ExhaleCreativity
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = new HostBuilder()
            .ConfigureAppConfiguration((hostContext, builder) =>
            {
                builder.AddCommandLine(args);

                Console.WriteLine($"ASPNETCORE_ENVIRONMENT: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
                Console.WriteLine($"AZURE_FUNCTIONS_ENVIRONMENT: {Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")}");
                Console.WriteLine($"HostingEnvironment.ENVIRONMENTNAME: {hostContext.HostingEnvironment.EnvironmentName}");

                //if (hostContext.HostingEnvironment.IsDevelopment())
                var environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Development";
                if (environment.Equals("Development"))
                {
                    builder.AddUserSecrets<Program>();
                }
                builder.AddEnvironmentVariables("EXHALESETTINGS_");

                builder.Build();
            })

            // .ConfigureFunctionsWorkerDefaults((ctx, builder) =>
            .ConfigureFunctionsWorkerDefaults(app =>
            {
                // add middleware here
                app.UseNewtonsoftJson();
            })
            .ConfigureServices(services =>
            {
                services.AddHttpClient();
                services.AddOptions<ExhaleOptions>().Configure<IConfiguration>((s, c) => c.GetSection(ExhaleOptions.SecureSettings).Bind(s));

                //TODO: update this to use convention mapping
                services.AddScoped<IExhaleBlobService, ExhaleBlobService>();
                services.AddScoped<IExhaleStripeService, ExhaleStripeService>();
                services.AddScoped<IExhaleSheetsService, ExhaleSheetsService>();
                services.AddScoped<IExhaleIndexService, ExhaleIndexService>();

                // mapping logic
                services.AddAutoMapper(typeof(Program));
            })
            .Build();

            await host.RunAsync();
        }


    }

    internal static class WorkerConfigurationExtensions
    {
        /// <summary>
        /// The functions worker uses the Azure SDK's ObjectSerializer to abstract away all JSON serialization. This allows you to
        /// swap out the default System.Text.Json implementation for the Newtonsoft.Json implementation.
        /// To do so, add the Microsoft.Azure.Core.NewtonsoftJson nuget package and then update the WorkerOptions.Serializer property.
        /// This method updates the Serializer to use Newtonsoft.Json. Call /api/HttpFunction to see the changes.
        /// </summary>
        public static IFunctionsWorkerApplicationBuilder UseNewtonsoftJson(this IFunctionsWorkerApplicationBuilder builder)
        {
            builder.Services.Configure<WorkerOptions>(workerOptions =>
            {
                var settings = NewtonsoftJsonObjectSerializer.CreateJsonSerializerSettings();
                settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                settings.Converters.Add(new StringEnumConverter());
                settings.ObjectCreationHandling = ObjectCreationHandling.Reuse;
                settings.NullValueHandling = NullValueHandling.Include;
                settings.Formatting = Formatting.Indented;
                workerOptions.Serializer = new NewtonsoftJsonObjectSerializer(settings);
            });

            return builder;
        }
    }
}