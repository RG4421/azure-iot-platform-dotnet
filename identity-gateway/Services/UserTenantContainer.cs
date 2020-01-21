using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using Mmm.Platform.IoT.Common.Services.External.TableStorage;
using Mmm.Platform.IoT.IdentityGateway.Services.Models;

namespace Mmm.Platform.IoT.IdentityGateway.Services
{
    public class UserTenantContainer : UserContainer, IUserContainer<UserTenantModel, UserTenantInput>
    {
        public UserTenantContainer()
        {
        }

        public UserTenantContainer(ITableStorageClient tableStorageClient)
            : base(tableStorageClient)
        {
        }

        public override string TableName => "user";

        public virtual async Task<UserTenantListModel> GetAllAsync(UserTenantInput input)
        {
            TableQuery<UserTenantModel> query = new TableQuery<UserTenantModel>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, input.UserId));
            List<UserTenantModel> result = await this.TableStorageClient.QueryAsync<UserTenantModel>(this.TableName, query);
            return new UserTenantListModel("GetTenants", result);
        }

        public virtual async Task<UserTenantListModel> GetAllUsersAsync(UserTenantInput input)
        {
            TableQuery<UserTenantModel> query = new TableQuery<UserTenantModel>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, input.Tenant));
            List<UserTenantModel> result = await this.TableStorageClient.QueryAsync<UserTenantModel>(this.TableName, query);
            return new UserTenantListModel("GetUsers", result);
        }

        public virtual async Task<UserTenantModel> GetAsync(UserTenantInput input)
        {
            return await this.TableStorageClient.RetrieveAsync<UserTenantModel>(this.TableName, input.UserId, input.Tenant);
        }

        public virtual async Task<UserTenantModel> CreateAsync(UserTenantInput input)
        {
            // In order to create a new user with a tenant, create a new user id
            if (input.UserId == null)
            {
                input.UserId = Guid.NewGuid().ToString();
            }
            // Create the user and options for creating the user record in the user table
            UserTenantModel existingModel = await this.GetAsync(input);
            if (existingModel != null)
            {
                // If this record already exists, return it without continuing with the insert operation
                throw new StorageException(
                    $"That UserTenant record already exists with value {existingModel.Roles}." +
                    " Use PUT instead to update this setting instead.");
            }
            UserTenantModel user = new UserTenantModel(input);
            return await this.TableStorageClient.InsertAsync(this.TableName, user);
        }

        public virtual async Task<UserTenantModel> UpdateAsync(UserTenantInput input)
        {
            UserTenantModel model = new UserTenantModel(input);
            if (model.RoleList != null && !model.RoleList.Any())
            {
                // If the RoleList of the model is empty, throw an exception. The RoleList is the only updateable feature of the UserTenant Table
                throw new ArgumentException("The UserTenant update model must contain a serialized role array.");
            }
            model.ETag = "*";  // An ETag is required for updating - this allows any etag to be used
            return await this.TableStorageClient.InsertOrMergeAsync(this.TableName, model);
        }

        public virtual async Task<UserTenantModel> DeleteAsync(UserTenantInput input)
        {
            // Get a list of all user models for this user id - we will pick the one matching the current tenant to delete
            UserTenantModel user = await this.GetAsync(input);
            if (user == null)
            {
                throw new StorageException($"That UserTenant does not exist");
            }

            user.ETag = "*";  // An ETag is required for deleting - this allows any etag to be used
            return await this.TableStorageClient.DeleteAsync(this.TableName, user);
        }

        public virtual async Task<UserTenantListModel> DeleteAllAsync(UserTenantInput input)
        {
            UserTenantListModel tenantRows = await this.GetAllUsersAsync(input);

            // delete all rows as one asynchronous job
            var deleteTasks = tenantRows.Models.Select(row =>
            {
                UserTenantInput deleteInput = new UserTenantInput
                {
                    UserId = row.PartitionKey,
                    Tenant = input.Tenant,
                };
                return this.DeleteAsync(deleteInput);
            });
            var deletionResult = await Task.WhenAll(deleteTasks);
            return new UserTenantListModel("Delete", deletionResult.ToList());
        }
    }
}