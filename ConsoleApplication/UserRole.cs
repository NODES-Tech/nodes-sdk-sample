using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Nodes.API.Enums;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;

namespace ConsoleApplication
{
    public abstract class UserRole
    {
        protected readonly NodesClient Client;
        protected Membership? Membership;
        protected User? User;
        protected Subscription? Subscription;
        protected Organization? Organization;

        protected UserRole(NodesClient client)
        {
            Client = client;
            CreateUserAndLogin().GetAwaiter().GetResult();
            FetchBasicInfo().GetAwaiter().GetResult();
        }


        public virtual User UserData() =>
            new User
            {
                Email = $"nodes-user-{GetType().Name}@example.com",
                LoginHandle = "nodes-user@example.com",
                FamilyName = $"{GetType().Name} USER",
                GivenName = "NODES",
            };

        public virtual async Task<User> CreateUserAndLogin()
        {
            var userRes = await Client.Users.GetByTemplate(new User {Email = UserData().Email});
            var user = userRes.Items.Any() ? userRes.Items.FirstOrDefault() : await CreateUser();
            return user;
        }

        private async Task<User> CreateUser()
        {
            var user = await Client.Users.Create(UserData());
            var organization = await Client.Organizations.Create(new Organization
            {
                Name = $"Nodes Test {GetType().Name} Organization",
                CountryId = "NO",
            });
            var subscription = await Client.Subscriptions.Create(new Subscription
            {
                OwnerOrganizationId = organization.Id,
                SubscriptionType = SubscriptionType.DSO,
            });
            var permissionSets = await Client.PermissionSets.GetByTemplate();
            var membership = await Client.Memberships.Create(new Membership
            {
                SubscriptionId = subscription.Id,
                UserId = user.Id,
                PermissionSetId = permissionSets.Items.First().Id,
            });
            return user;
        }

        protected async Task FetchBasicInfo()
        {
            User = Client.Users.GetCurrentUser().GetAwaiter().GetResult() ?? throw new Exception("Authentication: TBA");
            var memberships = await Client.Memberships.GetByTemplate(new Membership {UserId = User.Id});
            Membership = memberships.Items.FirstOrDefault() ?? throw new Exception($"No memberships for user {User}");
            Subscription = await Client.Subscriptions.GetById(Membership.SubscriptionId);
            Organization = await Client.Organizations.GetById(Subscription.OwnerOrganizationId);
        }
    }
}