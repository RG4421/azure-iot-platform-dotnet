using System.Threading.Tasks;

namespace Mmm.Platform.IoT.Common.Services.External.AppConfiguration
{
    public interface IAppConfigurationClient : IStatusOperation
    {
        Task SetValueAsync(string key, string value);
        string GetValue(string key);
        Task DeleteKeyAsync(string key);
    }
}