﻿using System;
using System.Collections.Generic;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Pathfinder
{
	public class Boundaries
	{
		public int Top { get; private set; }
		public int Left { get; private set; }
		public int Right { get; private set; }
		public int Bottom { get; private set; }

		public Boundaries(int top, int left, int right, int bottom)
		{
			Top = top;
			Left = left;
			Right = right;
			Bottom = bottom;
		}
	}

	public class Cluster
	{
		public Boundaries Boundaries { get; private set; }
		public LinkedList<CPos> Nodes = new LinkedList<CPos>();

		public Cluster(Boundaries boundaries)
		{
			Boundaries = boundaries;
		}

		public bool Contains(CPos cell)
		{
			return true;
		}

		public void AddNode(CPos cell)
		{
			Nodes.AddLast(cell);
		}
	}

	public class Edge
	{
		public Edge(CPos to, EdgeType edgeType, int cost)
		{
			To = to;
			EdgeType = edgeType;
			Cost = cost;
		}

		public EdgeType EdgeType { get; set; }
		public int Cost { get; private set; }
		public List<CPos> Path { get; private set; }
		public CPos To { get; set; }
	}

	public enum EdgeType : byte
	{
		Intra,
		Inter
	}

	public class ClustersManager
	{
		public HGraph Graph { get; private set; }
		public List<Cluster> Clusters;

		public ClustersManager(HGraph graph)
		{
			Graph = graph;
		}

		public void Add(List<Cluster> buildCluster)
		{
			Clusters = buildCluster;
		}
	}

	public class ClusterBuilder
	{
		const int Maxentrancewidth = 6;
		const int ClusterSize = 10;

		readonly Map map;
		readonly World world;
		readonly Locomotor locomotor;
		readonly int maxLevel;

		public List<Cluster>[] Clusters;

		HGraph graph = new HGraph();

		public ClusterBuilder(World world, Locomotor locomotor, int maxLevel)
		{
			map = world.Map;
			this.world = world;
			this.locomotor = locomotor;
			this.maxLevel = maxLevel;

			Clusters = new List<Cluster>[maxLevel];
		}

		public ClustersManager Build()
		{
			var clusters = new ClustersManager(graph);
			var clusterSize = ClusterSize;
			for (int level = 0; level < maxLevel; level++)
			{
				if (level != 0)
					clusterSize *= 3;

				clusters.Add(BuildCluster(level, clusterSize));
			}

			return clusters;
		}

		public List<Cluster> BuildCluster(int level, int clusterSize)
		{
			var clusters = new List<Cluster>();

			for (int top = 0, y = 0; top < map.MapSize.Y; top += clusterSize, y++)
				for (int left = 0, x = 0; left < map.MapSize.X; left += clusterSize, x++)
				{
					var width = Math.Min(clusterSize, map.MapSize.X - left);
					if (left + clusterSize == map.MapSize.X)
					{
						width++;
					}

					var height = Math.Min(clusterSize, map.MapSize.Y - top);

					if (top + clusterSize == map.MapSize.Y)
					{
						height++;
					}

					var boundaries = new Boundaries(top, left, left + width - 1, top + height - 1);

					var cluster = new Cluster(boundaries);

					var clusterAbove = top > 0 ? GetCluster(clusters, clusterSize, x, y - 1) : null;
					var clusterOnLeft = left > 0 ? GetCluster(clusters, clusterSize, x - 1, y) : null;

					if (level == 0)
					{
						CreateClusterEntrances(cluster, clusterAbove, clusterOnLeft);
					}
					else
					{
						foreach (var cluster1 in Clusters[level - 1])
						{
						}
					}

					clusters.Add(cluster);

					if (level > 0)
					{
					}
				}

			foreach (var cluster in clusters)
			{
			}

			return clusters;
		}

		public Cluster GetCluster(int x, int y)
		{
			return GetCluster(Clusters[0], ClusterSize, x, y);
		}

		public Cluster GetCluster(List<Cluster> clusters, int clusterSize, int left, int top)
		{
			var width = map.MapSize.X;
			var clustersW = width / clusterSize;
			if (width % clusterSize > 0)
				clustersW++;

			return clusters[top * clustersW + left];
		}

		void CreateClusterEntrances(Cluster cluster, Cluster clusterAbove, Cluster clusterOnLeft)
		{
			var top = cluster.Boundaries.Top;
			var left = cluster.Boundaries.Left;

			if (clusterAbove != null)
			{
				CreateEntrancesOnTop(
										left,
										cluster.Boundaries.Right,
										top,
										clusterAbove,
										cluster);
			}

			if (clusterOnLeft != null)
			{
				CreateEntrancesOnLeft(
					top,
					cluster.Boundaries.Bottom,
					left,
					clusterOnLeft,
					cluster);
			}
		}

		void CreateEntrancesOnTop(int colStart, int colEnd, int row, Cluster clusterOnTop, Cluster cluster)
		{
			Func<int, Tuple<CPos, CPos>> getNodesForColumn =
				column => Tuple.Create(GetNode(column, row - 1), GetNode(column, row));

			CreateEntrancesAlongEdge(colStart, colEnd, clusterOnTop, cluster, getNodesForColumn);
		}

		void CreateEntrancesOnLeft(int rowStart, int rowEnd, int column, Cluster clusterOnLeft, Cluster cluster)
		{
			Func<int, Tuple<CPos, CPos>> getNodesForRow =
				row => Tuple.Create(GetNode(column - 1, row), GetNode(column, row));

			CreateEntrancesAlongEdge(rowStart, rowEnd, clusterOnLeft, cluster, getNodesForRow);
		}

		void CreateEntrancesAlongEdge(
			int startPoint,
			int endPoint,
			Cluster precedentCluster,
			Cluster currentCluster,
			Func<int, Tuple<CPos, CPos>> getNodesInEdge)
		{
			for (var entranceStart = startPoint; entranceStart <= endPoint; entranceStart++)
			{
				var size = GetEntranceSize(entranceStart, endPoint, getNodesInEdge);

				var entranceEnd = entranceStart + size - 1;
				if (size == 0)
					continue;

				if (size > Maxentrancewidth)
				{
					var nodes = getNodesInEdge(entranceStart);
					var srcNode = nodes.Item1;
					var destNode = nodes.Item2;

					graph.AddEdge(srcNode, destNode, EdgeType.Inter);
					graph.AddEdge(destNode, srcNode, EdgeType.Inter);

					precedentCluster.AddNode(srcNode);
					currentCluster.AddNode(destNode);

					nodes = getNodesInEdge(entranceEnd);
					srcNode = nodes.Item1;
					destNode = nodes.Item2;

					precedentCluster.AddNode(srcNode);
					currentCluster.AddNode(destNode);

					graph.AddEdge(srcNode, destNode, EdgeType.Inter);
					graph.AddEdge(destNode, srcNode, EdgeType.Inter);
				}
				else
				{
					var nodes = getNodesInEdge((entranceEnd + entranceStart) / 2);
					var srcNode = nodes.Item1;
					var destNode = nodes.Item2;

					graph.AddEdge(srcNode, destNode, EdgeType.Inter);
					graph.AddEdge(destNode, srcNode, EdgeType.Inter);

					precedentCluster.AddNode(srcNode);
					currentCluster.AddNode(destNode);
				}

				entranceStart = entranceEnd;
			}
		}

		int GetEntranceSize(int entranceStart, int end, Func<int, Tuple<CPos, CPos>> getNodesInEdge)
		{
			var size = 0;
			while (entranceStart + size <= end && EntranceIsOpen(entranceStart + size, getNodesInEdge))
				size++;

			return size;
		}

		bool EntranceIsOpen(int entrancePoint, Func<int, Tuple<CPos, CPos>> getNodesInEdge)
		{
			var nodes = getNodesInEdge(entrancePoint);

			return locomotor.CanEnterCell(null, nodes.Item1, null) &&
				   locomotor.CanEnterCell(null, nodes.Item2, null);
		}

		CPos GetNode(int left, int top)
		{
			return new CPos(left, top);
		}
	}

	public class HGraph
	{
		Dictionary<CPos, LinkedList<Edge>> edges = new Dictionary<CPos, LinkedList<Edge>>();

		public void AddEdge(CPos cell, CPos to, EdgeType edgeType, int cost = 1)
		{
			edges.GetOrAdd(cell).AddLast(new Edge(to, edgeType, cost));
		}

		public IEnumerable<Edge> Edges(CPos cell)
		{
			return edges[cell];
		}
	}
}
