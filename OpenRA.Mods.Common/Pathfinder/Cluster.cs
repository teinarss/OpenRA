using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

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
		public List<int> Components { get; private set; }

		public Cluster(Boundaries boundaries, List<int> components)
		{
			Boundaries = boundaries;
			Components = components;
		}

		public bool Contains(CPos cell)
		{
			return true;
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
		readonly CellLayer<int> componentIds;
		readonly List<Component> components;
		public AbstractGraph Graph { get; private set; }
		public List<Cluster> Clusters;

		public ClustersManager(AbstractGraph graph, int clustersW, int clusterSize, CellLayer<int> componentIds, List<Component> components)
		{
			this.clustersW = clustersW;
			this.clusterSize = clusterSize;
			this.componentIds = componentIds;
			this.components = components;
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

		public Component GetComponent(CPos cell)
		{
			var id = componentIds[cell];

			return components[id - 1];
		}

		public Component GetComponent(int id)
		{
			return components[id - 1];
		}

		public int GetComponentId(CPos cell)
		{
			return componentIds[cell];
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
		AbstractGraph graph;

		readonly CellLayer<int> componentIds;
		List<Component> components = new List<Component>();
		int componentId = 1;

		public ClusterBuilder(World world, Locomotor locomotor, int maxLevel)
		{
			map = world.Map;
			componentIds = new CellLayer<int>(map);
			graph = new AbstractGraph(map);
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

			var clusters = new ClustersManager(graph, clustersW, ClusterSize, componentIds, components);
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

		List<int> CreateComponents(Boundaries boundaries)
		{
			var ids = new List<int>();
			for (var x = boundaries.Left; x <= boundaries.Right; x++)
			{
				for (var y = boundaries.Top; y <= boundaries.Bottom; y++)
				{
					var pos = new CPos(x, y);
					if (componentIds[pos] == 0 && locomotor.CanEnterCell(pos))
					{
						var component = Floodfill(x, y, boundaries);
						ids.Add(component.Id);
						components.Add(component);
					}
				}
			}

			return ids;
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
			componentIds[start] = id;

			while (queue.Count > 0)
			{
				var position = queue.Dequeue();

				for (var i = 0; i < directions.Length; i++)
				{
					var neighbor = position + directions[i];

					if (boundaries.Contains(neighbor) && !cells.Contains(neighbor) && locomotor.CanEnterCell(neighbor))
					{
                        cells.Add(neighbor);
                        queue.Enqueue(neighbor);
                        componentIds[neighbor] = id;
					}
				}
			}

			var component = new Component(id, cells);

			return component;
		}

		void CreateIntraEdges(Cluster cluster)
		{
			foreach (var componentId in cluster.Components)
			{
				var component = GetComponent(componentId);
				var clusterPathGraph = PathSearch.GetClusterPathGraph(world, component, locomotor);
				var dijkstra = new Dijkstra(clusterPathGraph);

				foreach (var node in component.Entrances)
				{
					var paths = dijkstra.Search(node, component.Entrances);

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
				CreateEntrancesOnTop(left, cluster.Boundaries.Right, top);
			}

			if (clusterOnLeft != null)
			{
				CreateEntrancesOnLeft(top, cluster.Boundaries.Bottom, left);
			}
		}

		void CreateEntrancesOnTop(int colStart, int colEnd, int row)
		{
			Func<int, Tuple<CPos, CPos>> getNodesForColumn =
				column => Tuple.Create(GetNode(column, row - 1), GetNode(column, row));

			CreateEntrancesAlongEdge(colStart, colEnd, getNodesForColumn);
		}

		void CreateEntrancesOnLeft(int rowStart, int rowEnd, int column)
		{
			Func<int, Tuple<CPos, CPos>> getNodesForRow =
				row => Tuple.Create(GetNode(column - 1, row), GetNode(column, row));

			CreateEntrancesAlongEdge(rowStart, rowEnd, getNodesForRow);
		}

		void CreateEntrancesAlongEdge(int startPoint, int endPoint, Func<int, Tuple<CPos, CPos>> getNodesInEdge)
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

					var c1 = GetComponent(srcNode);
					c1.AddNode(srcNode);
					var c2 = GetComponent(destNode);
					c2.AddNode(destNode);

					graph.AddEdge(srcNode, destNode, EdgeType.Inter);
					graph.AddEdge(destNode, srcNode, EdgeType.Inter);

					nodes = getNodesInEdge(entranceEnd);
					srcNode = nodes.Item1;
					destNode = nodes.Item2;

					c1 = GetComponent(srcNode);
					c1.AddNode(srcNode);
					c2 = GetComponent(destNode);
					c2.AddNode(destNode);

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

					var c1 = GetComponent(srcNode);
					c1.AddNode(srcNode);
					var c2 = GetComponent(destNode);
					c2.AddNode(destNode);
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

			return locomotor.CanEnterCell(nodes.Item1) && locomotor.CanEnterCell(nodes.Item2);
		}

		CPos GetNode(int left, int top)
		{
			return new CPos(left, top);
		}

		Component GetComponent(CPos cell)
		{
			var id = componentIds[cell];

			return components[id - 1];
		}

		Component GetComponent(int id)
		{
			return components[id - 1];
		}
	}

	public class Component
	{
		public int Id { get; private set; }
		public HashSet<CPos> Cells { get; private set; }
		public List<CPos> Entrances = new List<CPos>();

		public Component(int id, HashSet<CPos> cells)
		{
			Id = id;
			Cells = cells;
		}

		public void AddNode(CPos cell)
		{
			Entrances.Add(cell);
		}

		public bool Contains(CPos cell)
		{
			return Cells.Contains(cell);
		}
	}

	public interface IAbstractGraph
	{
		IEnumerable<GraphConnection> GetConnections(CPos currentMinNode);
	}

	public class AbstractGraph : IAbstractGraph
	{
		CellLayer<CellInfo> infos;
		Dictionary<CPos, LinkedList<Edge>> edges = new Dictionary<CPos, LinkedList<Edge>>();

		public AbstractGraph(Map map)
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

		public IEnumerable<GraphConnection> GetConnections(CPos position)
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
