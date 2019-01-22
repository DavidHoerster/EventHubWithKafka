using System;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;

namespace consumer
{

    public static class TelemetryWrapper
    {
        public static TelemetryClient Client { get; set; }

    }
}