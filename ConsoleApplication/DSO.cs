using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nodes.API.Enums;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using Nodes.API.Support;
using static System.Console;

namespace ConsoleApplication
{
    public class DSO : UserRole
    {
        public const string RootName = "DSORoot3";
        public const string MarketName = "Demo market3";

        public DSO(NodesClient client) : base(client)
        {
        }

        public async Task CreateGridNodesIfNeeded()
        {
            var gridNodes = await Client.GridNodes.GetByTemplate(new GridNode { Name = RootName });
            if (gridNodes.Items.Any())
            {
                WriteLine("Using this grid hierarchy: ");
                await DisplayGridNodeTree(gridNodes.Items.Single());
            }
            else
            {
                WriteLine("Setting up a grid node structure");
                var priceAreas = await Client.PriceAreas.GetByTemplate();
                var priceArea = priceAreas.Items.First();

                WriteLine("Using price area " + priceArea);

                var rootNode = await Client.GridNodes.Create(new GridNode
                {
                    Name = RootName,
                    OperatedByOrganizationId = Organization.Id,
                    PriceAreaId = priceArea.Id,
                    Location = Coordinate.ParseSingleCoordinate("60, 10")
                });

                var substation = await Client.GridNodes.AddLinkedGridNode(rootNode.Id, new GridNode
                {
                    Name = "Substation 1",
                    OperatedByOrganizationId = Organization?.Id,
                    PriceAreaId = priceArea.Id,
                    Location = Coordinate.ParseSingleCoordinate("60, 11")
                });

                var secondarySubstation = await Client.GridNodes.AddLinkedGridNode(substation.Id, new GridNode
                {
                    Name = "Secondary Substation 1",
                    OperatedByOrganizationId = Organization?.Id,
                    PriceAreaId = priceArea.Id,
                    Location = Coordinate.ParseSingleCoordinate("60, 12")
                });


                WriteLine("Created the following grid node structure: ");
                await DisplayGridNodeTree(rootNode);


                WriteLine("creating a flexibility POWER market... ");
                var market = await Client.Markets.Create(new Market
                {
                    Name = MarketName,
                    CurrencyId = "NOK",
                    TimeZone = "CET",
                    QuantityType = QuantityType.Power,
                    OwnerOrganizationId = Organization?.Id,
                });

                WriteLine("marking congested nodes / Creating a grid location / open order books");
                var gridLocation = await Client.GridNodes.OpenGridNodeForTrade(substation.Id, market.Id);

                WriteLine($"Done! Awaiting orders on grid node {substation.Id}, market id {market.Id}, grid location {gridLocation.Id}");
            }
        }

        public async Task DisplayGridNodeTree(GridNode rootNode = null)
        {
            var tree = await Client.GridNodes.GetGrid();
            foreach (var root in tree.Where(n => rootNode == null || rootNode.Id == n.Id))
            {
                LogToConsole(root, 0);
            }
        }

