﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Mmm.Platform.IoT.Common.Services.Config;
using Mmm.Platform.IoT.Common.Services.Exceptions;
using Mmm.Platform.IoT.Common.Services.Filters;
using Mmm.Platform.IoT.IdentityGateway.Services;
using Mmm.Platform.IoT.IdentityGateway.Services.Helpers;
using Mmm.Platform.IoT.IdentityGateway.Services.Models;
using Mmm.Platform.IoT.IdentityGateway.WebService.Models;
using Newtonsoft.Json;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Mmm.Platform.IoT.IdentityGateway.Controllers
{
    [Route("")]
    [TypeFilter(typeof(ExceptionsFilterAttribute))]
    public class AuthorizeController : Controller
    {
        private readonly IOpenIdProviderConfiguration openIdProviderConfiguration;
        private AppConfig config;
        private IJwtHelpers jwtHelper;
        private IAuthenticationContext authenticationContext;
        private UserTenantContainer userTenantContainer;
        private UserSettingsContainer userSettingsContainer;

        public AuthorizeController(AppConfig config, UserTenantContainer userTenantContainer, UserSettingsContainer userSettingsContainer, IJwtHelpers jwtHelper, IOpenIdProviderConfiguration openIdProviderConfiguration, IAuthenticationContext authenticationContext)
        {
            this.config = config;
            this.userTenantContainer = userTenantContainer;
            this.userSettingsContainer = userSettingsContainer;
            this.jwtHelper = jwtHelper;
            this.openIdProviderConfiguration = openIdProviderConfiguration;
            this.authenticationContext = authenticationContext;
        }

        // GET: connect/authorize
        [HttpGet]
        [Route("connect/authorize")]
        public IActionResult Get(
            [FromQuery] string redirect_uri,
            [FromQuery] string state,
            [FromQuery(Name = "client_id")] string clientId,
            [FromQuery] string nonce,
            [FromQuery] string tenant,
            [FromQuery] string invite)
        {
            // Validate Input
            if (!Uri.IsWellFormedUriString(redirect_uri, UriKind.Absolute))
            {
                throw new Exception("Redirect Uri is not valid!");
            }

            Guid validatedGuid = Guid.Empty;

            // if is not null we want to validate that it is a guid, Otherwise it will pick a tenant for the user
            if (tenant != null && !Guid.TryParse(tenant, out validatedGuid))
            {
                throw new Exception("Tenant is not valid!");
            }

            var uri = new UriBuilder(this.config.Global.AzureB2cBaseUri);

            // Need to build Query carefully to not clobber other query items -- just injecting state
            var query = HttpUtility.ParseQueryString(uri.Query);
            query["state"] = JsonConvert.SerializeObject(new AuthState
            { ReturnUrl = redirect_uri, State = state, Tenant = tenant, Nonce = nonce, ClientId = clientId, Invitation = invite });
            query["redirect_uri"] = openIdProviderConfiguration.Issuer + "/connect/callback"; // must be https for B2C
            uri.Query = query.ToString();
            return Redirect(uri.Uri.ToString());
        }

        // POST: connect/token
        [HttpPost]
        [Route("connect/token")]
        public async Task<IActionResult> PostTokenAsync(
            [FromBody] ClientCredentialInput input)
        {
            string resourceUri = "https://graph.microsoft.com/";
            ClientCredential clientCredential = new ClientCredential(input.ClientId, input.ClientSecret);

            try
            {
                AuthenticationResult token = await authenticationContext.AcquireTokenAsync(resourceUri, clientCredential);
            }
            catch (Exception e)
            {
                return StatusCode(401, e.Message);
            }

            UserTenantInput tenantInput = new UserTenantInput
            {
                UserId = input.ClientId,
                Tenant = input.Scope
            };
            UserTenantListModel tenantsModel = await this.userTenantContainer.GetAllAsync(tenantInput);
            if (tenantsModel.Models.Count == 0) {
                throw new Exception("Not granted access to that tenant");
            }

            // if successful, then mint token
            var jwtHandler = new JwtSecurityTokenHandler();
            var claims = new List<Claim>();
            claims.Add(new Claim("client_id", input.ClientId));
            claims.Add(new Claim("sub", input.ClientId));
            claims.Add(new Claim("name", input.ClientId));
            claims.Add(new Claim("type", "Client Credentials"));

            string tokenString = jwtHandler.WriteToken(await this.jwtHelper.GetIdentityToken(claims, input.Scope, "IoTPlatform", null));

            return StatusCode(200, tokenString);
        }
        // GET: connect/authorize
        /// <summary>
        /// This is a pass-through auth gateway so there is no need to officially end session.
        /// Session state is never saved. Therefore, simply return to redirect.
        /// </summary>
        /// <param name="post_logout_redirect_uri"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [HttpGet]
        [Route("connect/logout")]
        public IActionResult Get([FromQuery] string post_logout_redirect_uri)
        {
            // Validate Input
            if (!Uri.IsWellFormedUriString(post_logout_redirect_uri, UriKind.Absolute))
            {
                throw new Exception("Redirect Uri is not valid!");
            }

            var uri = new UriBuilder(post_logout_redirect_uri);

            return Redirect(
                uri.Uri.ToString());
        }

        // GET connect/callback
        [HttpPost("connect/switch/{tenant}")]
        public async Task<ActionResult> PostAsync([FromHeader(Name = "Authorization")] string authHeader, [FromRoute] string tenant)
        {
            if (authHeader == null || !authHeader.StartsWith("Bearer"))
            {
                throw new NoAuthorizationException("No Bearer Token Authorization Header was passed.");
            }

            // Extract Bearer token
            string encodedToken = authHeader.Substring("Bearer ".Length).Trim();
            var jwtHandler = new JwtSecurityTokenHandler();
            if (!jwtHelper.TryValidateToken("IoTPlatform", encodedToken, HttpContext, out JwtSecurityToken jwt))
            {
                throw new NoAuthorizationException("The given token could not be read or validated.");
            }

            if (jwt?.Claims?.Count(c => c.Type == "sub") == 0)
            {
                throw new NoAuthorizationException("Not allowed access. No User Claims");
            }
            // Create a userTenantInput for the purpose of finding if the user has access to the space
            UserTenantInput tenantInput = new UserTenantInput
            {
                UserId = jwt?.Claims?.Where(c => c.Type == "sub").First()?.Value,
                Tenant = tenant
            };
            UserTenantModel tenantResult = await this.userTenantContainer.GetAsync(tenantInput);
            if (tenantResult != null)
            {
                // Everything checks out so you can mint a new token
                var tokenString = jwtHandler.WriteToken(await this.jwtHelper.GetIdentityToken(jwt.Claims.Where(c => new List<string>() { "sub", "name", "email" }.Contains(c.Type)).ToList(), tenant, jwt.Audiences.First(), jwt.ValidTo));

                return StatusCode(200, tokenString);
            }
            else
            {
                throw new NoAuthorizationException("Not allowed access to this tenant.");
            }
        }

        // GET connect/callback
        [HttpPost("connect/callback")]
        public async Task<IActionResult> PostAsync(
            [FromForm] string state,
            [FromForm] string id_token,
            [FromForm] string error,
            [FromForm] string error_description)
        {
            if (!string.IsNullOrEmpty(error))
            {
                // If there was an error returned from B2C, throw it as an expcetion
                throw new Exception($"Azure B2C returned an error: {{{error}: {error_description}}}");
            }

            AuthState authState = null;
            try
            {
                authState = JsonConvert.DeserializeObject<AuthState>(state);
            }
            catch (Exception e)
            {
                throw new Exception("Invlid state from authentication redirect", e);
            }

            var originalAudience = authState.ClientId;

            // Bring over Subject and Name
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwt = jwtHandler.ReadJwtToken(id_token);
            var claims = jwt.Claims.Where(t => new List<string> { "sub", "name" }.Contains(t.Type)).ToList();

            // If theres an invitation token then add user to tenant
            if (!string.IsNullOrEmpty(authState.Invitation))
            {
                var inviteJWT = jwtHandler.ReadJwtToken(authState.Invitation);
                UserTenantInput UserTenant = new UserTenantInput()
                {
                    UserId = claims.Where(c => c.Type == "sub").First().Value,
                    Tenant = inviteJWT.Claims.Where(c => c.Type == "tenant").First().Value,
                    Roles = JsonConvert.SerializeObject(inviteJWT.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList()),
                    Type = "Member"
                };
                await this.userTenantContainer.UpdateAsync(UserTenant);

                // Delete placeholder for invite
                UserTenant.UserId = inviteJWT.Claims.Where(c => c.Type == "userId").First().Value;
                await this.userTenantContainer.DeleteAsync(UserTenant);
            }

            // Extract first email
            var emailClaim = jwt.Claims.Where(t => t.Type == "emails").FirstOrDefault();
            if (emailClaim != null)
            {
                claims.Add(new Claim("email", emailClaim.Value));
            }

            if (!string.IsNullOrEmpty(authState.Nonce))
            {
                claims.Add(new Claim("nonce", authState.Nonce));
            }

            string tokenString = jwtHandler.WriteToken(await this.jwtHelper.GetIdentityToken(claims, authState.Tenant, originalAudience, null));

            // Build Return Uri
            var returnUri = new UriBuilder(authState.ReturnUrl);

            // Need to build Query carefully to not clobber other query items -- just injecting state
            // var query = HttpUtility.ParseQueryString(returnUri.Query);
            // query["state"] = HttpUtility.UrlEncode(authState.state);
            // returnUri.Query = query.ToString();

            returnUri.Fragment =
                "id_token=" + tokenString + "&state=" +
                HttpUtility.UrlEncode(authState
                    .State); // pass token in Fragment for more security (Browser wont forward...)
            return Redirect(returnUri.Uri.ToString());
        }
    }
}
public class ErrorModel
{
    public string ErrorMessage { get; set; }
}
