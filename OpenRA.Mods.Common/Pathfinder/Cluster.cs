using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Server;
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

		public bool Contains(CPos cell)
		{
			return cell.X >= Left && cell.X <= Right && cell.Y >= Top && cell.Y <= Bottom;
		}
	}

	public class Cluster
	{
		public Boundaries Boundaries { get; private set; }
		public List<Component> Components { get; private set; }
		public LinkedList<CPos> Nodes = new LinkedList<CPos>();

		public Cluster(Boundaries boundaries, List<Component> components)
		{
			Boundaries = boundaries;
			Components = components;
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
		public Edge(CPos to, EdgeType edgeType, int cost, List<CPos> path)
		{
			To = to;
			EdgeType = edgeType;
			Cost = cost;
			Path = path;
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
		readonly int clustersW;
		readonly int clusterSize;
		public HGraph Graph { get; private set; }
		public List<Cluster> Clusters;

		public ClustersManager(HGraph graph, int clustersW, int clusterSize)
		{
			this.clustersW = clustersW;
			this.clusterSize = clusterSize;
			Graph = graph;
		}

		public void Add(List<Cluster> buildCluster)
		{
			Clusters = buildCluster;
		}

		public Cluster GetCluster(CPos cell)
		{
			var x = cell.X / clusterSize;
			var y = cell.Y / clusterSize;
			return Clusters[y * clustersW + x];
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
		readonly Dictionary<Tuple<CPos, CPos>, bool> distanceCalculated = new Dictionary<Tuple<CPos, CPos>, bool>();
		HGraph graph;

		readonly CellLayer<int> components;
		int componentId = 1;

		public ClusterBuilder(World world, Locomotor locomotor, int maxLevel)
		{
			map = world.Map;
			components = new CellLayer<int>(map);
			graph = new HGraph(map);
			this.world = world;
			this.locomotor = locomotor;
			this.maxLevel = maxLevel;

			Clusters = new List<Cluster>[maxLevel];
		}

		public ClustersManager Build()
		{
			var width = map.MapSize.X;
			var clustersW = width / ClusterSize;
			if (width % ClusterSize > 0)
				clustersW++;

			var clusters = new ClustersManager(graph, clustersW, ClusterSize);
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

					var components = CreateComponents(boundaries);

					var cluster = new Cluster(boundaries, components);

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
				CreateIntraEdges(cluster);
			}

			return clusters;
		}

		List<Component> CreateComponents(Boundaries boundaries)
		{
			var components = new List<Component>();

			for (var x = boundaries.Left; x < boundaries.Right; x++)
			{
				for (var y = boundaries.Top; y < boundaries.Bottom; y++)
				{
					var pos = new CPos(x, y);
					if (this.components[pos] == 0 && locomotor.CanEnterCell(pos))
						components.Add(Floodfill(x, y, boundaries));
				}
			}

			return components;
		}

		Component Floodfill(int x, int y, Boundaries boundaries)
		{
			var queue = new Queue<CPos>();
			var id = componentId++;
			var start = new CPos(x, y);
			queue.Enqueue(start);
			var directions = CVec.Directions;

			var cells = new HashSet<CPos>
			{
                start
			};

			while (queue.Count > 0)
			{
				var position = queue.Dequeue();

				components[position] = id;

				for (var i = 0; i < directions.Length; i++)
				{
					var neighbor = position + directions[i];

					if (boundaries.Contains(neighbor) && !cells.Contains(neighbor) && locomotor.CanEnterCell(neighbor))
					{
                        cells.Add(neighbor);
                        queue.Enqueue(neighbor);
					}
				}
			}

			var component = new Component(cells);

			return component;
		}

		void CreateIntraEdges(Cluster cluster)
		{
			var clusterPathGraph = PathSearch.GetClusterPathGraph(world, cluster.Boundaries, locomotor);
			var dijkstra = new Dijkstra(clusterPathGraph);

			foreach (var node in cluster.Nodes)
			{
				var paths = dijkstra.Search(node, cluster.Nodes);

				foreach (var path in paths)
				{
					var tuple = Tuple.Create(node, path.Target);
					var invtuple = Tuple.Create(path.Target, node);

					if (distanceCalculated.ContainsKey(tuple))
						continue;

					// var edge2 = new Edge(toEntrancePoint, node1, EdgeType.Intra, path.Cost, path.Path);
					graph.AddEdge(path.Target, node, EdgeType.Intra, path.Cost, path.Path);

					var reversePath = new List<CPos>(path.Path);
					reversePath.Reverse();

					graph.AddEdge(node, path.Target, EdgeType.Intra, path.Cost, reversePath);

					distanceCalculated[tuple] = distanceCalculated[invtuple] = true;
				}

				dijkstra.Reset();
				clusterPathGraph.Dispose();
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

			return locomotor.CanEnterCell(nodes.Item1) &&
				   locomotor.CanEnterCell(nodes.Item2);
		}

		CPos GetNode(int left, int top)
		{
			return new CPos(left, top);
		}
	}

	public class Component
	{
		public HashSet<CPos> Cells { get; private set; }

		public Component(HashSet<CPos> cells)
		{
			Cells = cells;
		}
	}

	public class HGraph : IGraph<CellInfo>
	{
		CellLayer<CellInfo> infos;
		Dictionary<CPos, LinkedList<Edge>> edges = new Dictionary<CPos, LinkedList<Edge>>();

		public HGraph(Map map)
		{
			infos = new CellLayer<CellInfo>(map);
		}

		public void AddEdge(CPos cell, CPos to, EdgeType edgeType, int cost = 1, List<CPos> pathPath = null)
		{
			edges.GetOrAdd(cell).AddLast(new Edge(to, edgeType, cost, pathPath));
		}

		public IEnumerable<Edge> Edges(CPos cell)
		{
			return edges[cell];
		}

		public Edge GetEdge(CPos from, CPos to)
		{
			return edges[from].SingleOrDefault(e => e.To == to);
		}

		public void Dispose()
		{
		}

		public List<GraphConnection> GetConnections(CPos position)
		{
			var list = edges[position];
			var result = new List<GraphConnection>();
			foreach (var edge in list)
				result.Add(new GraphConnection(edge.To, edge.Cost));

			return result;
		}

		public CellInfo this[CPos pos]
		{
			get { return infos[pos]; }
			set { infos[pos] = value; }
		}

		public Func<CPos, bool> CustomBlock { get; set; }
		public Func<CPos, int> CustomCost { get; set; }
		public int LaneBias { get; set; }
		public bool InReverse { get; set; }
		public Actor IgnoreActor { get; set; }
		public World World { get; private set; }
		public Actor Actor { get; private set; }
	}

	public class Dijkstra
	{
		readonly IGraph<CellInfo> graph;
		readonly PriorityQueue<GraphConnection> frontier;

		public Dijkstra(IGraph<CellInfo> graph)
		{
			this.graph = graph;
			frontier = new PriorityQueue<GraphConnection>(GraphConnection.ConnectionCostComparer);
		}

		public List<IntraClusterPath> Search(CPos @from, IEnumerable<CPos> targets)
		{
			frontier.Add(new GraphConnection(@from, 0));

			var result = new List<IntraClusterPath>();
			var counts = targets.Count() - 1;

			graph[from] = new CellInfo(0, 0, from, CellStatus.Open);

			while (!frontier.Empty)
			{
				var currentMinNode = frontier.Pop().Destination;
				var currentCell = graph[currentMinNode];
				graph[currentMinNode] = new CellInfo(currentCell.CostSoFar, currentCell.EstimatedTotal, currentCell.PreviousPos, CellStatus.Closed);

				if (counts == 0)
					break;

				if (currentMinNode != from && targets.Contains(currentMinNode) && result.All(r => r.Target != currentMinNode))
				{
					var cc = currentCell;

					var path = new List<CPos>();
					while (true)
					{
						if (cc.PreviousPos == from || cc.PreviousPos == CPos.Zero)
							break;

						path.Add(cc.PreviousPos);
						cc = graph[cc.PreviousPos];
					}

					result.Add(new IntraClusterPath(currentMinNode, path, currentCell.CostSoFar));
					counts--;
				}

				foreach (var connection in graph.GetConnections(currentMinNode))
				{
					// Calculate the cost up to that point
					var gCost = currentCell.CostSoFar + connection.Cost;

					var neighborCPos = connection.Destination;
					var neighborCell = graph[neighborCPos];

					// Cost is even higher; next direction:
					if (neighborCell.Status == CellStatus.Closed || gCost >= neighborCell.CostSoFar)
						continue;

					graph[neighborCPos] = new CellInfo(gCost, 0, currentMinNode, CellStatus.Open);

					if (neighborCell.Status != CellStatus.Open)
						frontier.Add(new GraphConnection(neighborCPos, gCost));
				}
			}

			return result;
		}

		public void Reset()
		{
			frontier.Clear();
		}
	}

	public class IntraClusterPath
	{
		public CPos Target { get; private set; }
		public List<CPos> Path { get; private set; }
		public int Cost { get; private set; }

		public IntraClusterPath(CPos target, List<CPos> path, int cost)
		{
			Target = target;
			Path = path;
			Cost = cost;
		}
	}
}
