using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mmm.Platform.IoT.Common.Services;
using Mmm.Platform.IoT.Common.Services.Filters;
using Mmm.Platform.IoT.TenantManager.Services;
using Mmm.Platform.IoT.TenantManager.Services.Models;
using Newtonsoft.Json;

namespace Mmm.Platform.IoT.TenantManager.WebService.Controllers
{
    [Route("api/[controller]")]
    [TypeFilter(typeof(ExceptionsFilterAttribute))]
    public class TenantController : Controller
    {
        private readonly ITenantContainer tenantContainer;
        private readonly ILogger logger;

        public TenantController(ITenantContainer tenantContainer, ILogger<TenantController> logger)
        {
            this.tenantContainer = tenantContainer;
            this.logger = logger;
        }

        // POST api/tenant
        [HttpPost]
        public async Task<CreateTenantModel> PostAsync()
        {
            /* Creates a new tenant */
            // Generate new tenant Id
            string tenantGuid = Guid.NewGuid().ToString();
            string userId = this.GetClaimsUserId();
            try
            {
                return await this.tenantContainer.CreateTenantAsync(tenantGuid, userId);
            }
            catch (Exception e)
            {
                // If there is an error while creating the new tenant - delete all of the created tenant resources
                // this may not be able to delete iot hub - due to the long running process
                var deleteResponse = await this.tenantContainer.DeleteTenantAsync(tenantGuid, userId, false);
                logger.LogInformation("The Tenant was unable to be created properly. To ensure the failed tenant does not consume resources, some of its resources were deleted after creation failed. {response}", deleteResponse);
                throw e;
            }
        }

        // GET api/tenant/<tenantId>
        [HttpGet("")]
        [Authorize("ReadAll")]
        public async Task<TenantModel> GetAsync()
        {
            return await this.tenantContainer.GetTenantAsync(this.GetTenantId());
        }

        [HttpDelete("")]
        [Authorize("DeleteTenant")]
        public async Task<DeleteTenantModel> DeleteAsync([FromQuery] bool ensureFullyDeployed = true)
        {
            return await this.tenantContainer.DeleteTenantAsync(this.GetTenantId(), this.GetClaimsUserId(), ensureFullyDeployed);
        }
    }
}
