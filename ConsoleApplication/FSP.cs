using System;
using System.Linq;
using System.Threading.Tasks;
using Nodes.API.Enums;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using Nodes.API.Queries;
using Nodes.API.Support;
using static System.Console;
using static Nodes.API.Enums.OrderCompletionType;

// ReSharper disable PossibleUnintendedReferenceComparison

namespace ConsoleApplication
{
    public class FSP : UserRole
    {
        public FSP(NodesClient client) : base(client)
        {
        }

        public async Task CreateAssets()
        {
            WriteLine("Setting up assets");

            // Locate acceptable asset types: 
            var assetTypes = await Client.AssetTypes.GetByTemplate();


            var assetNames = Enumerable.Range(1, 3).Select(i => "asset" + i).ToArray();


            WriteLine($"creating {assetNames.Length} assets (if they dont' exist yet)...");
            var assets = assetNames
                .Select( name =>  CreateAssetIfNotExists(name, assetTypes.Items.FirstOrDefault()).GetAwaiter().GetResult())
                .ToArray();


            WriteLine($"Assets {string.Join(", ", assets.Select(a => a.Id).ToArray())} were registered. Awaiting approval by DSO. ");
        }

        public async Task AssignAssetsToGrid()
        {
            var assets = await Client.Assets.GetByTemplate(new Asset {OperatedByOrganizationId = Organization.Id});
            var gridNodes = await Client.GridNodes.GetByTemplate();
            const string mpid = "12345678910";
            
            // Assign assets to a MPID
            foreach (var asset in assets.Items)
            {
                // TODO: Check that they are not already assigned to a grid node
                await Client.AssetGridAssignments.Create(new AssetGridAssignment
                {
                    AssetId = asset.Id,
                    ManagedByOrganizationId = Organization?.Id,
                    OperatedByOrganizationId = Organization?.Id,
                    SuppliedByOrganizationId = Organization?.Id,
                    MPID = mpid,
                    GridNodeId = gridNodes.Items.First().Id,
                });
            }
        }

        private async Task<Asset> CreateAssetIfNotExists(string name, AssetType type)
        {
            var res = await Client.Assets.GetByTemplate(new Asset {Name = name, OperatedByOrganizationId = Organization.Id});
            return res.Items.Any()
                ? res.Items.Single()
                : await Client.Assets.Create(new Asset
                {
                    Name = name,
                    AssetTypeId = type.Id,
                    OperatedByOrganizationId = Organization.Id,
                    RampUpRate = 1, RampDownRate = 1,
                });
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
                RenewableType = RenewableType.Renewable,
                MinRampDownRate = 1, MinRampUpRate = 2, MaxRampDownRate = 3, MaxRampUpRate = 4,
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
            var assetPortfolio = assetPortfolios.Items.FirstOrDefault() ?? throw new Exception($"No portfolios found for org {Organization?.Id}");
            var locations = await Client.GridLocations.GetByTemplate();
            var location = locations.Items.FirstOrDefault();
            var markets = await Client.Markets.GetByTemplate();
            // var market = markets.Items.First();
            var market = markets.Items.Single(m => m.Name.StartsWith("Agder")); // TODO: Fix this hardcoding. 

            var now = DateTimeOffset.UtcNow;
            now = now.Subtract(TimeSpan.FromMilliseconds(now.Millisecond));
            now = now.Subtract(TimeSpan.FromSeconds(now.Second));
            now = now.Subtract(TimeSpan.FromMinutes(now.Minute));

            var start = now.AddHours(2);
            var end = now.AddHours(2).AddSeconds(market.MinimumBlockSizeInSeconds);

            var order = await Client.Orders.Create(new Order
            {
                AssetPortfolioId = assetPortfolio.Id,
                GridNodeId = location.GridNodeId,
                OwnerOrganizationId = Organization?.Id,
                MarketId = market.Id,
                Side = OrderSide.Sell,
                PriceType = PriceType.Limit,
                RegulationType = RegulationType.Down,
                QuantityType = QuantityType.Power,
                Quantity = (decimal?) 13.3,
                RebalancePrice = (decimal?) 13.3,
                FlexMarginPrice = (decimal?) 26.6,
                UnitPrice = null, // Leave this out for Power markets
                FillType = FillType.Normal,
                ValidTo = start,
                PeriodFrom = start,
                PeriodTo = end,
                BlockSizeInSeconds = market.MinimumBlockSizeInSeconds,
                MaxBlocks = 1, AdjacentBlocks = 1, RestBlocks = 0,
            });
            WriteLine($"Sell order {order.Id} created");
        }

