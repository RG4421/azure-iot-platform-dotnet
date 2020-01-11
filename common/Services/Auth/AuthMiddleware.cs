﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Mmm.Platform.IoT.Common.Services.Config;
using Mmm.Platform.IoT.Common.Services.External;

namespace Mmm.Platform.IoT.Common.Services.Auth
{
    /// <summary>
    /// Validate every incoming request checking for a valid authorization header.
    /// The header must containg a valid JWT token. Other than the usual token
    /// validation, the middleware also restrict the allowed algorithms to block
    /// tokens created with a weak algorithm.
    /// Validations used:
    /// * The issuer must match the one in the configuration
    /// * The audience must match the one in the configuration
    /// * The token must not be expired, some configurable clock skew is allowed
    /// * Signature is required
    /// * Signature must be valid
    /// * Signature must be from the issuer
    /// * Signature must use one of the algorithms configured
    /// </summary>
    public class AuthMiddleware
    {
        // The authorization header carries a bearer token, with this prefix
        private const string AUTH_HEADER_PREFIX = "Bearer ";

        // Usual authorization header, carrying the bearer token
        private const string AUTH_HEADER = "Authorization";

        // User requests are marked with this header by the reverse proxy
        // TODO ~devis: this is a temporary solution for public preview only
        // TODO ~devis: remove this approach and use the service to service authentication
        // https://github.com/Azure/pcs-auth-dotnet/issues/18
        // https://github.com/Azure/azure-iot-pcs-remote-monitoring-dotnet/issues/11
        private const string EXT_RESOURCES_HEADER = "X-Source";

        private const string ERROR401 = @"{""Error"":""Authentication required""}";
        private const string ERROR503_AUTH = @"{""Error"":""Authentication service not available""}";

        private readonly RequestDelegate requestDelegate;
        private readonly IConfigurationManager<OpenIdConnectConfiguration> openIdCfgMan;
        private readonly AppConfig config;
        private readonly ILogger _logger;
        private TokenValidationParameters tokenValidationParams;
        private readonly bool authRequired;
        private bool tokenValidationInitialized;
        private readonly IUserManagementClient userManagementClient;
        private readonly List<string> allowedUrls = new List<string>() { "/v1/status", "/api/status", "/.well-known/openid-configuration", "/connect" };

        public AuthMiddleware(
            RequestDelegate requestDelegate,
            IConfigurationManager<OpenIdConnectConfiguration> openIdCfgMan,
            AppConfig config,
            IUserManagementClient userManagementClient,
            ILogger<AuthMiddleware> logger)
        {
            this.requestDelegate = requestDelegate;
            this.openIdCfgMan = openIdCfgMan;
            this.config = config;
            _logger = logger;
            this.authRequired = config.Global.AuthRequired;
            this.tokenValidationInitialized = false;
            this.userManagementClient = userManagementClient;

            // This will show in development mode, or in case auth is turned off
            if (!this.authRequired)
            {
                _logger.LogWarning("### AUTHENTICATION IS DISABLED! ###");
                _logger.LogWarning("### AUTHENTICATION IS DISABLED! ###");
                _logger.LogWarning("### AUTHENTICATION IS DISABLED! ###");
            }
            else
            {
                _logger.LogInformation("Auth config is {config}", config);

                this.InitializeTokenValidationAsync(CancellationToken.None).Wait();
            }

            // TODO ~devis: this is a temporary solution for public preview only
            // TODO ~devis: remove this approach and use the service to service authentication
            // https://github.com/Azure/pcs-auth-dotnet/issues/18
            // https://github.com/Azure/azure-iot-pcs-remote-monitoring-dotnet/issues/11
            _logger.LogWarning("### Service to service authentication is not available in public preview ###");
            _logger.LogWarning("### Service to service authentication is not available in public preview ###");
            _logger.LogWarning("### Service to service authentication is not available in public preview ###");
        }

