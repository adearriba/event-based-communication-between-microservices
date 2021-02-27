using ConsumerMicroservice.IntegrationEvents.EventHandlers;
using EventBus.Interfaces;
using EventBus.SubscriptionManager;
using EventBusRabbitMQ.Connections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace ConsumerMicroservice
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "ConsumerMicroservice", Version = "v1" });
            });

            services.AddSingleton<IRabbitMQPersistentConnection>(sp => RabbitMQStartup.CreateDefaultPersistentConnection(Configuration, sp));
            
            services.AddTransient<WeatherForecastRequestedHandler>();
            services.AddTransient<WeatherForecastRequestedDeadLetterHandler>();

            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            services.AddSingleton<IEventBusDeadLetterSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            services.AddSingleton<IEventBusSubscriber>(sp => RabbitMQStartup.CreateEventBusSubscriber(Configuration, sp));

            services.AddSingleton<IEventBusDeadLetterSubscriber>(sp => RabbitMQStartup.CreateEventBusDeadLetterSubscriber(sp));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConsumerMicroservice v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            RabbitMQStartup.ConfigureEventBusSubscriber(app);
            RabbitMQStartup.ConfigureEventBusDeadLetterSubscriber(app);
        }
    }
}
