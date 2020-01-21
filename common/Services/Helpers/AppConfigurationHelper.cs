using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.AppConfiguration;
using Mmm.Platform.IoT.Common.Services.Config;
using Mmm.Platform.IoT.Common.Services.Models;

namespace Mmm.Platform.IoT.Common.Services.Helpers
{
    public class AppConfigurationHelper : IAppConfigurationHelper
    {
        private ConfigurationClient client;
        private Dictionary<string, AppConfigCacheValue> cache = new Dictionary<string, AppConfigCacheValue>();

        public AppConfigurationHelper(AppConfig config)
        {
            this.client = new ConfigurationClient(config.AppConfigurationConnectionString);
        }

        public async Task SetValueAsync(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("the key parameter must not be null or empty to create a new app config key value pair.");
            }

            try
            {
                await this.client.SetConfigurationSettingAsync(key, value);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to create the app config key value pair {{{key}, {value}}}", e);
            }
        }

        /// <summary>
        /// Get a value from app config using the given key
        /// </summary>
        /// <param name="key"></param>
        /// <returns>The value returned by app config for the given key</returns>
        public string GetValue(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("App Config cannot take a null key parameter. The given key was not correctly configured.");
            }

            string value = string.Empty;
            try
            {
                if (this.cache.ContainsKey(key) && this.cache[key].ExpirationTime > DateTime.UtcNow)
                {
                    value = this.cache[key].Value.Value; // get string from configuration setting
                }
                else
                {
                    ConfigurationSetting setting = this.client.GetConfigurationSetting(key);
                    value = setting.Value;
                    if (this.cache.ContainsKey(key))
                    {
                        this.cache[key] = new AppConfigCacheValue(setting);
                    }
                    else
                    {
                        this.cache.Add(key, new AppConfigCacheValue(setting));
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"An exception occured while getting the value of {key} from App Config:\n" + e.Message);
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new NullReferenceException($"App Config returned a null value for {key}");
            }
            return value;
        }

        /// <summary>
        /// Delete an app config key value pair based on key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task DeleteKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("The key parameter must not be null or empty to delete an app config key value pair.");
            }

            try
            {
                await this.client.DeleteConfigurationSettingAsync(key);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to delete the app config key value pair for key {key}", e);
            }
        }
    }
}