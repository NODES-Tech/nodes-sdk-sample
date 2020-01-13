using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nodes.API.Enums;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using static System.Console;

namespace ConsoleApplication
{
    public class DSO: UserRole
    {
        public DSO(NodesClient? client ) : base(client)
        {
        }

        public DSO()
        {
        }

        public async Task CreateGridNodes()
        {
            WriteLine("Setting up a gride node structure");
            var rootNode = await Client.GridNodes.Create(new GridNode
            {
                Name = "DSORoot",
                OperatedByOrganizationId = Organization?.Id,
            });

            var substation = await Client.GridNodes.AddLinkedGridNode(rootNode.Id, new GridNode
            {
                Name = "Substation 1",
                OperatedByOrganizationId = Organization?.Id,
            });

            var secondarySubstation = await Client.GridNodes.AddLinkedGridNode(substation.Id, new GridNode
            {
                Name = "Secondary Substation 1",
                OperatedByOrganizationId = Organization?.Id,
            });

            WriteLine("creating a flexibility POWER market... ");
            var market = await Client.Markets.Create(new Market
            {
                QuantityType = QuantityType.Power,
                OwnerOrganizationId = Organization?.Id,
            });

            WriteLine("marking congested nodes / Creating a grid location / open order books");
            var gridLocation = await Client.GridNodes.OpenGridNodeForTrade(substation.Id, market.Id);

            WriteLine($"Done! Awaiting orders on grid node {substation.Id}, market id {market.Id}, grid location {gridLocation.Id}");
        }

        public async Task PlaceBuyOrder()
        {
            // Locate our grid location: 
            var locations = await Client.GridLocations.GetByTemplate();
            var node = locations.Items.FirstOrDefault() ?? throw new Exception("No grid location available for trade");

            // Find sell orders - not required. We do this in this example in order 
            // to find a suitable price and see what is available in the market. 
            var orders = await Client.Orders.GetByTemplate(new Order
            {
                GridNodeId = node.GridNodeId,
            });

            var sellOrder = orders.Items.FirstOrDefault() ?? throw new Exception($"No sell orders available on gride node {node.GridNodeId}");

            // In this case, we create a 
            var buyOrder = await Client.Orders.Create(new Order
            {
                MarketId = sellOrder.MarketId,
                GridNodeId = node.GridNodeId,
                Quantity = sellOrder.Quantity,
                QuantityType = sellOrder.QuantityType,
                Side = OrderSide.Buy,

                // Here we specify a price, which is the upper limit of what we are willing to pay. 
                // An alternative is to specify price type Market. In that case UnitPrice is not used
                // and there will be no limit to the price we are willing to pay. 
                UnitPrice = sellOrder.UnitPrice,
                PriceType = PriceType.Limit
            });

            WriteLine($"Buy order {buyOrder.Id} created successfully");

            Thread.Sleep(1000);

            // Check if the order has been matched - that will cause 
            // a trade to be created. 
            var trades = await Client.Trades.GetByTemplate(new Trade
            {
                GridNodeId = node.GridNodeId,
            });

            WriteLine($"{trades.NumberOfHits} trades found. ");
            foreach (var trade in trades.Items)
            {
                WriteLine(trade.Id);
            }
        }

        public async Task ApproveAssets()
        {
            var unapprovedAssets = await Client.Assets.GetByTemplate(new Asset {Status = Status.Pending});
            foreach (var asset in unapprovedAssets.Items)
            {
                asset.Status = Status.Active;
                await Client.Assets.Update(asset);
            }
            
            WriteLine($"{unapprovedAssets.Items.Count} assets were approved / activated");
        }
    }
}