#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using OpenRA.Mods.Common.Pathfinder.PriorityQueue;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Pathfinder
{
	public sealed class PathSearch : IDisposable
	{
		// PERF: Maintain a pool of layers used for paths searches for each world. These searches are performed often
		// so we wish to avoid the high cost of initializing a new search space every time by reusing the old ones.
		static readonly ConditionalWeakTable<World, CellInfoLayerPool> LayerPoolTable = new ConditionalWeakTable<World, CellInfoLayerPool>();
		static readonly ConditionalWeakTable<World, CellInfoLayerPool>.CreateValueCallback CreateLayerPool = world => new CellInfoLayerPool(world.Map);

		public IGraph<CellInfo> Graph { get; set; }
		protected FastPriorityQueueStruct OpenQueue { get; set; }

		//public IEnumerable<Pair<CPos, int>> Considered { get; }

		public Player Owner { get { return Graph.Actor.Owner; } }
		public int MaxCost { get; protected set; }
		public bool Debug { get; set; }
		protected Func<CPos, short> heuristic;
		protected Func<CPos, bool> isGoal;

		static CellInfoLayerPool LayerPoolForWorld(World world)
		{
			return LayerPoolTable.GetValue(world, CreateLayerPool);
		}

		public IEnumerable<Pair<CPos, int>> Considered
		{
			get { return considered; }
		}

		LinkedList<Pair<CPos, int>> considered;
		Guid fileId;
		//StringBuilder stringBuilder;

		#region Constructors

		private PathSearch(IGraph<CellInfo> graph)

		{
			fileId = Guid.NewGuid();

			Graph = graph;
			OpenQueue = new FastPriorityQueueStruct(200);
			MaxCost = 0;

			considered = new LinkedList<Pair<CPos, int>>();

			//stringBuilder = new StringBuilder();
		}

		public static PathSearch Search(World world, LocomotorInfo li, Actor self, bool checkForBlocked, Func<CPos, bool> goalCondition)
		{
			var graph = new PathGraph(LayerPoolForWorld(world), li, self, world, checkForBlocked);
			var search = new PathSearch(graph);
			search.isGoal = goalCondition;
			search.heuristic = loc => 0;
			return search;
		}

		public static PathSearch FromPoint(World world, LocomotorInfo li, Actor self, CPos from, CPos target, bool checkForBlocked)
		{
			var graph = new PathGraph(LayerPoolForWorld(world), li, self, world, checkForBlocked);
			var search = new PathSearch(graph)
			{
				heuristic = DefaultEstimator(target)
			};

			search.isGoal = loc =>
			{
				var locInfo = search.Graph[loc];
				return locInfo.EstimatedTotal - locInfo.CostSoFar == 0;
			};

			if (world.Map.Contains(from))
				search.AddInitialCell(from);

			return search;
		}

		public static PathSearch FromPoints(World world, LocomotorInfo li, Actor self, IEnumerable<CPos> froms, CPos target, bool checkForBlocked)
		{
			var graph = new PathGraph(LayerPoolForWorld(world), li, self, world, checkForBlocked);
			var search = new PathSearch(graph)
			{
				heuristic = DefaultEstimator(target)
			};

			search.isGoal = loc =>
			{
				var locInfo = search.Graph[loc];
				return locInfo.EstimatedTotal - locInfo.CostSoFar == 0;
			};

			foreach (var sl in froms.Where(sl => world.Map.Contains(sl)))
				search.AddInitialCell(sl);

			return search;
		}

		private void AddInitialCell(CPos location)
		{
			var cost = heuristic(location);
			Graph[location] = new CellInfo(0, cost, location, CellStatus.Open);
			var connection = new GraphConnection2(location, cost);



			OpenQueue.Enqueue(connection);
	//		stringBuilder.AppendLine("new List<Container>()");
	//		stringBuilder.AppendLine("{");
	//		stringBuilder.AppendLine(
	//"new Container(new CPos({0}, {1}), {2}),".F(location.X, location.Y, cost));
	//		stringBuilder.AppendLine("},");

			//StartPoints.Add(connection);
			considered.AddLast(new Pair<CPos, int>(location, 0));
		}

		#endregion

		/// <summary>
		/// This function analyzes the neighbors of the most promising node in the Pathfinding graph
		/// using the A* algorithm (A-star) and returns that node
		/// </summary>
		/// <returns>The most promising node of the iteration</returns>
		public CPos Expand()
		{
			var currentMinNode = OpenQueue.Dequeue().Destination;
			var currentCell = Graph[currentMinNode];
			Graph[currentMinNode] = new CellInfo(currentCell.CostSoFar, currentCell.EstimatedTotal, currentCell.PreviousPos, CellStatus.Closed);

			if (Graph.CustomCost != null && Graph.CustomCost(currentMinNode) == Constants.InvalidNode)
				return currentMinNode;

			//stringBuilder.AppendLine("new List<Container>()");
			//stringBuilder.AppendLine("{");

			foreach (var connection in Graph.GetConnections(currentMinNode))
			{
				// Calculate the cost up to that point
				short gCost = (short) (currentCell.CostSoFar + connection.Priority);

				var neighborCPos = connection.Destination;
				var neighborCell = Graph[neighborCPos];

				// Cost is even higher; next direction:
				if (neighborCell.Status == CellStatus.Closed || gCost >= neighborCell.CostSoFar)
					continue;

				// Now we may seriously consider this direction using heuristics. If the cell has
				// already been processed, we can reuse the result (just the difference between the
				// estimated total and the cost so far
				int hCost;
				if (neighborCell.Status == CellStatus.Open)
					hCost = neighborCell.EstimatedTotal - neighborCell.CostSoFar;
				else
					hCost = heuristic(neighborCPos);

				short estimatedCost = (short) (gCost + hCost);
				Graph[neighborCPos] = new CellInfo(gCost, estimatedCost, currentMinNode, CellStatus.Open);

				if (neighborCell.Status != CellStatus.Open)
				{
					OpenQueue.Enqueue(new GraphConnection2(neighborCPos, estimatedCost));
					//stringBuilder.AppendLine(
					//	"new Container(new CPos({0}, {1}), {2}),".F(neighborCPos.X, neighborCPos.Y, estimatedCost));

				}

				if (Debug)
				{
					if (gCost > MaxCost)
						MaxCost = gCost;

					considered.AddLast(new Pair<CPos, int>(neighborCPos, gCost));
				}
			}

			//stringBuilder.AppendLine("},");

			
			//System.Diagnostics.Debug.WriteLine("Enqueued {0}".F(count));

			return currentMinNode;
		}

		public PathSearch Reverse()
		{
			Graph.InReverse = true;
			return this;
		}

		public PathSearch WithCustomBlocker(Func<CPos, bool> customBlock)
		{
			Graph.CustomBlock = customBlock;
			return this;
		}

		public PathSearch WithIgnoredActor(Actor b)
		{
			Graph.IgnoreActor = b;
			return this;
		}

		public PathSearch WithHeuristic(Func<CPos, short> h)
		{
			heuristic = h;
			return this;
		}

		public PathSearch WithCustomCost(Func<CPos, short> w)
		{
			Graph.CustomCost = w;
			return this;
		}

		public PathSearch WithoutLaneBias()
		{
			Graph.LaneBias = 0;
			return this;
		}

		public PathSearch FromPoint(CPos from)
		{
			if (Graph.World.Map.Contains(from))
				AddInitialCell(from);

			return this;
		}

		public bool IsTarget(CPos location)
		{
			return isGoal(location);
		}

		public bool CanExpand { get { return OpenQueue.Count > 0; } }


		public void Dispose()
		{
			//File.AppendAllText("c:\\files\\temp\\{0}.txt".F(fileId), stringBuilder.ToString());
			Graph.Dispose();

			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Default: Diagonal distance heuristic. More information:
		/// http://theory.stanford.edu/~amitp/GameProgramming/Heuristics.html
		/// </summary>
		/// <returns>A delegate that calculates the estimation for a node</returns>
		private static Func<CPos, short> DefaultEstimator(CPos destination)
		{
			return here =>
			{
				var diag = Math.Min(Math.Abs(here.X - destination.X), Math.Abs(here.Y - destination.Y));
				var straight = Math.Abs(here.X - destination.X) + Math.Abs(here.Y - destination.Y);

				// According to the information link, this is the shape of the function.
				// We just extract factors to simplify.
				// Possible simplification: var h = Constants.CellCost * (straight + (Constants.Sqrt2 - 2) * diag);
				return (short) (Constants.CellCost * straight + (Constants.DiagonalCellCost - 2 * Constants.CellCost) * diag);
			};
		}
	}
}
