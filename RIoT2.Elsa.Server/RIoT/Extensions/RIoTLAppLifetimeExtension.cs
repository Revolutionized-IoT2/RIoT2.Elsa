using RIoT2.Elsa.Server.RIoT.Services;

namespace RIoT2.Elsa.Server.RIoT.Extensions
{
    public static class RIoTLAppLifetimeExtension
    {
        public static IHostApplicationLifetime SetRIoTAppLifetime(this IHostApplicationLifetime lifetime, IServiceProvider services)
        {
            lifetime.ApplicationStopping.Register(onShutdown);

            void onShutdown() //this code is called when the application stops
            {
                var mqttService = services.GetRequiredService<MqttBackgroundService>();
                mqttService.StopAsync(default).Wait();
            }

            return lifetime;
        }
    }
}
