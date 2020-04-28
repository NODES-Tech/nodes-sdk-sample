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

            const string mpid = "12345678910";

            WriteLine("creating assets...");
            var a = new Asset
            {
                Name = "asset1",
                AssetTypeId = assetTypes.Items.First().Id,
                OperatedByOrganizationId = Organization?.Id,
                RampUpRate = 1, RampDownRate = 1,
            };
            var asset1 = await Client.Assets.Create(a);

            var asset2 = await Client.Assets.Create(new Asset
            {
                Name = "asset2",
                AssetTypeId = assetTypes.Items.First().Id,
                OperatedByOrganizationId = Organization?.Id,
                RampUpRate = 1, RampDownRate = 1,
            });

            var asset3 = await Client.Assets.Create(new Asset
            {
                Name = "asset3",
                AssetTypeId = assetTypes.Items.First().Id,
                OperatedByOrganizationId = Organization?.Id,
                RampUpRate = 1, RampDownRate = 1,
            });

            var assets = new[] {asset1, asset2, asset3};

            var gridNodes = await Client.GridNodes.GetByTemplate();

            // Assign assets to a MPID
            foreach (var asset in assets)
            {
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

            WriteLine($"Assets {string.Join(", ", assets.Select(a => a.Id).ToArray())} were registered. Awaiting approval by DSO. ");
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
            var assetPortfolio = assetPortfolios.Items.First();
            var locations = await Client.GridLocations.GetByTemplate();
            var location = locations.Items.First();
            var markets = await Client.Markets.GetByTemplate();
            var market = markets.Items.First();

            var now = DateTimeOffset.UtcNow;
            now = now.Subtract(TimeSpan.FromMilliseconds(now.Millisecond));
            now = now.Subtract(TimeSpan.FromSeconds(now.Second));
            now = now.Subtract(TimeSpan.FromMinutes(now.Minute));


            var order = await Client.Orders.Create(new Order
            {
                AssetPortfolioId = assetPortfolio.Id,
                GridNodeId = location.GridNodeId,
                OwnerOrganizationId = Organization.Id,
                MarketId = market.Id,
                Side = OrderSide.Sell,
                FillType = FillType.Normal,
                PriceType = PriceType.Limit,
                RegulationType = RegulationType.Down,
                QuantityType = QuantityType.Power,
                Quantity = 1000,
                RebalancePrice = 100,
                FlexMarginPrice = 200,
                UnitPrice = 300,
                PeriodFrom = now,
                PeriodTo = now.AddHours(2),
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
            await ShowUsers();
            await ShowTrades();
            await ShowPortfolios();
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
                Created = new DateTimeRange(DateTimeOffset.UtcNow.AddHours(-24), null),
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
                });
            WriteLine($"Number of asset portfolios:  {portfolioRes.NumberOfHits}");
            portfolioRes.Items.ForEach(i => WriteLine($"  Asset portfolio: {i.Id} {i.Name}"));
        }

        private async Task ShowUsers()
        {
            var userRes = await Client.Users.GetByTemplate();
            WriteLine($"Number of users:  {userRes.NumberOfHits}");
            userRes.Items.ForEach(i => WriteLine("  User: " + i));
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