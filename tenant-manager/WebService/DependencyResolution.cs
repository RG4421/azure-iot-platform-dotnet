using System.Reflection;
using Autofac;
using Mmm.Platform.IoT.Common.Services.Diagnostics;
using Mmm.Platform.IoT.Common.Services.External;
using Mmm.Platform.IoT.Common.Services.Helpers;
using Mmm.Platform.IoT.Common.Services.Runtime;
using Mmm.Platform.IoT.Common.WebService;
using Mmm.Platform.IoT.Common.WebService.Auth;
using Mmm.Platform.IoT.TenantManager.Services.Helpers;
using Mmm.Platform.IoT.TenantManager.Services.Runtime;
using Mmm.Platform.IoT.TenantManager.WebService.Runtime;
using Mmm.Platform.IoT.Common.Services.Http;

namespace Mmm.Platform.IoT.TenantManager.WebService
{
    public class DependencyResolution : DependencyResolutionBase
    {
        protected override void SetupCustomRules(ContainerBuilder builder, ILogger logger, IHttpClient httpClient)
        {
            // Auto-wire additional assemblies
            var assembly = typeof(IServicesConfig).GetTypeInfo().Assembly;
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();

            // Make sure the configuration is read only once.
            IConfig config = new Config(new ConfigData(logger));
            builder.RegisterInstance(config).As<IConfig>().SingleInstance();

            // Service configuration is generated by the entry point, so we
            // prepare the instance here.
            builder.RegisterInstance(config.ServicesConfig).As<IServicesConfig>().SingleInstance();
            builder.RegisterInstance(config.ServicesConfig).As<IAppConfigClientConfig>().SingleInstance();
            builder.RegisterInstance(config.ServicesConfig).As<IUserManagementClientConfig>().SingleInstance();
            builder.RegisterInstance(config.ServicesConfig).As<IAuthMiddlewareConfig>().SingleInstance();

            // Add helpers
            var tokenHelper = new TokenHelper(config.ServicesConfig);
            builder.RegisterInstance(new TenantRunbookHelper(config.ServicesConfig, tokenHelper)).As<TenantRunbookHelper>().SingleInstance();
            builder.RegisterInstance(new CosmosHelper(config.ServicesConfig)).As<CosmosHelper>().SingleInstance();
            builder.RegisterInstance(new TableStorageHelper(config.ServicesConfig)).As<TableStorageHelper>().SingleInstance();
            builder.RegisterType<ExternalRequestHelper>().As<IExternalRequestHelper>().SingleInstance();

            // Auth and CORS setup
            Auth.Startup.SetupDependencies(builder, config);
        }
    }
}