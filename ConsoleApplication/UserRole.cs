using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
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

        protected UserRole()
        {
            Client = CreateDefaultClient();
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
            var user = userRes.Items.Any() ? userRes.Items.Single() : await CreateUser();

            // TODO: Temporary login
            var currentUser = await Client.Users.GetCurrentUser();
            if (currentUser?.Id != user.Id)
                await HttpUtils.GetAsync<User>($"{Program.APIUrl}users/set-current-user-id/{user.Id}");

            return user;
        }

        private async Task<User> CreateUser()
        {
            var user = await Client.Users.Create(UserData());
            var organization = await Client.Organizations.Create(new Organization
            {
                Name = $"Nodes Test {GetType().Name} Organization",
            });
            var subscription = await Client.Subscriptions.Create(new Subscription
            {
                OwnerOrganizationId = organization.Id,
            });
            var membership = await Client.Memberships.Create(new Membership
            {
                SubscriptionId = subscription.Id,
                UserId = user.Id,
            });
            return user;
        }

        protected UserRole(NodesClient client) => Client = client;

        public static NodesClient CreateDefaultClient()
        {
            Console.WriteLine($"Connecting to {Program.APIUrl}");
            var client = new NodesClient(Program.APIUrl);
            return client;
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