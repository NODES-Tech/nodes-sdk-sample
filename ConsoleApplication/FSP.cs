using System.Linq;
using System.Threading.Tasks;
using Nodes.API.Enums;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using static System.Console;

namespace ConsoleApplication
{
    public class FSP : UserRole
    {
        public FSP(NodesClient? client = null) : base(client)
        {
        }

        public async Task CreateAssets()
        {
            WriteLine("Setting up assets");

            // Locate acceptable asset types: 
            var assetTypes = await Client.AssetTypes.GetByTemplate();

            const string mpid = "12345678910";

            WriteLine("creating assets...");
            var asset1 = await Client.Assets.Create(new Asset
            {
                Name = "asset1",
                AssetTypeId = assetTypes.Items.First().Id,
                OperatedByOrganizationId = Organization?.Id,
            });

            var asset2 = await Client.Assets.Create(new Asset
            {
                Name = "asset2",
                AssetTypeId = assetTypes.Items.First().Id,
                OperatedByOrganizationId = Organization?.Id,
            });

            var asset3 = await Client.Assets.Create(new Asset
            {
                Name = "asset3",
                AssetTypeId = assetTypes.Items.First().Id,
                OperatedByOrganizationId = Organization?.Id,
            });

            var assets = new[] {asset1, asset2, asset3};

            // Assign assets to a MPID
            foreach (var asset in assets)
            {
                await Client.AssetGridAssignments.Create(new AssetGridAssignment
                {
                    AssetId = asset.Id,
                    MPID = mpid,
                });
            }

            WriteLine($"Assets {string.Join(", ", assets.Select(a=>a.Id).ToArray())} were registered. Awaiting approval by DSO. ");
        }

        public async Task CreatePortfolio()
        {
            WriteLine("Creating a portfolio with all approved assets");
            var assets = await Client.Assets.GetByTemplate(new Asset
            {
                Status = Status.Active,
                OperatedByOrganizationId = Organization?.Id,
            });
            var portfolio = await Client.AssetPortfolios.Create(new AssetPortfolio
            {
                Name = "Asset portfolio 1",
                ManagedByOrganizationId = Organization?.Id,
            });
            foreach (var asset in assets.Items)
            {
                var assignments = await Client.AssetGridAssignments.GetByTemplate(new AssetGridAssignment {AssetId = asset.Id});
                var assignment = assignments.Items.Single();
                await Client.AssetPortfolioAssignments.Create(new AssetPortfolioAssignment
                {
                    AssetPortfolioId = portfolio.Id, 
                    AssetGridAssignmentId = assignment.Id,
                });
            }

            WriteLine($"{assets.Items.Count} assets were added to asset portfolio {portfolio.Id}.");
        }

        // TODO: Add base line

        public async Task PlaceSellOrder()
        {
            WriteLine("placing sell order... ");

            var assetPortfolios = await Client.AssetPortfolios.GetByTemplate(new AssetPortfolio {ManagedByOrganizationId = Organization?.Id});
            var assetPortfolio = assetPortfolios.Items.First(); 
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
                AssetPortfolioId = assetPortfolio.Id,
                GridNodeId = location.GridNodeId,
            });
            WriteLine($"Sell order {order.Id} created");
        }
    }
}