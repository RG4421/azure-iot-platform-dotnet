using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.WindowsAzure.Storage.Table;
using IdentityGateway.Services.Models;
using IdentityGateway.Services.Helpers;

namespace IdentityGateway.Services
{
    public class UserTenantContainer : UserContainer, IUserContainer<UserTenantModel, UserTenantInput> 
    {
        public override string tableName { get{return "user";} }

        public UserTenantContainer(IHttpContextAccessor httpContextAccessor, TableHelper tableHelper) : base(httpContextAccessor, tableHelper)
        {
        }

        /// <summary>
        /// GetAll methods return all rows of the given user input userid
        /// </summary>
        /// <param name="input">UserTenantInput with the userId param</param>
        /// <returns></returns>
        public async Task<List<UserTenantModel>> GetAllAsync(UserTenantInput input)
        {
            TableQuery query = new TableQuery().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, input.userId));
            TableQuerySegment resultSegment = await this._tableHelper.QueryAsync(this.tableName, query, null);
            return (List<UserTenantModel>)resultSegment.Results;  // cast to a UserTenantModel list to easily parse result
        }

        /// <summary>
        /// Get methods return a specific UserTenantModel from the required info
        /// </summary>
        /// <param name="input">UserTenantInput with a userid</param>
        /// <returns></returns>
        public async Task<UserTenantModel> GetAsync(UserTenantInput input)
        {
            TableOperation retrieveUserTenant = TableOperation.Retrieve<UserTenantModel>(input.userId, this.tenant);
            TableResult result = await this._tableHelper.ExecuteOperationAsync(this.tableName, retrieveUserTenant);
            return (UserTenantModel)result.Result;
        }

        /// <summary>
        /// Create a User record in the UserTenantContainer using the given userId and current tenant
        /// </summary>
        /// <param name="input">UserTenantInput with a userId</param>
        /// <returns></returns>
        public async Task<UserTenantModel> CreateAsync(UserTenantInput input)
        {
            // Create the user and options for creating the user record in the user table
            UserTenantModel user = new UserTenantModel(input.userId, this.tenant, input.roles);
            TableOperation insertOperation = TableOperation.Insert(user);

            // Insert the user record. Return the user model from the user table insert
            TableResult userInsert = await this._tableHelper.ExecuteOperationAsync(this.tableName, insertOperation);
            return (UserTenantModel)userInsert.Result;  // cast to UserTenantModel to parse results
        }

        /// <summary>
        /// Delete a User record in the UserTenantContainer using the given userId and current tenant
        /// </summary>
        /// <param name="input">UserTenantInput with a userId</param>
        /// <returns></returns>
        public async Task<UserTenantModel> DeleteAsync(UserTenantInput input)
        {
            // Get a list of all user models for this user id - we will pick the one matching the current tenant to delete
            UserTenantModel user = await this.GetAsync(input);
            TableOperation deleteOperation = TableOperation.Delete(user);

            // delete the record and return the deleted user model
            TableResult deleteUser = await this._tableHelper.ExecuteOperationAsync(this.tableName, deleteOperation);
            return (UserTenantModel)deleteUser.Result;
        }
    }
}