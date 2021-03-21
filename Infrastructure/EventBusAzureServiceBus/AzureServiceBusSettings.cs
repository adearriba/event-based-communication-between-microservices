using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.AzureServiceBus
{
    public record AzureServiceBusSettings
    {
        public static AzureServiceBusSettings _instance = null;

        public string IntegrationEventSuffix { get; init; }

        private AzureServiceBusSettings()
        {
            IntegrationEventSuffix = Environment.GetEnvironmentVariable("IntegrationEventSuffix") ?? "IntegrationEvent";
        }

        public static AzureServiceBusSettings GetInstance()
        {
            if (_instance != null) return _instance;

            _instance = new AzureServiceBusSettings();
            return _instance;
        }
    }
}
