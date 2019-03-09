using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Pathfinder
{
	public class EntrancePoint
	{
		public EntrancePoint(ConcreSteNode srcNode)
		{
			
		}

		public Position Position { get; set; }
		public CPos Pos { get; set; }
	}

	public class Cluster
	{
		public int X { get; private set; }
		public int Y { get; private set; }
		public int Width { get; private set; }
		public int Height { get; private set; }

		public List<EntrancePoint> Entrances = new List<EntrancePoint>();
		private readonly Dictionary<Tuple<CPos, CPos>, bool> _distanceCalculated;

		public Cluster(Map map, int x, int y, int left, int top, int width, int height)
		{
			X = x;
			Y = y;
			Width = width;
			Height = height;
		}

		public void CreateIntraClusterEdges()
		{
			foreach (var point1 in Entrances)
				foreach (var point2 in Entrances)
					ComputePathBetweenEntrances(point1, point2);
		}

		void ComputePathBetweenEntrances(EntrancePoint fromEntrancePoint, EntrancePoint toEntrancePoint)
		{
			if (fromEntrancePoint.Pos == toEntrancePoint.Pos)
				return;

			var tuple = Tuple.Create(fromEntrancePoint.Pos, toEntrancePoint.Pos);
			var invtuple = Tuple.Create(toEntrancePoint.Pos, fromEntrancePoint.Pos);

			if (_distanceCalculated.ContainsKey(tuple))
				return;

			//var startNodeId = Id<ConcreteNode>.From(GetEntrancePositionIndex(fromEntrancePoint));
			//var targetNodeId = Id<ConcreteNode>.From(GetEntrancePositionIndex(toEntrancePoint));
			//var search = new AStar<ConcreteNode>(SubConcreteMap, startNodeId, targetNodeId);

			var pathFinder = new PathFinder(null);
			var path = pathFinder.FindUnitPath(fromEntrancePoint.Pos, toEntrancePoint.Pos, null, null);


			if (path.PathCost != 0)
			{
				// Yeah, we are supposing reaching A - B is the same like reaching B - A. Which
				// depending on the game this is NOT necessarily true (e.g climbing, downstepping a mountain)
				_distances[tuple] = _distances[invtuple] = path.PathCost;
				_cachedPaths[tuple] = new List<Id<ConcreteNode>>(path.PathNodes);
				path.PathNodes.Reverse();
				_cachedPaths[invtuple] = path.PathNodes;

			}

			_distanceCalculated[tuple] = _distanceCalculated[invtuple] = true;
		}
	}

	public struct Position
	{
		public Position(int x, int y) : this()
		{
			X = x;
			Y = y;
		}


		public int X { get; private set; }
		public int Y { get; private set; }
	}

	public class HierarchicalGraph
	{
		readonly Map map;
		const int MAX_ENTRANCE_WIDTH = 6;

		const int ClusterSize = 10;

		public HierarchicalGraph(Map map)
		{
			this.map = map;
		}

		public void BuildCluster(Map map)
		{
			var hierarchicalMap = new HierarchicalMap(map);
			var clusters = new List<Cluster>();

			for (int top = 0, y = 0; top < map.MapSize.Y; top += ClusterSize, y++)
			for (int left = 0, x = 0; left < map.MapSize.X; left += ClusterSize, x++)
			{
					var width = Math.Min(ClusterSize, map.MapSize.X - left);
					var height = Math.Min(ClusterSize, map.MapSize.Y - top);
					var cluster = new Cluster(map, x, y, left, top, width, height);


					var clusterAbove = top > 0 ? GetCluster(map.MapSize.X, clusters, x, y - 1) : null;
					var clusterOnLeft = left > 0 ? GetCluster(map.MapSize.X, clusters, x - 1, y) : null;

					CreateClusterEntrances(cluster, clusterAbove, clusterOnLeft);


					clusters.Add(cluster);
			}
		}

		Cluster GetCluster(int width, List<Cluster> clusters, int left, int top)
		{
			var clustersW = width / ClusterSize;
			if (width % ClusterSize > 0)
				clustersW++;

			return clusters[top * clustersW + left];
		}

		void CreateClusterEntrances(Cluster cluster, Cluster clusterAbove, Cluster clusterOnLeft)
		{
			var top = cluster.Y;
			var left = cluster.X;

			if (clusterAbove != null)
			{
				CreateEntrancesOnTop(
										left,
										left + cluster.Width - 1,
										top - 1,
										clusterAbove,
										cluster
										 );
			}

			if (clusterOnLeft != null)
			{
				CreateEntrancesOnLeft(
					top,
					top + cluster.Height - 1,
					left - 1,
					clusterOnLeft,
					cluster);
			}
		}

		void CreateEntrancesOnTop(int colStart, int colEnd, int row, Cluster clusterOnTop, Cluster cluster)
		{
			Func<int, Tuple<ConcreteNode, ConcreteNode>> getNodesForColumn =
				column => Tuple.Create(GetNode(column, row), GetNode(column, row + 1));

			CreateEntrancesAlongEdge(colStart, colEnd, clusterOnTop, cluster, getNodesForColumn);
		}

		void CreateEntrancesOnLeft(int rowStart, int rowEnd, int column, Cluster clusterOnLeft, Cluster cluster)
		{
			Func<int, Tuple<ConcreteNode, ConcreteNode>> getNodesForRow =
				row => Tuple.Create(GetNode(column, row), GetNode(column + 1, row));

			CreateEntrancesAlongEdge(rowStart, rowEnd, clusterOnLeft, cluster, getNodesForRow);
		}

		void CreateEntrancesAlongEdge(
			int startPoint,
			int endPoint,
			Cluster precedentCluster,
			Cluster currentCluster,
			Func<int, Tuple<ConcreteNode, ConcreteNode>> getNodesInEdge)
		{
			for (var entranceStart = startPoint; entranceStart <= endPoint; entranceStart++)
			{
				var size = GetEntranceSize(entranceStart, endPoint, getNodesInEdge);

				var entranceEnd = entranceStart + size - 1;
				if (size == 0)
					continue;

				if (size > MAX_ENTRANCE_WIDTH)
				{
					var nodes = getNodesInEdge(entranceStart);
					var srcNode = nodes.Item1;
					var destNode = nodes.Item2;

					precedentCluster.Entrances.Add(new EntrancePoint(srcNode));
					currentCluster.Entrances.Add(new EntrancePoint(destNode));

					nodes = getNodesInEdge(entranceEnd);
					srcNode = nodes.Item1;
					destNode = nodes.Item2;

					precedentCluster.Entrances.Add(new EntrancePoint(srcNode));
					currentCluster.Entrances.Add(new EntrancePoint(destNode));
				}
				else
				{
					var nodes = getNodesInEdge((entranceEnd + entranceStart) / 2);
					var srcNode = nodes.Item1;
					var destNode = nodes.Item2;

					precedentCluster.Entrances.Add(new EntrancePoint(srcNode));
					currentCluster.Entrances.Add(new EntrancePoint(destNode));
				}

				entranceStart = entranceEnd;
			}
		}

		int GetEntranceSize(int entranceStart, int end, Func<int, Tuple<ConcreteNode, ConcreteNode>> getNodesInEdge)
		{
			var size = 0;
			while (entranceStart + size <= end && !EntranceIsBlocked(entranceStart + size, getNodesInEdge))
			{
				size++;
			}

			return size;
		}

		private ConcreteNode GetNode(int left, int top)
		{
			map.AllCells.
			return _concreteMap.Graph.GetNode(_concreteMap.GetNodeIdFromPos(left, top));
		}
	}

	public class ConcreteNode
	{
	}

	class HierarchicalMap
	{
		public HierarchicalMap(Map map)
		{
			
		}

		public List<Cluster> Clusters { get; set; }
		
	}


	class Node
	{
		public List<Edge> Edges;
	}

	public class Edge
	{

	}
}