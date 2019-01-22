using System;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;

namespace consumer
{
    class Program
    {

        private const string EventHubConnectionString = "<Your Event Hub Connection String>";
        private const string EventHubName = "baseball-events";
        private const string StorageContainerName = "simple-messages";
        private const string StorageAccountName = "<Storage Account Name>";
        private const string StorageAccountKey = "<Storage Account Key>";

        private static readonly string StorageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", StorageAccountName, StorageAccountKey);

        static void Main(string[] args)
        {
            TelemetryConfiguration config = TelemetryConfiguration.Active;
            config.InstrumentationKey = "<App Insights Instrumentation Key>";
            config.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());

            InitializeDependencyTracking(config);

            TelemetryWrapper.Client = new TelemetryClient(config);

            MainAsync(args).GetAwaiter().GetResult();
        }


        private static async Task MainAsync(string[] args)
        {
            TelemetryWrapper.Client.TrackTrace("Registering EventProcessor...");
            Console.WriteLine("Registering EventProcessor...");

            var eventProcessorHost = new EventProcessorHost(
                EventHubName,
                "dotnetcore",
                //PartitionReceiver.DefaultConsumerGroupName,
                EventHubConnectionString,
                StorageConnectionString,
                StorageContainerName);

            // Registers the Event Processor Host and starts receiving messages
            await eventProcessorHost.RegisterEventProcessorAsync<SimpleEventProcessor>();

            TelemetryWrapper.Client.TrackTrace("Receiving. Press ENTER to stop worker.");
            Console.WriteLine("Receiving. Press ENTER to stop worker.");

            TelemetryWrapper.Client.Flush();

            while (true)
            {
                //loop until broken
                var ticks = DateTime.Now.Ticks;
                if (ticks % 500 == 0)
                {
                    TelemetryWrapper.Client.Flush();
                }
            }

            // Disposes of the Event Processor Host
            await eventProcessorHost.UnregisterEventProcessorAsync();
        }


        static DependencyTrackingTelemetryModule InitializeDependencyTracking(TelemetryConfiguration configuration)
        {
            var module = new DependencyTrackingTelemetryModule();

            // prevent Correlation Id to be sent to certain endpoints. You may add other domains as needed.
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.windows.net");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.chinacloudapi.cn");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.cloudapi.de");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.usgovcloudapi.net");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("localhost");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("127.0.0.1");

            // enable known dependency tracking, note that in future versions, we will extend this list. 
            // please check default settings in https://github.com/Microsoft/ApplicationInsights-dotnet-server/blob/develop/Src/DependencyCollector/NuGet/ApplicationInsights.config.install.xdt#L20
            module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.ServiceBus");
            module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");

            // initialize the module
            module.Initialize(configuration);

            return module;
        }

    }
}
