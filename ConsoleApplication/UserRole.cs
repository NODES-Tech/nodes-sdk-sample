using System;
using System.Linq;
using System.Threading.Tasks;
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

        protected UserRole(NodesClient client) => Client = client;

        public static NodesClient CreateDefaultClient()
        {
            Console.WriteLine($"Connecting to {Program.APIUrl}");
            var client = new NodesClient(Program.APIUrl);
            // From portal-test: 
            var token =
                @"eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImtpZCI6IkE5QUFGOEU1MTQyMjBBN0E3MUJGNjg0RDU2NzIzRDJBNUU4MzY4MzkiLCJ4NXQiOiJxYXI0NVJRaUNucHh2MmhOVm5JOUtsNkRhRGsifQ.eyJpc3MiOiJodHRwczovL25vZGVzdGVjaGRldi5iMmNsb2dpbi5jb20vMmI1OGNkNzAtMTUzYS00ZmUxLWFkNzQtZjgzOWZlMjE0ODE1L3YyLjAvIiwiZXhwIjoxNTg2ODkzMTU2LCJuYmYiOjE1ODY4ODk1NTYsImF1ZCI6ImFlZmE3ZDYxLTRiYzEtNDdhNS05ZTI0LWEzMDU3OTk4YWFkYSIsInN1YiI6ImJmNmViNTY2LTEwOWUtNDgzZS05OGVlLTA0NDQ0Yjg3NzE2NyIsImVtYWlsIjoibm9kZXNvcGVyYXRvckBnbWFpbC5jb20iLCJuYW1lIjoiTm9kZXMgb3BlcmF0b3IiLCJnaXZlbl9uYW1lIjoibm9kZXNvcGVyYXRvciIsImZhbWlseV9uYW1lIjoibm9kZXMgb3BlcmF0b3IiLCJleHRlbnNpb25fbm9kZXNfcmVmIjoiQkVBRkJGMUItMUMwMS00MDNDLTk4RTctMzRBNzgxQTRBRDU4IiwidGlkIjoiMmI1OGNkNzAtMTUzYS00ZmUxLWFkNzQtZjgzOWZlMjE0ODE1IiwidGZwIjoiQjJDXzFBX3NpZ25pbl9vbmx5Iiwibm9uY2UiOiJkMTRjZmJiMy0yMDE4LTQ1YWItOTUxNC0wZmVkMTVmNTI4ZjMiLCJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLCJhenAiOiJhZWZhN2Q2MS00YmMxLTQ3YTUtOWUyNC1hMzA1Nzk5OGFhZGEiLCJ2ZXIiOiIxLjAiLCJpYXQiOjE1ODY4ODk1NTZ9.JJv7nQwviB4jY9sZ4e0UqDmJ30gJ6TpiZMla09KyJ4Bw5T_DN2VtyE06kl5B1Xe1lfbJ9oG5OfFmUG1DlLSNFMO6TxqT4BSdABl_UNp372HgdzaaxHTicvl2q7qhR30CT6U9ER1cEbUdkHRB4EA3E_Jy7UrzusTnlNi1g4W4_KoKRkSFrtyiP62m34-U_pI8D_wE5v71RG2Sh-kP1FqvZen2-6OYZYM3wTQFLD86_qef2SsA83PVU6Qd2wM5yqEIc7jGS2uFwVTociUZeJ3lwhGx3STyiB2fxpX5Y4M55XmGfEIFhP8_HEPyMa2yUaYG5aKAXJpj6JUSfMBCOD9v1w";
            client.HttpUtils.HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
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