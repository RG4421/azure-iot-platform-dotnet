﻿using Microsoft.Azure.KeyVault;
using Microsoft.Azure;
using Microsoft.Azure.Services.AppAuthentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DependencyResolver;
using Microsoft.Azure.KeyVault.Models;

namespace TokenGenerator.Helpers
{
    public class KeyVaultHelper : IDisposable
    {
        private IKeyVaultClient client;
        private IConfiguration _config;
        public KeyVaultHelper(IConfiguration _config)
        {
            string AzureServicesAuthConnectionString =
                $"RunAs=App;AppId={_config["keyvaultAppId"]};TenantId={_config["tenantId"]};AppKey={_config["keyvaultAppKey"]};";

            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider(AzureServicesAuthConnectionString);

            client = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

        }

        public string getKeyVaultSecretIdentifier(string secret, IConfiguration _config)
        {
            return $"https://{ _config["keyvaultName"]}.vault.azure.net/secrets/{secret}";
        }
        public async Task<string> getSecretAsync(string secret)
        {
            return (await client.GetSecretAsync(getKeyVaultSecretIdentifier("tenantStorageAccountConnectionString", this._config))).Value;
            
        }

        public void Dispose()
        {

        }
    }
}