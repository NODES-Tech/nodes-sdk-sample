using System;
using System.Linq;
using System.Threading.Tasks;
using Nodes.API.Enums;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using Nodes.API.Queries;
using Nodes.API.Support;
using Nodes.API.Support.ExtensionMethods;
using static System.Console;
using static Nodes.API.Enums.OrderCompletionType;

// ReSharper disable PossibleUnintendedReferenceComparison

namespace ConsoleApplication;

public class FSP : UserRole
{
    public const string AssetNamePrefix = DSO.SdkPrefix + " test-asset-";
    public const string AssetPortfolioNamePrefix = DSO.SdkPrefix + " test-assetportfolio-";

    public static readonly DateTimeOffset TimeOfTrade = DateTimeOffset.UtcNow.Date.AddHours(26).ToUniversalTime();
        
        
    public FSP(NodesClient client) : base(client)
    {
    }

    public async Task CreateAssets()
    {
        WriteLine("Setting up assets");

        // Locate acceptable asset types: 
        var assetTypes = await Client.AssetTypes.GetByTemplate();


        var assetNames = Enumerable.Range(1, 3).Select(i => AssetNamePrefix + i).ToArray();


        WriteLine($"creating {assetNames.Length} assets (if they dont' exist yet)...");
        var assets = assetNames
            .Select(name => CreateAssetIfNotExists(name, assetTypes.Items.FirstOrDefault()).GetAwaiter().GetResult())
            .ToArray();

        WriteLine($"Assets {string.Join(", ", assets.Select(a => a.Name).ToArray())} are registered. Awaiting approval by DSO. ");
    }

    public async Task AssignAssetsToGrid()
    {
        var assets = await Client.Assets.GetByTemplate(new Asset { OperatedByOrganizationId = Organization.Id });
        const string mpid = "12345678910";

        // Assign assets to a MPID
        foreach (var asset in assets.Items)
        {
            var agas = await Client.AssetGridAssignments.GetByTemplate(new AssetGridAssignment
            {
                AssetId = asset.Id,
            });
            if (agas.Items.Any())
            {
                WriteLine("Asset " + asset + " already had an asset grid assignment");
                continue;
            }

            WriteLine("Assigning mpid to " + asset);
            await Client.AssetGridAssignments.Create(new AssetGridAssignment
            {
                AssetId = asset.Id,
                ManagedByOrganizationId = Organization?.Id,
                OperatedByOrganizationId = Organization?.Id,
                SupplierOrganizationId = Organization?.Id,
                MPID = mpid,
                // GridNodeId = gridNode.Id,
            });
        }

        WriteLine("---");
    }

    private async Task<Asset> CreateAssetIfNotExists(string name, AssetType type)
    {
        var res = await Client.Assets.GetByTemplate(new Asset { Name = name, OperatedByOrganizationId = Organization.Id });
        if (res.Items.Any())
        {
            WriteLine("Asset already exists: " + name);
            return res.Items.Single();
        }
        else
        {
            WriteLine("Creating asset " + name);
            return await Client.Assets.Create(new Asset
            {
                Name = name,
                AssetTypeId = type.Id,
                OperatedByOrganizationId = Organization.Id,
                RampUpRate = 1, RampDownRate = 1,
            });
        }
    }

    public async Task CreatePortfolio()
    {
        WriteLine("Creating a portfolio with all approved assets (if not exists already)");
        var assets = await Client.Assets.GetByTemplate(new Asset
        {
            Status = Status.Active,
            OperatedByOrganizationId = Organization?.Id,
        });

        var portfolioName = AssetPortfolioNamePrefix + "1";
        var portfolios = await Client.AssetPortfolios.GetByTemplate(new AssetPortfolio
        {
            Name = portfolioName,
        });

        AssetPortfolio portfolio;
        if (portfolios.Items.Any())
        {
            portfolio = portfolios.Items.Single();
        }
        else
        {
            portfolio = await Client.AssetPortfolios.Create(new AssetPortfolio
            {
                Name = portfolioName,
                ManagedByOrganizationId = Organization?.Id,
                RenewableType = RenewableType.Renewable,
                MinRampDownRate = 1, MinRampUpRate = 2, MaxRampDownRate = 3, MaxRampUpRate = 4,
            });
        }

        foreach (var asset in assets.Items)
        {
            var assignments = await Client.AssetGridAssignments.GetByTemplate(new AssetGridAssignment
            {
                AssetId = asset.Id,
                Status = Status.Active,
            });
            var assignment = assignments.Items.SingleOrDefault() ?? throw new ArgumentException("No active grid assignments found");
            WriteLine("  asset " + asset + " has asset active grid assignment " + assignment.MPID);

            var apas = await Client.AssetPortfolioAssignments.GetByTemplate(new AssetPortfolioAssignment
            {
                AssetGridAssignmentId = assignment.Id,
            });
            if (apas.Items.Any())
            {
                WriteLine("  asset " + asset + " is already part of " + portfolio);
            }
            else
            {
                await Client.AssetPortfolioAssignments.Create(new AssetPortfolioAssignment
                {
                    AssetPortfolioId = portfolio.Id,
                    AssetGridAssignmentId = assignment.Id,
                });
                WriteLine("Added " + asset + " to portfolio " + portfolio);
            }
        }

        portfolio = await Client.AssetPortfolios.GetById(portfolio.Id);
            
        WriteLine($"{assets.Items.Count} assets are part of asset portfolio {portfolio.Id}. Portfolio status=" + portfolio.Status +
                  ", grid node =" + portfolio.GridNodeId);
    }

