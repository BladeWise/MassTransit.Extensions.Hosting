using GreenPipes;
using MassTransit.Extensions.Hosting;
using MassTransit.Extensions.Hosting.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Example.ConsoleHost
{
    public static class Program
    {
        private static void Main(string[] args)
        {
            // use the new generic host from ASP.NET Core
            // see for more info: https://github.com/aspnet/Hosting/issues/1163
            new HostBuilder()
                .ConfigureHostConfiguration(config => config.AddEnvironmentVariables())
                .ConfigureAppConfiguration((context, builder) => ConfigureAppConfiguration(context, builder, args))
                .ConfigureServices(ConfigureServices)
                .Build()
                .Run();
        }

        private static void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder configurationBuilder, string[] args)
        {
            var environmentName = context.HostingEnvironment.EnvironmentName;

            configurationBuilder.AddJsonFile("appsettings.json", optional: true);
            configurationBuilder.AddJsonFile($"appsettings.{environmentName}.json", optional: true);

            configurationBuilder.AddCommandLine(args);
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            services.AddTransient<IWidgetService, WidgetService>();
            // add other DI services...

            // optionally use configuration for any settings
            var configuration = context.Configuration;

            // the following adds IBusManager which is also an IHostedService that is started/stopped by HostBuilder
            services.AddMassTransit(busBuilder =>
            {
                // load the RabbitMq options
                var rabbitMqOptions = configuration.GetSection("MassTransit:RabbitMq").Get<RabbitMqOptions>();

                busBuilder.UseRabbitMq(rabbitMqOptions, hostBuilder =>
                {
                    // use scopes for all downstream filters and consumers
                    // i.e. per-request lifetime
                    hostBuilder.UseServiceScope();

                    // example adding an optional configurator to the bus
                    // using IRabbitMqBusFactoryConfigurator
                    hostBuilder.AddConfigurator(configureBus =>
                    {
                        configureBus.UseRetry(r => r.Immediate(1));
                    });

                    // example adding a receive endpoint to the bus
                    hostBuilder.AddReceiveEndpoint("example-queue-1", endpointBuilder =>
                    {
                        // example adding an optional configurator to the receive endpoint
                        // using IRabbitMqReceiveEndpointConfigurator
                        endpointBuilder.AddConfigurator(configureEndpoint =>
                        {
                            configureEndpoint.UseRetry(r => r.Immediate(3));
                        });

                        // example adding a consumer to the receive endpoint
                        endpointBuilder.AddConsumer<ExampleConsumer>(configureConsumer =>
                        {
                            // example adding an optional configurator to the consumer
                            // using IConsumerConfigurator<TConsumer>
                            configureConsumer.UseRateLimit(10);
                        });
                    });
                });

                // adding more bus instances...
                busBuilder.UseInMemory("connection-name-2", hostBuilder =>
                {
                    hostBuilder.UseServiceScope();
                    hostBuilder.AddReceiveEndpoint("example-queue-2", endpointBuilder =>
                    {
                        endpointBuilder.AddConsumer<ExampleConsumer>();
                    });
                });
            });
        }

    }
}