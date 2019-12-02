﻿// Copyright (c) Microsoft. All rights reserved.    

using System;
using Mmm.Platform.IoT.Common.Services.Auth;
using Mmm.Platform.IoT.IdentityGateway.Services.Runtime;
using Mmm.Platform.IoT.Common.Services.Runtime;

namespace Mmm.Platform.IoT.IdentityGateway.WebService.Runtime
{
    public interface IConfig
    {
        // Web service listening port
        int Port { get; }

        // Service layer configuration
        IServicesConfig ServicesConfig { get; }

        // Client authentication and authorization configuration
        IClientAuthConfig ClientAuthConfig { get; }
    }

    /// <summary>Web service configuration</summary>
    public class Config : IConfig
    {
        private const string GLOBAL_KEY = "Global:";
        private const string APPLICATION_KEY = "IdentityGatewayService:";
        private const string PORT_KEY = APPLICATION_KEY + "webserviceport";

        private const string CLIENT_AUTH_KEY = GLOBAL_KEY + "ClientAuth:";
        private const string CORS_WHITELIST_KEY = CLIENT_AUTH_KEY + "corsWhitelist";
        private const string AUTH_TYPE_KEY = CLIENT_AUTH_KEY + "authType";
        private const string AUTH_REQUIRED_KEY = "AuthRequired";
        
        private const string EXTERNAL_DEPENDENCIES_KEY = "ExternalDependencies:";
        private const string USER_MANAGEMENT_URL_KEY = EXTERNAL_DEPENDENCIES_KEY + "authWebServiceUrl";

        private const string JWT_KEY = GLOBAL_KEY + "ClientAuth:JWT:";
        private const string JWT_ALGOS_KEY = JWT_KEY + "allowedAlgorithms";
        private const string JWT_ISSUER_KEY = JWT_KEY + "authissuer";
        private const string JWT_AUDIENCE_KEY = JWT_KEY + "audience";
        private const string JWT_CLOCK_SKEW_KEY = JWT_KEY + "clockSkewSeconds";

        private const string PUBLIC_KEY_KEY = "identityGatewayPublicKey";
        private const string PRIVATE_KEY_KEY = "identityGatewayPrivateKey";

        private const string AZURE_B2C_BASE_URI = GLOBAL_KEY + "AzureB2CBaseUri";
        private const string STORAGE_CONNECTION_STRING_KEY = "storageAccountConnectionString";
        private const string SEND_GRID_API_KEY = "sendGridAPIKey";

        public int Port { get; }
        public IServicesConfig ServicesConfig { get; }
        public IClientAuthConfig ClientAuthConfig { get; }

        public Config(IConfigData configData)
        {
            this.Port = configData.GetInt(PORT_KEY);

            this.ServicesConfig = new ServicesConfig
            {
                UserPermissions = configData.GetUserPermissions(),
                PublicKey = configData.GetString(PUBLIC_KEY_KEY),
                PrivateKey = configData.GetString(PRIVATE_KEY_KEY),
                StorageAccountConnectionString = configData.GetString(STORAGE_CONNECTION_STRING_KEY),
                AzureB2CBaseUri = configData.GetString(AZURE_B2C_BASE_URI),
                Port = configData.GetString(PORT_KEY),
                SendGridAPIKey = configData.GetString(SEND_GRID_API_KEY),
                UserManagementApiUrl = configData.GetString(USER_MANAGEMENT_URL_KEY),
            };

            this.ClientAuthConfig = new ClientAuthConfig
            {
                // By default CORS is disabled
                CorsWhitelist = configData.GetString(CORS_WHITELIST_KEY, string.Empty),
                // By default Auth is required
                AuthRequired = configData.GetBool(AUTH_REQUIRED_KEY, true),
                // By default auth type is JWT
                AuthType = configData.GetString(AUTH_TYPE_KEY, "JWT"),
                // By default the only trusted algorithms are RS256, RS384, RS512
                JwtAllowedAlgos = configData.GetString(JWT_ALGOS_KEY, "RS256,RS384,RS512").Split(','),
                JwtIssuer = configData.GetString(JWT_ISSUER_KEY),
                JwtAudience = configData.GetString(JWT_AUDIENCE_KEY),
                JwtSecurityKeys = null,
                // By default the allowed clock skew is 2 minutes
                JwtClockSkew = TimeSpan.FromSeconds(configData.GetInt(JWT_CLOCK_SKEW_KEY, 120)),
            };
        }
    }
}
