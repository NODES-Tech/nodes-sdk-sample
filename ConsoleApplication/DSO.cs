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
    public class DSO
    {
        public async Task CreateGridNodes()
        {
            WriteLine("Setting up a gride node structure");

            var client = new NodesClient(Program.APIUrl);

            // ... authenticate 
            WriteLine("authenticating... ");
            var user = await client.Users.GetCurrentUser() ?? throw new Exception("Authentication: TBA");

            var memberships = await client.Memberships.GetByTemplate(new Membership {UserId = user.Id});
            var subscription = await client.Subscriptions.GetById(memberships.Items.First().SubscriptionId);
            var organization = await client.Organizations.GetById(subscription.OwnerOrganizationId);

            // Register topology: 
            WriteLine("creating grid nodes... ");
            var rootNode = await client.GridNodes.Create(new GridNode
            {
                Name = "DSORoot",
                GridNodeType = "DSO Root Node",
                OperatedByDsoOrganizationId = organization.Id,
            });

            var substation = await client.GridNodes.Create(new GridNode
            {
                Name = "Substation 1",
                GridNodeType = "Substation",
                OperatedByDsoOrganizationId = organization.Id,
                ParentGridNodeId = rootNode.Id,
            });

            var secondarySubstation = await client.GridNodes.Create(new GridNode
            {
                Name = "Secondary Substation 1",
                GridNodeType = "Secondary Substation",
                OperatedByDsoOrganizationId = organization.Id,
                ParentGridNodeId = substation.Id,
            });

            // Create a power market: 
            WriteLine("creating market... ");
            var market = await client.Markets.Create(new Market
            {
                Currency = "NOK",
                QuantityType = QuantityType.Power,
                OwnerOrganizationId = organization.Id,
            });

            // Mark congested areas: 
            WriteLine("marking congested nodes");
            var gridLocation = await client.GridNodes.OpenGridNodeForTrade(substation.Id, market.Id);

            WriteLine($"Done! Awaiting orders on grid node {substation.Id}, market id {market.Id}, grid location {gridLocation.Id}");
        }

        public async Task PlaceBuyOrder()
        {
            var client = new NodesClient(Program.APIUrl);

            // Locate our grid location: 
            var locations = await client.GridLocations.GetByTemplate();
            var node = locations.Items.FirstOrDefault() ?? throw new Exception("No grid location available for trade");

            // Find sell orders: 
            var orders = await client.Orders.GetByTemplate(new Order
            {
                GridNodeId = node.GridNodeId,
            });

            var sellOrder = orders.Items.FirstOrDefault() ?? throw new Exception($"No sell orders available on gride node {node.GridNodeId}");

            var buyOrder = await client.Orders.Create(new Order
            {
                Quantity = sellOrder.Quantity,
                QuantityType = sellOrder.QuantityType,
                Side = OrderSide.Buy,
                UnitPrice = sellOrder.UnitPrice,
                MarketId = sellOrder.MarketId,
            });

            WriteLine($"Buy order {buyOrder.Id} created successfully");

            Thread.Sleep(5000);
            var trades = await client.Trades.GetByTemplate(new Trade
            {
                GridNodeId = node.GridNodeId,
            });


            WriteLine($"{trades.NumberOfHits} trades found. ");
            foreach (var trade in trades.Items)
            {
                WriteLine(trade.Id);
            }
        }
    }
}