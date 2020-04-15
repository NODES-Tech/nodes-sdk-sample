using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nodes.API.Enums;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using Nodes.API.Queries;
using Nodes.API.Support;
using static System.Console;

namespace ConsoleApplication
{
    public class DSO : UserRole
    {
        public async Task CreateGridNodes()
        {
            // await DisplayGridNodeTree();
            // return; 
            
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
            
            
            WriteLine("Created the following grid node structure: ");
            await DisplayGridNodeTree();
            

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

        public async Task DisplayGridNodeTree()
        {
            var all = new SearchOptions(100);
            var gridNodes = await Client.GridNodes.GetByTemplate(null, all);
            var gridLinks = await Client.GridNodeLinks.GetByTemplate(null, all);
            var gridLocations = await Client.GridLocations.GetByTemplate(null, all);

            DisplayGridNodeTree(gridNodes.Items, gridLinks.Items, gridLocations.Items);
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


        public static void DisplayGridNodeTree(List<GridNode> nodes, List<GridNodeLink> links, List<GridLocation> gridLocationsItems)
        {
            var roots = BuildForest(nodes, links);

            foreach (var root in roots)
            {
                root.Traverse((gn, d) =>
                {
                    var prefix = Enumerable.Range(0, d).Select(_ => " ").JoinToString("");
                    var gridLocation = gridLocationsItems.Any(gl => gl.GridNodeId == gn.Id) ? "(Gridlocation)" : "";
                    WriteLine($"{prefix}{gn.Name} {gridLocation}");
                });
                WriteLine();
            }
        }

        public static List<TreeNode<GridNode>> BuildForest(List<GridNode> nodes, List<GridNodeLink> links) =>
            nodes
                .Where(node => links.All(gl => gl.TargetGridNodeId != node.Id))
                .Select(gn => new TreeNode<GridNode>(gn))
                .Select(gn => AddChildrenRecursively(gn, nodes, links))
                .ToList();

        public static TreeNode<GridNode> AddChildrenRecursively(TreeNode<GridNode> node, List<GridNode> nodes, List<GridNodeLink> links)
        {
            links
                .Where( l => nodes.Any(n => n.Id == l.TargetGridNodeId)) // Exclude some incorrect data, should not be needed normally
                .Where(l => l.SourceGridNodeId == node.Value.Id)
                .Select(l => nodes.SingleOrDefault(n => n.Id == l.TargetGridNodeId) ?? throw new Exception("Link target node not found: " + l.TargetGridNodeId))
                .Select(node.AddChild)
                .ToList()
                .ForEach(child => AddChildrenRecursively(child, nodes, links));
            return node;
        }


        public static void TestTree()
        {
            var nodes = new[] {"A", "A.A", "A.A.A", "A.A.B", "A.B", "B"}
                .Select(s => new GridNode {Id = s, Name = s});

            var links = new List<GridNodeLink>
            {
                new GridNodeLink {SourceGridNodeId = "A", TargetGridNodeId = "A.A"},
                new GridNodeLink {SourceGridNodeId = "A", TargetGridNodeId = "A.B"},
                new GridNodeLink {SourceGridNodeId = "A.A", TargetGridNodeId = "A.A.A"},
                new GridNodeLink {SourceGridNodeId = "A.A", TargetGridNodeId = "A.A.B"},
            };

            var locations = new[] {"A", "A.A", "A.A.B"}
                .Select(s => new GridLocation {Id = s, GridNodeId = s});

            DisplayGridNodeTree(nodes.ToList(), links.ToList(), locations.ToList());
        }
    }
}