using System.Linq;
using System.Threading.Tasks;
using Nodes.API.Enums;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using static System.Console;

namespace ConsoleApplication
{
    public class FSP: UserRole
    {
        public FSP(NodesClient client = null) : base(client)
        {
        }

        public async Task CreateAssets()
        {
            WriteLine("Setting up assets");

            // Locate acceptable asset types: 
            var assetTypes = await Client.AssetTypes.GetByTemplate();

            // Locate the grid node where our assets are stored: 
            // TODO: Use mpid instead? 
//            var locations = await client.GridLocations.GetByTemplate();
//            var node = locations.Items.FirstOrDefault() ?? throw new Exception("No grid location available for trade");

            const string mpid = "12345678910";
            var node = await Client.GridNodes.Create(new GridNode
            {
                SuppliedByBrpOrganizationId = "23", 
                MeterPointId = "asdf",
                OperatedByDsoOrganizationId = "123",
            });


            WriteLine("creating assets...");
            var asset1 = await Client.Assets.Create(new Asset
            {
                Name = "asset1",
                AssetTypeId = assetTypes.Items.First().Id,
                OperatedByOrganizationId = Organization.Id, 
                GridNodeId = node.Id,
            });

            var asset2 = await Client.Assets.Create(new Asset
            {
                Name = "asset1",
                AssetTypeId = assetTypes.Items.First().Id,
                OperatedByOrganizationId = Organization.Id, 
                GridNodeId = node.Id,
            });

            WriteLine($"Assets {asset1}, {asset2} were registered. Awaiting approval by DSO. ");
        }

        public async Task CreatePortfolio()
        {
            WriteLine( "Creating a portfolio with all approved assets");
            var assets = await Client.Assets.GetByTemplate(new Asset
            {
                Status    = "Active", 
                OperatedByOrganizationId = Organization.Id,
            });
            var assetPortfolio = await Client.AssetPortfolios.Create(new AssetPortfolio
            {
                Name = "Asset portfolio 1",
                ManagedByOrganizationId = Organization.Id,
            });
            foreach (var asset in assets.Items)
            {
                await Client.AssetPortfolios.AddAssetToPortfolio(assetPortfolio.Id, asset.Id);
            }

            WriteLine($"{assets.Items.Count} assets were added to asset portfolio {assetPortfolio.Id}.");
        }

        // TODO: Add base line

        public async Task PlaceSellOrder()
        {
            WriteLine("placing sell order... ");

            var assetPortfolios = await Client.AssetPortfolios.GetByTemplate(new AssetPortfolio{ManagedByOrganizationId = Organization.Id});
            var locations = await Client.GridLocations.GetByTemplate();
            var location = locations.Items.First();

            var order = await Client.Orders.Create(new Order
            {
                Quantity = 1000,
                RebalancePrice = 100,
                FlexMarginPrice = 200,
                UnitPrice = 300,
                Side = OrderSide.Sell,
                FillType = FillType.Normal,
                AssetPortfolioId = assetPortfolios.Items.First().Id,
                GridNodeId = location.GridNodeId,
//                GridNodeId = location.GridNodeId, // TODO: This can be validated and calculated by NODES. Make it optional / illegal? 
//                PeriodFrom = DateTimeOffset.Now, 
            });
            WriteLine($"Sell order {order.Id} created");
        }
    }
}