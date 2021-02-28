using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.RabbitMQ
{
    public record RabbitMQSettings
    {
        public static RabbitMQSettings _instance = null;

        public string DefaultExchangeName { get; init; }
        public string DeadLetterExchangeName { get; init; }
        public string DeadLetterQueueName { get; init; }
        public int EventHandlerRetryCount { get; init; }

        private RabbitMQSettings()
        {
            DefaultExchangeName = Environment.GetEnvironmentVariable("DefaultExchangeName") ?? "rabbitmq_event_bus";
            DeadLetterExchangeName = Environment.GetEnvironmentVariable("DeadLetterExchangeName") ?? "rabbitmq_dead_letter_bus";
            DeadLetterQueueName = Environment.GetEnvironmentVariable("DeadLetterQueueName") ?? "dead_letter_queue";

            var retryIsInt = Int32.TryParse(Environment.GetEnvironmentVariable("EventHandlerRetryCount"), out int retries);

            if (retryIsInt) EventHandlerRetryCount = retries;
            else EventHandlerRetryCount = 5;
        }

        public static RabbitMQSettings GetInstance()
        {
            if (_instance != null) return _instance;

            _instance = new RabbitMQSettings();
            return _instance;
        }
    }
}
