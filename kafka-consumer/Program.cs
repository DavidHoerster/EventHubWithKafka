using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Serialization;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;

namespace kafka_consumer
{
    class Program
    {
        static void Main(string[] args)
        {
            TelemetryConfiguration config = TelemetryConfiguration.Active;
            config.InstrumentationKey = "<App Insights Instrumentation Key>>";
            config.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());

            var conf = new Dictionary<string, object>{
                { "group.id", "kafka" },
                { "bootstrap.servers", "<Your Event Hub Namespace>:9093" },
                { "security.protocol","SASL_SSL" },
                { "sasl.mechanism","PLAIN" },
                { "sasl.username", "$ConnectionString" },
                { "sasl.password", "<Your Event Hub Connection String>" },
                { "ssl.ca.location", "./cacert.pem" },
                { "auto.commit.interval.ms", 5000 },
                { "broker.version.fallback", "1.0.0" },
                { "request.timeout.ms", 60000 },
                { "auto.offset.reset", "earliest" }
            };

            using (InitializeDependencyTracking(config))
            using (var consumer = new Consumer<Null, string>(conf, null, new StringDeserializer(Encoding.UTF8)))
            {
                var telemetryClient = new TelemetryClient(config);

                consumer.OnMessage += (_, msg)
                    =>
                {
                    Console.WriteLine($"Read '{msg.Value}' from: {msg.TopicPartitionOffset}");
                    telemetryClient.TrackTrace($"Read '{msg.Value}'");
                };

                consumer.OnError += (_, error)
                    =>
                {
                    Console.WriteLine($"Error: {error}");
                    telemetryClient.TrackTrace($"Error: {error}");
                };

                consumer.OnConsumeError += (_, msg)
                    =>
                {
                    Console.WriteLine($"Consume error ({msg.TopicPartitionOffset}): {msg.Error}");
                    telemetryClient.TrackTrace($"Consume error: {msg.Error}");
                };

                telemetryClient.TrackTrace("Starting to subscribe to baseball-events...");

                consumer.Subscribe("baseball-events");
                Console.WriteLine("Consuming messages from topic: kafka, broker(s): wabone-kafka.servicebus.windows.net:9093");

                while (true)
                {
                    consumer.Poll(TimeSpan.FromMilliseconds(100));
                    var ticks = DateTime.Now.Ticks;
                    if (ticks % 500 == 0)
                    {
                        telemetryClient.Flush();
                    }
                }

                Task.Delay(5000).Wait();
            }

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