        public async Task<SearchResult<Order>> GetCurrentActiveOrders()
        {
            WriteLine("fetching list of activated orders");
            var options = new SearchOptions
            {
                OrderBy = {"Created desc"},
                Embeddings = {"assetportfolio"},
                Take = 100,
            };

            var orderTemplate = new Order
            {
                Status = Status.Completed,
                // Side = OrderSide.Sell,
            };
            return await Client.Orders.GetByTemplate(orderTemplate, options);
        }

        public static bool IsActive(Order o) =>
            (o.CompletionType == Filled || o.CompletionType == Killed)
            && o.PeriodFrom <= DateTimeOffset.UtcNow
            && o.PeriodTo >= DateTimeOffset.UtcNow;

        public async Task ClearOrders()
        {
            WriteLine("Clear orders");

            var orders = await GetCurrentActiveOrders();
            var activeOrders = orders.Items.Where(IsActive).ToList();
            foreach (var order in activeOrders)
            {
                await Client.Orders.Delete(order.Id);
            }

            WriteLine($"{activeOrders.Count} items found and deleted");
        }

        public async Task GetInfo()
        {
            WriteLine("List FSP info");

            await ShowOrders();
            await ShowTrades();
            await ShowPortfolios();
            await ShowAssets();
        }

        private async Task ShowTrades()
        {
            var tradeTemplate = new Trade();
            var searchOptions = new SearchOptions
            {
                Take = 100,
                Embeddings = {Relations.Organization, Relations.AssetPortfolio, Relations.GridNode},
                OrderBy = {nameof(Trade.LastModified)}
            };
            var search = new TradeSearch
            {
                PeriodFrom = new DateTimeRange(DateTimeOffset.UtcNow.AddHours(-24), null),
            };
            var tradeRes = await Client.Trades.GetByTemplate(tradeTemplate, searchOptions, search);
            WriteLine($"Number of trades:  {tradeRes.Items.Count} / {tradeRes.NumberOfHits}");
            foreach (var trade in tradeRes.Items)
            {
                var org = tradeRes.Embedded.SingleOrDefault(x => x.Id == trade.OwnerOrganizationId);
                var gn = tradeRes.Embedded.OfType<GridNode>().SingleOrDefault(x => x.Id == trade.GridNodeId);
                var ap = tradeRes.Embedded.SingleOrDefault(x => x.Id == trade.AssetPortfolioId);
                // NB: Asset portfolio is not relevant/visible for BUY orders. 
                WriteLine(
                    $"  Trade: {trade.Status} {trade.Side} {trade.RegulationType} {trade.Quantity}MWh@{trade.UnitPrice} EUR  " +
                    $"{trade.PeriodFrom}-{trade.PeriodTo}ap={ap}, §org={org}, gn={gn}, mpid={gn.MPID}");
            }
        }

        private async Task ShowPortfolios()
        {
            var portfolioRes =
                await Client.AssetPortfolios.GetByTemplate(new AssetPortfolio
                {
                    ManagedByOrganizationId = Organization.Id,
                }, new SearchOptions()
                {
                    Embeddings =
                    {
                        Relations.AssetPortfolioAssignment,
                        SearchUtil.CombineEmbedSpecs(Relations.AssetPortfolioAssignment, Relations.AssetGridAssignment)
                    }
                });
            WriteLine($"Number of asset portfolios:  {portfolioRes.NumberOfHits}");
            portfolioRes.Items.ForEach(i => WriteLine($"  Asset portfolio: {i.Id} {i.Name}"));
            portfolioRes.Embedded.OfType<AssetGridAssignment>().ToList()
                .ForEach((assetGridAssignment) =>
                    WriteLine(
                        $"Asset {assetGridAssignment.AssetId} has asset grid assignment {assetGridAssignment.Id} with mpid:  {assetGridAssignment.MPID}"));
        }

        private async Task ShowAssets()
        {
            var assets = await Client.Assets.GetByTemplate(new Asset {OperatedByOrganizationId = Organization.Id});
            WriteLine($"Number of assets:  {assets.NumberOfHits}");
            assets.Items.ForEach(i => WriteLine($"  Asset: {i.Id} {i.Name}"));
        }

        public async Task ShowOrders()
        {
            var orders = await Client.Orders.GetByTemplate(new Order(),
                new SearchOptions
                {
                    Embeddings = {"organization"},
                    Take = 10,
                    OrderBy = {"lastmodified desc"}
                });
            WriteLine($"Number of orders:  {orders.NumberOfHits}");
            orders.Items.ForEach(i =>
            {
                var org = (Organization) orders.Embedded.Single(x => x.Id == i.OwnerOrganizationId);
                WriteLine(
                    $"  {i.Side} Order: {i.Status} {i.RegulationType} {i.PeriodFrom}-{i.PeriodTo}, org={org.Id} {org.Name}"
                );
            });
        }
    }
}