using System;
using System.Linq;
using System.Threading.Tasks;
using Nodes.API.Enums;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using static System.Console;

namespace ConsoleApplication
{
    public class FSP
    {
        public async Task AddAssets()
        {
            WriteLine("Setting up assets");

            var client = new NodesClient(Program.APIUrl);

            // ... authenticate 
            WriteLine("authenticating... ");
            var user = await client.Users.GetCurrentUser() ?? throw new Exception("Authentication: TBA");

            var memberships = await client.Memberships.GetByTemplate(new Membership {UserId = user.Id});
            var subscription = await client.Subscriptions.GetById(memberships.Items.First().SubscriptionId);
            var organization = await client.Organizations.GetById(subscription.OwnerOrganizationId);
            
            // Locate the grid node where our assets are stored: 
            var locations = await client.GridLocations.GetByTemplate();
            var node = locations.Items.FirstOrDefault() ?? throw new Exception("No grid location available for trade");

            // Locate an acceptable asset type: 
            var assetTypes = await client.AssetTypes.GetByTemplate(); 
            
            WriteLine( "creating assets...");
            var asset1 = await client.Assets.Create(new Asset
            {
                Name = "asset1", 
                AssetTypeId = assetTypes.Items.First().Id,
                GridNodeId = node.GridNodeId,
//                OperatedByOrganizationId = 
            });
            
            var asset2 = await client.Assets.Create(new Asset
            {
                Name = "asset1", 
                AssetTypeId = assetTypes.Items.First().Id,
                GridNodeId = node.GridNodeId,
            });


            var assetPortfolio = await client.AssetPortfolios.Create(new AssetPortfolio
            {
                Name = "Asset portfolio 1",
                ManagedByOrganizationId = organization.Id,
            });
            await client.AssetPortfolios.AddAssetToPortfolio(assetPortfolio.Id, asset1.Id);
            await client.AssetPortfolios.AddAssetToPortfolio(assetPortfolio.Id, asset2.Id);

            WriteLine($"Assets were added to asset portfolio {assetPortfolio.Id}. Awaiting approval by DSO. ");
        }


        public async Task PlaceSellOrder()
        {
            WriteLine("placing sell order... ");
            
            var client = new NodesClient(Program.APIUrl);
            var assetPortfolios = await client.AssetPortfolios.GetByTemplate();
            var locations = await client.GridLocations.GetByTemplate();
            var location = locations.Items.First();

            var order = await client.Orders.Create(new Order
            {
                Quantity = 1000,
                RebalancePrice = 100,
                FlexMarginPrice = 200,
                UnitPrice = 300,
                Side = OrderSide.Sell, 
                FillType = FillType.Normal, 
                AssetPortfolioId = assetPortfolios.Items.First().Id,
                GridNodeId = location.GridNodeId,
            });
            WriteLine($"Sell order {order.Id} created");
        }
    }
}