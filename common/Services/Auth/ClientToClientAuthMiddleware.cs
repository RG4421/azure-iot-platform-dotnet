using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mmm.Platform.IoT.Common.Services.Auth
{
    public class ClientToClientAuthMiddleware
    {
        private const string TenantHeader = "ApplicationTenantID";
        private const string TenantKey = "TenantID";
        private readonly ILogger logger;
        private RequestDelegate requestDelegate;

        public ClientToClientAuthMiddleware(RequestDelegate requestDelegate, ILogger<ClientToClientAuthMiddleware> logger)
        {
            this.requestDelegate = requestDelegate;
            this.logger = logger;
        }

        public Task Invoke(HttpContext context)
        {
            string tenantId = context.Request.Headers[TenantHeader].ToString();

            context.Request.SetTenant(tenantId);
            return this.requestDelegate(context);

        }
    }
}