        private void LogToConsole(TreeNode node, int level)
        {
            var prefix = new string[level].Select(c => "--").JoinToString("") + "> ";
            WriteLine(prefix + node.Name);
            foreach (var child in node.Children)
            {
                LogToConsole(child, level + 1);
            }
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
                OwnerOrganizationId = Organization.Id,
                QuantityType = sellOrder.QuantityType,
                FillType = FillType.Normal,
                Side = OrderSide.Buy,
                RegulationType = RegulationType.Down,

                BlockSizeInSeconds = sellOrder.BlockSizeInSeconds,
                MaxBlocks = 1, MinAdjacentBlocks = 1, RestBlocks = 0,

                // Here we specify a price, which is the upper limit of what we are willing to pay. 
                // An alternative is to specify price type Market. In that case UnitPrice is not used
                // and there will be no limit to the price we are willing to pay. 
                UnitPrice = sellOrder.UnitPrice,
                // FlexMarginPrice = sellOrder.FlexMarginPrice,
                // RebalancePrice = sellOrder.RebalancePrice,
                PriceType = PriceType.Limit,
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
            var unapprovedAssets = await Client.AssetGridAssignments.GetByTemplate(new AssetGridAssignment { Status = Status.Pending });
            WriteLine("Approving " + unapprovedAssets.Items.Count + " assets");
            var gridNodes = await Client.GridNodes.GetByTemplate(new GridNode { Name = RootName });
            var gridNode = gridNodes.Items.Single();
            foreach (var aga in unapprovedAssets.Items)
            {
                try
                {
                    aga.Status = Status.Active;
                    aga.GridNodeId = gridNode.Id;
                    await Client.AssetGridAssignments.Update(aga);
                    WriteLine("Approved asset grid assignment " + aga);
                }
                catch (Exception e)
                {
                    WriteLine("Failed to approve asset grid assignment " + aga);
                }
            }

            WriteLine($"{unapprovedAssets.Items.Count} assets were approved / activated");
        }


        // public static void DisplayGridNodeTree(List<GridNode> nodes, List<GridNodeLink> links, List<GridLocation> gridLocationsItems)
        // {
        //     var roots = BuildForest(nodes, links);
        //
        //     foreach (var root in roots)
        //     {
        //         root.Traverse((gn, d) =>
        //         {
        //             var prefix = Enumerable.Range(0, d).Select(_ => " ").JoinToString("");
        //             var gridLocation = gridLocationsItems.Any(gl => gl.GridNodeId == gn.Id) ? "(Gridlocation)" : "";
        //             WriteLine($"{prefix}{gn.Name} {gridLocation}");
        //         });
        //         WriteLine();
        //     }
        // }

        // public static List<TreeNode<GridNode>> BuildForest(List<GridNode> nodes, List<GridNodeLink> links) =>
        //     nodes
        //         .Where(node => links.All(gl => gl.TargetGridNodeId != node.Id))
        //         .Select(gn => new TreeNode<GridNode>(gn))
        //         .Select(gn => AddChildrenRecursively(gn, nodes, links))
        //         .ToList();
        //
        // public static TreeNode<GridNode> AddChildrenRecursively(TreeNode<GridNode> node, List<GridNode> nodes, List<GridNodeLink> links)
        // {
        //     links
        //         .Where( l => nodes.Any(n => n.Id == l.TargetGridNodeId)) // Exclude some incorrect data, should not be needed normally
        //         .Where(l => l.SourceGridNodeId == node.Value.Id)
        //         .Select(l => nodes.SingleOrDefault(n => n.Id == l.TargetGridNodeId) ?? throw new Exception("Link target node not found: " + l.TargetGridNodeId))
        //         .Select(node.AddChild)
        //         .ToList()
        //         .ForEach(child => AddChildrenRecursively(child, nodes, links));
        //     return node;
        // }
        //
        //
        // public static void TestTree()
        // {
        //     var nodes = new[] {"A", "A.A", "A.A.A", "A.A.B", "A.B", "B"}
        //         .Select(s => new GridNode {Id = s, Name = s});
        //
        //     var links = new List<GridNodeLink>
        //     {
        //         new GridNodeLink {SourceGridNodeId = "A", TargetGridNodeId = "A.A"},
        //         new GridNodeLink {SourceGridNodeId = "A", TargetGridNodeId = "A.B"},
        //         new GridNodeLink {SourceGridNodeId = "A.A", TargetGridNodeId = "A.A.A"},
        //         new GridNodeLink {SourceGridNodeId = "A.A", TargetGridNodeId = "A.A.B"},
        //     };
        //
        //     var locations = new[] {"A", "A.A", "A.A.B"}
        //         .Select(s => new GridLocation {Id = s, GridNodeId = s});
        //
        //     DisplayGridNodeTree(nodes.ToList(), links.ToList(), locations.ToList());
        // }
    }
}