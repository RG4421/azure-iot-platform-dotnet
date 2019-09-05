﻿using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.IoTSolutions.TenantManager.WebService;
using ILogger = Microsoft.Azure.IoTSolutions.TenantManager.Services.Diagnostics.ILogger;
using Microsoft.Azure.IoTSolutions.TenantManager.Services.Runtime;
using Microsoft.Azure.IoTSolutions.TenantManager.WebService.Runtime;
using Microsoft.Azure.IoTSolutions.TenantManager.Services.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace MMM.Azure.IoTSolutions.TenantManager.WebService
{
    public class Startup
    {

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            var builder = new ConfigurationBuilder();
#if DEBUG
            builder.AddIniFile("appsettings.ini", optional: false, reloadOnChange: true);
#endif
            builder.AddEnvironmentVariables();
            var settings = builder.Build();
            builder.AddAzureAppConfiguration(settings["PCS_APPLICATION_CONFIGURATION"]);
            Configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            services.AddSingleton<IConfiguration>(Configuration);

            ILogger logger = new Logger(Uptime.ProcessId, Microsoft.Azure.IoTSolutions.TenantManager.Services.Diagnostics.LogLevel.Info);
            services.AddSingleton<ILogger>(logger);

            IConfig config = new Config(new ConfigData(logger));
            services.AddSingleton<IConfig>(config);

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            Microsoft.Azure.IoTSolutions.TenantManager.WebService.Auth.Startup.SetupDependencies(services, config);

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            // Check for Authorization header before dispatching requests
            app.UseMiddleware<AuthMiddleware>();

            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
