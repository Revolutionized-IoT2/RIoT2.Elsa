using Elsa.Extensions;
using Elsa.Features.Services;
using RIoT2.Elsa.Server.RIoT.Features;

namespace RIoT2.Elsa.Server.RIoT.Extensions
{
    public static class RIoTModuleExtensions
    {
        /// <summary>
        /// Adds RIoT capabilities to Elsa.
        /// </summary>
        public static IModule UseRIoT(this IModule module, Action<RIoTFeature>? configure = null)
        {
            module.Use(configure);
            return module;
        }
    }
}