        public Task Invoke(HttpContext context)
        {
            var header = string.Empty;
            var token = string.Empty;

            // Store this setting to skip validating authorization in the controller if enabled
            context.Request.SetAuthRequired(config.Global.AuthRequired);

            context.Request.SetExternalRequest(true);

            // Skip Authentication on certain URLS
            if (allowedUrls.Where(s => context.Request.Path.StartsWithSegments(s)).Count() > 0)
            {
                return this.requestDelegate(context);
            }
            if (!context.Request.Headers.ContainsKey(EXT_RESOURCES_HEADER))
            {
                // This is a service to service request running in the private
                // network, so we skip the auth required for user requests
                // Note: this is a temporary solution for public preview
                // https://github.com/Azure/pcs-auth-dotnet/issues/18
                // https://github.com/Azure/azure-iot-pcs-remote-monitoring-dotnet/issues/11

                // Call the next delegate/middleware in the pipeline
                _logger.LogDebug("Skipping auth for service to service request");
                context.Request.SetExternalRequest(false);
                context.Request.SetTenant();
                return this.requestDelegate(context);
            }

            if (!this.authRequired)
            {
                // Call the next delegate/middleware in the pipeline
                _logger.LogDebug("Skipping auth (auth disabled)");
                return this.requestDelegate(context);
            }

            if (!this.InitializeTokenValidationAsync(context.RequestAborted).Result)
            {
                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                context.Response.Headers["Content-Type"] = "application/json";
                context.Response.WriteAsync(ERROR503_AUTH);
                return Task.CompletedTask;
            }

            if (context.Request.Headers.ContainsKey(AUTH_HEADER))
            {
                header = context.Request.Headers[AUTH_HEADER].SingleOrDefault();
            }
            else
            {
                _logger.LogError("Authorization header not found");
            }

            if (header != null && header.StartsWith(AUTH_HEADER_PREFIX))
            {
                token = header.Substring(AUTH_HEADER_PREFIX.Length).Trim();
            }
            else
            {
                _logger.LogError("Authorization header prefix not found");
            }

            if (this.ValidateToken(token, context) || !this.authRequired)
            {
                // Call the next delegate/middleware in the pipeline
                return this.requestDelegate(context);
            }

            _logger.LogWarning("Authentication required");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.Headers["Content-Type"] = "application/json";
            context.Response.WriteAsync(ERROR401);

            return Task.CompletedTask;
        }

        private bool ValidateToken(string token, HttpContext context)
        {
            if (string.IsNullOrEmpty(token)) return false;

            try
            {
                SecurityToken validatedToken;
                var handler = new JwtSecurityTokenHandler();
                handler.ValidateToken(token, this.tokenValidationParams, out validatedToken);
                var jwtToken = new JwtSecurityToken(token);

                // Validate the signature algorithm
                if (config.Global.ClientAuth.Jwt.AllowedAlgorithms.Contains(jwtToken.SignatureAlgorithm))
                {
                    // Store the user info in the request context, so the authorization
                    // header doesn't need to be parse again later in the User controller.
                    context.Request.SetCurrentUserClaims(jwtToken.Claims);

                    AddAllowedActionsToRequestContext(context);

                    //Set Tenant Information
                    context.Request.SetTenant();

                    return true;
                }

                _logger.LogError("JWT token signature algorithm '{signatureAlgorithm}' is not allowed.", jwtToken.SignatureAlgorithm);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to validate JWT token");
            }

            return false;
        }

        private void AddAllowedActionsToRequestContext(HttpContext context)
        {
            var roles = context.Request.GetCurrentUserRoleClaim().ToList();
            if (!roles.Any())
            {
                _logger.LogWarning("JWT token doesn't include any role claims.");
                context.Request.SetCurrentUserAllowedActions(new string[] { });
                return;
            }

            var allowedActions = new List<string>();
            foreach (string role in roles)
            {
                if (!Permissions.Roles.ContainsKey(role))
                {
                    _logger.LogWarning("Role claim specifies a role '{role}' that does not exist", role);
                    continue;
                }

                allowedActions.AddRange(Permissions.Roles[role]);
            }

            context.Request.SetCurrentUserAllowedActions(allowedActions);
        }

        private async Task<bool> InitializeTokenValidationAsync(CancellationToken token)
        {
            if (this.tokenValidationInitialized) return true;

            try
            {
                _logger.LogInformation("Initializing OpenID configuration");
                var openIdConfig = await this.openIdCfgMan.GetConfigurationAsync(token);

                //Attempted to do it myself still issue with SSL
                //HttpWebRequest request = (HttpWebRequest)WebRequest.Create(this.config.JwtIssuer+ "/.well-known/openid-configuration/jwks");
                //request.AutomaticDecompression = DecompressionMethods.GZip;
                //IdentityKeys

                //using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                //using (Stream stream = response.GetResponseStream())
                //using (StreamReader reader = new StreamReader(stream))
                //{
                //    keys = JsonConvert.DeserializeObject<IdentityGatewayKeys>(reader.ReadToEnd());
                //}

                this.tokenValidationParams = new TokenValidationParameters
                {
                    // Validate the token signature
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = openIdConfig.SigningKeys,

                    // Validate the token issuer
                    ValidateIssuer = true,
                    ValidIssuer = config.Global.ClientAuth.Jwt.AuthIssuer,

                    // Validate the token audience
                    ValidateAudience = false,
                    ValidAudience = config.Global.ClientAuth.Jwt.Audience,

                    // Validate token lifetime
                    ValidateLifetime = true,
                    ClockSkew = new TimeSpan(0, 0, config.Global.ClientAuth.Jwt.ClockSkewSeconds)
                };

                this.tokenValidationInitialized = true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to setup OpenId Connect");
            }

            return this.tokenValidationInitialized;
        }
    }
}