    public async Task CreateBaselines()
    {
        var assetPortfolios = await Client.AssetPortfolios.GetByTemplate(new AssetPortfolio
        {
            ManagedByOrganizationId = Organization?.Id,
            Status = Status.Active,
        });
        var assetPortfolio = assetPortfolios.Items
            .FirstOrDefault(ap => ap.GridNodeId != null) ?? throw new Exception($"No portfolios found for org {Organization?.Id}");

        var baseline = await Client.BaseLineIntervals.Create(new BaseLineInterval
        {
            AssetPortfolioId = assetPortfolio.Id,
            PeriodFrom = TimeOfTrade.AddHours(-1),
            PeriodTo = TimeOfTrade.AddHours(+1),
            QuantityType = QuantityType.Power,
            Quantity = 10,
            BatchReference = DSO.SdkPrefix + ";" + TimeOfTrade.ToIso(),
        });
        WriteLine( "Baseline added/updated for " + assetPortfolio);
    }

    public async Task PlaceSellOrder()
    {
        var assetPortfolios = await Client.AssetPortfolios.GetByTemplate(new AssetPortfolio
        {
            ManagedByOrganizationId = Organization?.Id,
            Status = Status.Active,
        });
        var assetPortfolio = assetPortfolios.Items
            .FirstOrDefault(ap => ap.GridNodeId != null) ?? throw new Exception($"No portfolios found for org {Organization?.Id}");

        if (assetPortfolio.GridNodeId == null)
            throw new Exception(" Active portfolio without grid node??");

        WriteLine("placing sell order using portfolio " + assetPortfolio + " on grid node " + assetPortfolio.GridNodeId);


        var markets = await Client.Markets.GetByTemplate(new Market
        {
            Name = DSO.MarketName,
        });
        var market = markets.Items.SingleOrDefault() ?? throw new ArgumentException("No such market: " + DSO.MarketName);
            

        var end = TimeOfTrade.AddSeconds(market.MinimumBlockSizeInSeconds);

        var order = await Client.Orders.Create(new Order
        {
            AssetPortfolioId = assetPortfolio.Id,
            // GridNodeId = location.GridNodeId,
            OwnerOrganizationId = Organization?.Id,
            MarketId = market.Id,
            Side = OrderSide.Sell,
            PriceType = PriceType.Limit,
            RegulationType = RegulationType.Down,
            QuantityType = QuantityType.Power,
            Quantity = (decimal?)13.3,
            // RebalancePrice = (decimal?) 13.3,
            // FlexMarginPrice = (decimal?) 26.6,
            // UnitPrice = null, // Leave this out for Power markets
            UnitPrice = (decimal?)26.6,
            FillType = FillType.Normal,
            ValidTo = TimeOfTrade,
            PeriodFrom = TimeOfTrade,
            PeriodTo = end,
            BlockSizeInSeconds = market.MinimumBlockSizeInSeconds,
            MaxBlocks = 1, MinAdjacentBlocks = 1, RestBlocks = 0,
        });
        WriteLine($"Sell order {order.Id} posted");
    }

    public async Task<SearchResult<Order>> GetCurrentActiveOrders()
    {
        WriteLine("fetching list of activated orders");
        var options = new SearchOptions
        {
            OrderBy = { "Created desc" },
            Embeddings = { "assetportfolio" },
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
        var searchOptions = new SearchOptions
        {
            Take = 100, // This is the default value
            Embeddings = { Relations.Organization, Relations.AssetPortfolio, Relations.GridNode },
            OrderBy = { nameof(Trade.LastModified) }
        };
        var search = IFilter.Filters(
            IFilter.KVP(nameof(Trade.PeriodFrom), new DateTimeRange(DateTimeOffset.UtcNow.AddHours(-24), null)));
        var tradeRes = await Client.Trades.Search(search, searchOptions);
        WriteLine($"Number of trades:  {tradeRes.Items.Count} / {tradeRes.NumberOfHits}");
        foreach (var trade in tradeRes.Items)
        {
            var org = tradeRes.Embedded.SingleOrDefault(x => x.Id == trade.OwnerOrganizationId);
            var gn = tradeRes.Embedded.OfType<GridNode>().Single(x => x.Id == trade.GridNodeId);
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
        WriteLine("---");
    }

    private async Task ShowAssets()
    {
        var assets = await Client.Assets.GetByTemplate(new Asset { OperatedByOrganizationId = Organization.Id });
        WriteLine($"Number of assets:  {assets.NumberOfHits}");
        assets.Items.ForEach(i => WriteLine($"  Asset: {i.Id} {i.Name}"));
        WriteLine("---");
    }

    public async Task ShowOrders()
    {
        var orders = await Client.Orders.GetByTemplate(new Order(),
            new SearchOptions
            {
                Embeddings = { "organization" },
                Take = 10,
                OrderBy = { "lastmodified desc" }
            });
        WriteLine($"Number of orders:  {orders.NumberOfHits}");
        orders.Items.ForEach(i =>
        {
            var org = (Organization)orders.Embedded.Single(x => x.Id == i.OwnerOrganizationId);
            WriteLine(
                $"  {i.Side} Order: {i.Status} {i.RegulationType} {i.PeriodFrom}-{i.PeriodTo}, org={org.Id} {org.Name}"
            );
        });
    }
}