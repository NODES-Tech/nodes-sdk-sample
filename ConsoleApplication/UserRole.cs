using System;
using System.Linq;
using System.Threading.Tasks;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;

namespace ConsoleApplication
{
    public abstract class UserRole
    {
        protected readonly NodesClient Client;
        protected Membership Membership;
        protected User User;
        protected Subscription Subscription;
        protected Organization Organization;

        protected UserRole(NodesClient client = null)
        {
            Client = client ?? CreateDefaultClient();
            FetchBasicInfo().GetAwaiter().GetResult();
        }

        private static NodesClient CreateDefaultClient()
        {
            Console.WriteLine($"Connecting to {Program.APIUrl}");
            var client = new NodesClient(Program.APIUrl);

            Console.WriteLine("checking authentication... ");
            var user = client.Users.GetCurrentUser().GetAwaiter().GetResult() ?? throw new Exception("Authentication: TBA");

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