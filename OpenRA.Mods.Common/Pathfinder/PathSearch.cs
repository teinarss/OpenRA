#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Pathfinder
{
	public sealed class PathSearch : BasePathSearch
	{
		// PERF: Maintain a pool of layers used for paths searches for each world. These searches are performed often
		// so we wish to avoid the high cost of initializing a new search space every time by reusing the old ones.
		static readonly ConditionalWeakTable<World, CellInfoLayerPool> LayerPoolTable = new ConditionalWeakTable<World, CellInfoLayerPool>();
		static readonly ConditionalWeakTable<World, CellInfoLayerPool>.CreateValueCallback CreateLayerPool = world => new CellInfoLayerPool(world.Map);

		static CellInfoLayerPool LayerPoolForWorld(World world)
		{
			return LayerPoolTable.GetValue(world, CreateLayerPool);
		}

		public override IEnumerable<Pair<CPos, int>> Considered
		{
			get { return considered; }
		}

		LinkedList<Pair<CPos, int>> considered;

		#region Constructors

		private PathSearch(IGraph<CellInfo> graph)
			: base(graph)
		{
			considered = new LinkedList<Pair<CPos, int>>();
		}

		public static IGraph<CellInfo> GetClusterPathGraph(World world, Component component, Locomotor locomotor)
		{
			return new ClusterPathGraph(component, LayerPoolForWorld(world), locomotor, world, false);
		}

		public static IPathSearch Search(World world, Locomotor locomotor, Actor self, BlockedByActor check, Func<CPos, bool> goalCondition)
		{
			var graph = new PathGraph(LayerPoolForWorld(world), locomotor, self, world, check);
			var search = new PathSearch(graph);
			search.isGoal = goalCondition;
			search.heuristic1 = new FuncHeuristic(loc => 0);
			return search;
		}

		public static IPathSearch FromPoint(World world, Locomotor locomotor, Actor self, CPos @from, CPos target,
			BlockedByActor check, bool hpa)
		{
			var layerPoolForWorld = LayerPoolForWorld(world);
			var graph = new PathGraph(layerPoolForWorld, locomotor, self, world, check);
			IHeuristic heuristic = null;

			if (hpa)
				heuristic = new HierarchicalHeuristic(world, layerPoolForWorld, locomotor.ClustersManager, @from, target);
			else
				heuristic = new DiagonalHeuristic(target);

			var search = new PathSearch(graph)
			{
				heuristic1 = heuristic
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

		public static IPathSearch FromPoints(World world, Locomotor locomotor, Actor self, IEnumerable<CPos> froms, CPos target, BlockedByActor check)
		{
			var graph = new PathGraph(LayerPoolForWorld(world), locomotor, self, world, check);
			var search = new PathSearch(graph)
			{
				heuristic1 = new DiagonalHeuristic(target)
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

		protected override void AddInitialCell(CPos location)
		{
			var cost = heuristic1.Heuristic(location);
			Graph[location] = new CellInfo(0, cost, location, CellStatus.Open);
			var connection = new GraphConnection(location, cost);
			OpenQueue.Add(connection);
			StartPoints.Add(connection);
			considered.AddLast(new Pair<CPos, int>(location, 0));
		}

		#endregion

		/// <summary>
		/// This function analyzes the neighbors of the most promising node in the Pathfinding graph
		/// using the A* algorithm (A-star) and returns that node
		/// </summary>
		/// <returns>The most promising node of the iteration</returns>
		public override CPos Expand()
		{
			var currentMinNode = OpenQueue.Pop().Destination;

			var currentCell = Graph[currentMinNode];
			Graph[currentMinNode] = new CellInfo(currentCell.CostSoFar, currentCell.EstimatedTotal, currentCell.PreviousPos, CellStatus.Closed);

			if (Graph.CustomCost != null && Graph.CustomCost(currentMinNode) == Constants.InvalidNode)
				return currentMinNode;

			foreach (var connection in Graph.GetConnections(currentMinNode))
			{
				// Calculate the cost up to that point
				var gCost = currentCell.CostSoFar + connection.Cost;

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
					hCost = heuristic1.Heuristic(neighborCPos);

				var estimatedCost = gCost + hCost;
				Graph[neighborCPos] = new CellInfo(gCost, estimatedCost, currentMinNode, CellStatus.Open);

				if (neighborCell.Status != CellStatus.Open)
					OpenQueue.Add(new GraphConnection(neighborCPos, estimatedCost));

				if (Debug)
				{
					if (gCost > MaxCost)
						MaxCost = gCost;

					considered.AddLast(new Pair<CPos, int>(neighborCPos, gCost));
				}
			}

			return currentMinNode;
		}
	}

	sealed class AbstractPathSearch
	{
		readonly IAbstractGraph graph;
		readonly ClustersManager clustersManager;
		IPriorityQueue<GraphConnection> openQueue;
		CellLayer<CellInfo> cellStatus;
		readonly DiagonalHeuristic heuristic;
		CPos target;

		public AbstractPathSearch(CellInfoLayerPool layerPool, IAbstractGraph graph, ClustersManager clustersManager, CPos start, CPos target)
		{
			var pooledLayer = layerPool.Get();
			cellStatus = pooledLayer.GetLayer();
			this.target = target;
			this.graph = graph;
			this.clustersManager = clustersManager;
			openQueue = new PriorityQueue<GraphConnection>(GraphConnection.ConnectionCostComparer);
			openQueue.Add(new GraphConnection(start, 0));
			heuristic = new DiagonalHeuristic(target);

			cellStatus[start] = new CellInfo(0, heuristic.Heuristic(start), start, CellStatus.Open);
		}

		public List<AbstractPath> FindPath()
		{
			List<AbstractPath> path = null;

			while (CanExpand)
			{
				var p = Expand();
				if (p == target)
				{
					path = MakePath(cellStatus, p);
					break;
				}
			}

			if (path != null)
				return path;

			// no path exists
			return new List<AbstractPath>();
		}

		bool CanExpand { get { return !openQueue.Empty; } }

		CPos Expand()
		{
			var currentMinNode = openQueue.Pop().Destination;

			var currentCell = cellStatus[currentMinNode];
			cellStatus[currentMinNode] = new CellInfo(currentCell.CostSoFar, currentCell.EstimatedTotal, currentCell.PreviousPos, CellStatus.Closed);

			foreach (var connection in graph.GetConnections(currentMinNode))
			{
				// Calculate the cost up to that point
				var gCost = currentCell.CostSoFar + connection.Cost;

				var neighborCPos = connection.Destination;
				var neighborCell = cellStatus[neighborCPos];

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
					hCost = heuristic.Heuristic(neighborCPos);

				var estimatedCost = gCost + hCost;
				cellStatus[neighborCPos] = new CellInfo(gCost, estimatedCost, currentMinNode, CellStatus.Open);

				if (neighborCell.Status != CellStatus.Open)
					openQueue.Add(new GraphConnection(neighborCPos, estimatedCost));
			}

			return currentMinNode;
		}

		List<AbstractPath> MakePath(CellLayer<CellInfo> cellInfo, CPos destination)
		{
			var ret = new List<AbstractPath>();
			var currentNode = destination;
			var currentCellInfo = cellInfo[currentNode];

			var take = false;
			while (cellInfo[currentNode].PreviousPos != currentNode)
			{
				if (take)
				{
					var component = clustersManager.GetComponent(currentNode);
					ret.Add(new AbstractPath(component.Id, currentNode, currentCellInfo.CostSoFar));
					take = false;
				}
				else
				{
					take = true;
				}

				currentNode = cellInfo[currentNode].PreviousPos;
				currentCellInfo = cellInfo[currentNode];
			}

			var componentId = clustersManager.GetComponent(currentNode).Id;
			ret.Add(new AbstractPath(componentId, currentNode, currentCellInfo.CostSoFar));
			return ret;
		}
	}

	public class DiagonalHeuristic : IHeuristic
	{
		readonly CPos destination;

		public DiagonalHeuristic(CPos destination)
		{
			this.destination = destination;
		}

		public int Heuristic(CPos here)
		{
			var diag = Math.Min(Math.Abs(here.X - destination.X), Math.Abs(here.Y - destination.Y));
			var straight = Math.Abs(here.X - destination.X) + Math.Abs(here.Y - destination.Y);

			// According to the information link, this is the shape of the function.
			// We just extract factors to simplify.
			// Possible simplification: var h = Constants.CellCost * (straight + (Constants.Sqrt2 - 2) * diag);
			return Constants.CellCost * straight + (Constants.DiagonalCellCost - 2 * Constants.CellCost) * diag;
		}
	}

	public interface IHeuristic
	{
		int Heuristic(CPos here);
	}

	class HierarchicalHeuristic : IHeuristic
	{
		ClustersManager manager;
		ExtendedGraph graph;
		int startComponent;
		int targetComponent;
		DiagonalHeuristic heuristic;

		List<AbstractPath> abstractPath = new List<AbstractPath>();

		AbstractPath currentStep;

		public HierarchicalHeuristic(World world, CellInfoLayerPool layerPool, ClustersManager clustersManager, CPos start, CPos target)
		{
			manager = clustersManager;
			graph = new ExtendedGraph(clustersManager.Graph);

			startComponent = AddNodes(start);
			targetComponent = AddNodes(target);

			heuristic = new DiagonalHeuristic(target);

			if (startComponent != targetComponent)
			{
				// reverse search
				var search = new AbstractPathSearch(layerPool, graph, clustersManager, target, start);
				abstractPath = search.FindPath();

				var clusterOverlay = world.WorldActor.Trait<ClusterOverlay>();

				clusterOverlay.AddPath(abstractPath);
			}
		}

		int AddNodes(CPos cell)
		{
			var component = manager.GetComponent(cell);
			graph.AddNode(cell, component.Entrances);
			return component.Id;
		}

		public int Heuristic(CPos here)
		{
			var currentComponent = manager.GetComponentId(here);

			// base case - we are in the target component
			if (currentComponent == targetComponent)
				return heuristic.Heuristic(here);

			if (currentComponent != currentStep.ComponentId)
				currentStep = GetStep(currentComponent);

			if (currentStep == null)
				return 500000;

			var diagonalHeuristic = new DiagonalHeuristic(currentStep.Exit);

			return diagonalHeuristic.Heuristic(here) + currentStep.CostToTarget;
		}

		AbstractPath GetStep(int componentId)
		{
			foreach (var path in abstractPath)
			{
				if (path.ComponentId == componentId)
					return path;
			}

			return null;
		}
	}

	public class AbstractPath
	{
		public AbstractPath(int componentId, CPos exit, int costToTarget)
		{
			ComponentId = componentId;
			Exit = exit;
			CostToTarget = costToTarget;
		}

		public int ComponentId { get; set; }
		public CPos Exit { get; private set; }
		public int CostToTarget { get; set; }
	}

	public class FuncHeuristic : IHeuristic
	{
		readonly Func<CPos, int> func;

		public FuncHeuristic(Func<CPos, int> func)
		{
			this.func = func;
		}

		public int Heuristic(CPos here)
		{
			return func(here);
		}
	}
}
