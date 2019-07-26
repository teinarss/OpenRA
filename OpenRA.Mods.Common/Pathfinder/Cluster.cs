using System;
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

		public Boundaries Bounds { get; set; }

		public bool Contains(CPos cell)
		{
			return true;
		}

		public void AddNode(CPos cell)
		{
			Nodes.AddLast(cell);
		}
	}

	class Edge
	{
		public Edge(CPos to, EdgeType edgeType, int cost)
		{
		}

		public EdgeType EdgeType { get; set; }
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
		public List<Cluster> Clusters;

		public void Add(List<Cluster> buildCluster)
		{
			Clusters = buildCluster;
		}
	}

	public class ClusterBuilder
	{
		readonly Map map;
		readonly World world;
		readonly Locomotor locomotor;
		readonly int maxLevel;
		const int Maxentrancewidth = 6;

		const int ClusterSize = 10;
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
			var clusters = new ClustersManager();
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
							if (cluster.Contains(cluster1))
							{
								cluster.AddCluster(cluster1);
							}

						}

						CreateAbstractBorderNodes(cluster, clusterAbove, clusterOnLeft);
					}

					clusters.Add(cluster);

					if (level > 0)
					{
					}
				}

			foreach (var cluster in clusters)
			{
				cluster.CreateIntraClusterEdges(level == 0);
			}

			return clusters;
		}

		void CreateAbstractBorderNodes(Cluster cluster, Cluster clusterAbove, Cluster clusterOnLeft)
		{
			foreach (var c1 in cluster.Clusters)
			{
				if (clusterOnLeft != null)
				{
					foreach (var c2 in clusterOnLeft.Clusters)
					{
						if (c1.TopLeft.Y == c2.TopLeft.Y && c2.BottomRight.X + 1 == c1.TopLeft.X)
						{
							CreateAbstractInterEdges(cluster, clusterOnLeft, c1, c2);
						}
					}
				}

				if (clusterAbove != null)
				{
					foreach (var c2 in clusterAbove.Clusters)
					{
						if (c1.TopLeft.X == c2.TopLeft.X && c2.BottomRight.Y + 1 == c1.TopLeft.Y)
						{
							CreateAbstractInterEdges(cluster, clusterAbove, c1, c2);
						}
					}
				}

			}
			//var top = cluster.TopLeft.Y;
			//var left = cluster.TopLeft.X;

			//if (clusterAbove != null)
			//{
			//	CreateEntrancesOnTop(
			//							left,
			//							left + cluster.Width - 1,
			//							top - 1,
			//							clusterAbove,
			//							cluster
			//							 );
			//}

			//if (clusterOnLeft != null)
			//{
			//	CreateEntrancesOnLeft(
			//		top,
			//		top + cluster.Height - 1,
			//		left - 1,
			//		clusterOnLeft,
			//		cluster);
			//}
		}

		void CreateAbstractInterEdges(Cluster cluster, Cluster clusterOnLeft, Cluster c1, Cluster c2)
		{
			foreach (var node in c1.Nodes)
			{
				foreach (var edge in node.Edges)
				{
					if (edge.EdgeType == EdgeType.Inter && c2.Contains(edge.To))
					{
						var node1 = cluster.AddNode(edge.From.CPos);

						//var node2 = new Node(currentCluster.Id, destNode);
						var node2 = clusterOnLeft.AddNode(edge.To.CPos);


						var edge1 = new Edge(node1, node2, EdgeType.Inter, 1);
						node1.AddEdge(edge1);

						var edge2 = new Edge(node2, node1, EdgeType.Inter, 1);
						node2.AddEdge(edge2);

						break;
					}
				}
			}
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
			var top = cluster.Bounds.Top;
			var left = cluster.Bounds.Left;

			if (clusterAbove != null)
			{
				CreateEntrancesOnTop(
										left,
										cluster.Bounds.Right,
										top,
										clusterAbove,
										cluster);
			}

			if (clusterOnLeft != null)
			{
				CreateEntrancesOnLeft(
					top,
					cluster.Bounds.Bottom,
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

		public void UpdateEntrance(Cluster cluster1, Cluster cluster2)
		{
			// cluster to the left
			if (cluster1.Bounds.X > cluster2.Bounds.X || cluster1.Bounds.X < cluster2.Bounds.X)
			{
				var clusterLeft = cluster1.Bounds.X > cluster2.Bounds.X ? cluster2 : cluster1;
				var cluster = cluster1.Bounds.X > cluster2.Bounds.X ? cluster1 : cluster2;

				var top = cluster.Bounds.Y;
				var left = cluster.Bounds.X;

				var nodesIoDelete1 = new List<Node>();
				var nodesIoDelete2 = new List<Node>();


				foreach (var node1 in cluster.Nodes)
				{
					var found = false;
					foreach (var edge in node1.Edges.Where(e => e.To.ClusterId == clusterLeft.Id))
					{

						//cluster2.RemoveNode(edge.To);
						nodesIoDelete2.Add(edge.To);
						found = true;
					}

					if (found)
						nodesIoDelete1.Add(node1);
				}

				foreach (var node in nodesIoDelete1)
				{
					cluster.RemoveNode(node);
				}

				foreach (var node in nodesIoDelete2)
				{
					clusterLeft.RemoveNode(node);
				}

				CreateEntrancesOnLeft(
				top,
				cluster.BottomRight.Y,
				left,
				clusterLeft,
				cluster);
			}

			// cluster to the top
			if (cluster1.Bounds.Y > cluster2.Bounds.Y || cluster1.Bounds.Y < cluster2.Bounds.Y)
			{
				var clusterOnTop = cluster1.Bounds.Y > cluster2.Bounds.Y ? cluster2 : cluster1;
				var cluster = cluster1.Bounds.Y > cluster2.Bounds.Y ? cluster1 : cluster2;

				var top = cluster.Bounds.Y;
				var left = cluster.Bounds.X;

				var nodesIoDelete1 = new List<Node>();
				var nodesIoDelete2 = new List<Node>();


				foreach (var node1 in cluster.Nodes)
				{
					var found = false;
					foreach (var edge in node1.Edges.Where(e => e.To.ClusterId == clusterOnTop.Id))
					{

						//cluster2.RemoveNode(edge.To);
						nodesIoDelete2.Add(edge.To);
						found = true;
					}

					if (found)
						nodesIoDelete1.Add(node1);
				}

				foreach (var node in nodesIoDelete1)
				{
					cluster.RemoveNode(node);
				}

				foreach (var node in nodesIoDelete2)
				{
					clusterOnTop.RemoveNode(node);
				}

				CreateEntrancesOnTop(
						left,
						cluster.BottomRight.X,
						top,
						clusterOnTop,
						cluster
						 );


			}
		}

		public void Update(Pair<CPos, SubCell>[] occupiedCells)
		{
			var updateCluster = new UpdateClusterEdges();

			var clusters = Clusters[0];

			foreach (var occupiedCell in occupiedCells)
			{
				var cell = occupiedCell.First;
				var i = cell.X / 10;
				var j = cell.Y / 10;

				var cluster = GetCluster(clusters, ClusterSize, i, j);

				updateCluster.UpdatedClusters.Add(cluster);

				// left edge and not on the border
				if (cell.X == cluster.Bounds.X && cluster.Bounds.X != 0)
				{
					var otherCluster = GetCluster(clusters, ClusterSize, i - 1, j);

					updateCluster.AddClusters(cluster, otherCluster);
				}

				// top edge
				if (cell.Y == cluster.TopLeft.Y && cluster.TopLeft.Y != 0)
				{
					var otherCluster = GetCluster(clusters, ClusterSize, i, j - 1);

					updateCluster.AddClusters(cluster, otherCluster);
				}

				// right edge
				if (cell.X == cluster.BottomRight.X && cluster.BottomRight.X != map.MapSize.X)
				{
					var otherCluster = GetCluster(clusters, ClusterSize, i + 1, j);

					updateCluster.AddClusters(cluster, otherCluster);
				}

				// bottom edge
				if (cell.Y == cluster.BottomRight.Y && cluster.BottomRight.Y != map.MapSize.Y)
				{
					var otherCluster = GetCluster(clusters, ClusterSize, i, j + 1);

					updateCluster.AddClusters(cluster, otherCluster);
				}


				// Todo: could be multiple nodes on this cell...
				//var node = cluster.Nodes.SingleOrDefault(n => n.CPos == cell);

				//if (node != null)
				//{
				//	foreach (var edge in node.Edges.Where(e => e.EdgeType == EdgeType.Inter))
				//	{
				//		var i1 = edge.To.CPos.X / 10;
				//		var j1 = edge.To.CPos.Y / 10;

				//		var cluster2 = GetCluster(clusters, 0, i1, j1);

				//		updateCluster.AddClusters(cluster, cluster2);
				//	}
				//}
				//else
				//{

				//}





				//if (!updateCluster.UpdatedClusters.ContainsKey(cluster))
				//{
				//	var hashSet = new HashSet<Node> { node };

				//	updateCluster.Add(cluster, hashSet);
				//}
				//else
				//{
				//	updateCluster[cluster].Add(node);
				//}
			}

			foreach (var clusterPair in updateCluster.Pairs)
			{
				UpdateEntrance(clusterPair.First, clusterPair.Second);
			}

			foreach (var cluster in updateCluster.UpdatedClusters)
			{
				cluster.RemoveIntraEdges();
				cluster.CreateIntraClusterEdges(true);
			}
		}
	}

	class HGraph
	{
		Dictionary<CPos, Edge> edges = new Dictionary<CPos, Edge>();

		public void AddEdge(CPos cell, CPos to, EdgeType edgeType, int cost = 1)
		{
			edges.Add(cell, new Edge(to, edgeType, cost));
		}
	}
}
