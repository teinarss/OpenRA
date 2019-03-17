using System.Collections.Generic;
using System.Linq;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Pathfinder
{
	public class Dijkstra
	{
		readonly IGraph<NodeInfo> graph;
		readonly PriorityQueue<GraphConnection> frontier;


		public Dijkstra(IGraph<NodeInfo> graph)
		{
			this.graph = graph;
			frontier = new PriorityQueue<GraphConnection>(GraphConnection.ConnectionCostComparer);
		}

		public List<Pair<CPos, int>> Search(CPos @from, CPos[] targets)
		{
			frontier.Add(new GraphConnection(@from, 0));

			var result = new List<Pair<CPos, int>>();
			var counts = targets.Length - 1;

			while (!frontier.Empty)
			{
				var currentMinNode = frontier.Pop().Destination;
				var currentCell = graph[currentMinNode];
				graph[currentMinNode] = new NodeInfo(currentCell.CostSoFar, currentCell.EstimatedTotal, currentCell.PreviousPos, NodeStatus.Closed);

				if (counts == 0)
					break;

				if (currentMinNode != from && targets.Contains(currentMinNode))
				{
					result.Add(new Pair<CPos, int>(currentMinNode, currentCell.CostSoFar));
					counts--;
				}

				foreach (var connection in graph.GetConnections(currentMinNode))
				{
					// Calculate the cost up to that point
					var gCost = currentCell.CostSoFar + connection.Cost;

					var neighborCPos = connection.Destination;
					var neighborCell = graph[neighborCPos];

					// Cost is even higher; next direction:
					if (neighborCell.Status == NodeStatus.Closed || gCost >= neighborCell.CostSoFar)
						continue;
	
					graph[neighborCPos] = new NodeInfo(gCost, 0, currentMinNode, NodeStatus.Open);

					if (neighborCell.Status != NodeStatus.Open)
						frontier.Add(new GraphConnection(neighborCPos, gCost));
				}
			}

			return result;
		}
			

	}
}