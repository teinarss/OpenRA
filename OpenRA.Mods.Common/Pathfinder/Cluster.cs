using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Pathfinder
{
	[Desc("Identify untraversable regions of the map for faster pathfinding, especially with AI.",
	"This trait is required. Every mod needs it attached to the world actor.")]
	class ClusterAooInfo : TraitInfo<ClusterAoo> { }

	public class ClusterAoo : IWorldLoaded, ITick
	{
		public List<Cluster>[] Clusters;
		HierarchicalGraph graph;

		public void WorldLoaded(World world, WorldRenderer wr)
		{
			var locomotor = world.WorldActor.TraitsImplementing<Locomotor>().Where(l => !string.IsNullOrEmpty(l.Info.Name))
	.FirstOrDefault();
			graph = new HierarchicalGraph(world, locomotor, 3);
			var actorMap = world.ActorMap;
			actorMap.ActorAdded += AddedToWorld;
			Build(world);
			//domainIndexes = new Dictionary<uint, MovementClassDomainIndex>();
			//tileSet = world.Map.Rules.TileSet;
			//var locomotors = world.WorldActor.TraitsImplementing<Locomotor>().Where(l => !string.IsNullOrEmpty(l.Info.Name));
			//var movementClasses = locomotors.Select(t => (uint)t.Info.GetMovementClass(tileSet)).Distinct();

			//foreach (var mc in movementClasses)
			//	domainIndexes[mc] = new MovementClassDomainIndex(world, mc);
		}

		void AddedToWorld(Actor actor)
		{
			if (actor.OccupiesSpace == null)
			{
				return;
			}

			var occupiedCells = actor.OccupiesSpace.OccupiedCells();

			//graph.Update(occupiedCells); Keep!!!

			//var updateCluster = new Dictionary<Cluster, HashSet<Node>>();
			

			//foreach (var pair in updateCluster)
			//{
			//	var eooue = new HashSet<Cluster>();
			//	var cluster = pair.Key;

			//	foreach (var node in pair.Value)
			//	{
			//		foreach (var edge in node.Edges.Where(e => e.EdgeType == EdgeType.Inter))
			//		{
			//			var otherCluster = graph.Clusters.SingleOrDefault(c => c.Id == edge.To.ClusterId);

			//			otherCluster.RemoveNode(edge.To);
			//			eooue.Add(otherCluster);
			//		}

					
			//		cluster.RemoveNode(node);
			//	}

			//	foreach (var otherCluster in eooue)
			//	{
			//		graph.UpdateEntrance(cluster, otherCluster);
			//	}

			//	//var dirtyNodes = new List<Node>();

			//	//foreach (var cell in pair.Value)
			//	//{
			//	//	var node = pair.Key.Nodes.SingleOrDefault(n => n.CPos == cell);

			//	//	if (node != null)
			//	//	{
			//	//		dirtyNodes.Add(node);
			//	//	}
			//	//}

			//	//pair.Key.Update(pair.Value);
			//}
		}

		void Build(World world)
		{

			graph.Build();

			Clusters = graph.Clusters;
		}

		public void Tick(Actor self)
		{
			//self.World.AddFrameEndTask(s =>
			//{
			//	Build(self.World);
			//});
		}
	}

	public class Cluster
	{
		readonly World world;
		readonly Locomotor locomotor;
		public int2 Id { get; private set; }
		public int2 TopLeft { get; private set; }
		public int2 BottomRight { get; private set; }
		public LinkedList<Node> Nodes = new LinkedList<Node>();
		public List<Cluster> Clusters = new List<Cluster>();



		readonly Dictionary<Tuple<CPos, CPos>, bool> distanceCalculated = new Dictionary<Tuple<CPos, CPos>, bool>();
		public readonly ClusterBoundaries ClusterBoundaries;

		public Cluster(World world, Locomotor locomotor, int2 id, int2 topLeft, int2 bottomRight)
		{
			this.world = world;
			this.locomotor = locomotor;
			Id = id;
			TopLeft = topLeft;
			BottomRight = bottomRight;
			ClusterBoundaries = new ClusterBoundaries(TopLeft.X, TopLeft.Y, BottomRight.X, BottomRight.Y);
		}

		public void CreateIntraClusterEdges(bool x)
		{
			distanceCalculated.Clear();


			//var baseCellCost = new SimpleBaseCost(LayerPoolForWorld(world));
			//var graph = new HPathGraph(boundaries, baseCellCost, li, world);


			var graph = x ? PathSearch.Get(world, locomotor.Info, ClusterBoundaries) : new HiarhialGraph(this);

			var dijkstra = new Dijkstra(graph);

			var pos = Nodes.Select(n => n.CPos).ToArray();

			foreach (var node1 in Nodes)
			{
				// TODO: remove prev node1 from pos
				var paths = dijkstra.Search(node1.CPos, pos);

				foreach (var path in paths)
				{
					var tuple = Tuple.Create(node1.CPos, path.First);
					var invtuple = Tuple.Create(path.First, node1.CPos);

					if (distanceCalculated.ContainsKey(tuple))
						continue;


					var toEntrancePoint = Nodes.SingleOrDefault(n => n.CPos == path.First);

					var edge1 = new Edge(node1, toEntrancePoint, EdgeType.Intra, path.Second);
					node1.AddEdge(edge1);



					var edge2 = new Edge(toEntrancePoint, node1, EdgeType.Intra, path.Second);
					toEntrancePoint.AddEdge(edge2);

					distanceCalculated[tuple] = distanceCalculated[invtuple] = true;
				}

				graph.Reset();
				//foreach (var node2 in Nodes)
				//{
				//	ComputePathBetweenEntrances(world, graph, pathFinder, node1, node2);
				//}
			}
			//foreach (var point1 in Entrances)
			//	foreach (var point2 in Entrances)
			//		ComputePathBetweenEntrances(point1, point2);
		}

		//void ComputePathBetweenEntrances(World world, IGraph<CellInfo> graph, IPathFinder pathFinder, Node fromEntrancePoint, Node toEntrancePoint)
		//{
		//	if (fromEntrancePoint.CPos == toEntrancePoint.CPos)
		//		return;



		//	//var startNodeId = Id<ConcreteNode>.From(GetEntrancePositionIndex(fromEntrancePoint));
		//	//var targetNodeId = Id<ConcreteNode>.From(GetEntrancePositionIndex(toEntrancePoint));
		//	//var search = new AStar<ConcreteNode>(SubConcreteMap, startNodeId, targetNodeId);
		//	//var pathSearch = PathSearch.Get(world, graph, fromEntrancePoint.CPos, toEntrancePoint.CPos);

				
		//	//var path = pathFinder.FindPath(pathSearch);


		//	if (path.PathNodes.Count != 0)
		//	{
		//		var edge1 = new Edge(fromEntrancePoint, toEntrancePoint, EdgeType.Intra, path.PathCost);
		//		fromEntrancePoint.AddEdge(edge1);

		//		var edge2 = new Edge(toEntrancePoint, fromEntrancePoint, EdgeType.Intra, path.PathCost);
		//		toEntrancePoint.AddEdge(edge2);
		//		// Yeah, we are supposing reaching A - B is the same like reaching B - A. Which
		//		// depending on the game this is NOT necessarily true (e.g climbing, downstepping a mountain)
		//		//_distances[tuple] = _distances[invtuple] = path.PathCost;
		//		//_cachedPaths[tuple] = new List<Id<ConcreteNode>>(path.PathNodes);
		//		//path.PathNodes.Reverse();
		//		//_cachedPaths[invtuple] = path.PathNodes;


				
		//	}

		//	pathSearch.Graph.Reset();
		//	distanceCalculated[tuple] = distanceCalculated[invtuple] = true;
		//}



		public Node AddNode(CPos pos)
		{
			var node = Nodes.SingleOrDefault(n => n.CPos == pos);

			if (node == null)
			{
				node = new Node(Id, pos);
				Nodes.AddLast(node);
			}

			return node;
		}

		public void RemoveNode(Node node)
		{
			// remove all edges connected to this node
			foreach (var edge in node.Edges)
			{
				var otherNode = edge.To;

				var edgesToRemove = otherNode.Edges.Where(e => e.To.CPos == node.CPos).ToArray();

				foreach (var edge1 in edgesToRemove)
				{
					otherNode.Edges.Remove(edge1);
				}

				//foreach (var otherNodeEdge in otherNode.Edges)
				//{
				//	if (otherNodeEdge.To.CPos == node.CPos)
				//	{
				//		otherNode.Edges.Remove(otherNodeEdge);
				//	}

				//}
				//otherNode.Edges. .RemoveAll(e => e.To.CPos == node.CPos);

				//if (edge.To != node)
				//{
				//	var otherNode = edge.To;
				//	otherNode.Edges.Remove(edge);
				//}
				//else
				//{
				//	var otherNode = edge.From;
				//	otherNode.Edges.Remove(edge);
				//}
			}

			Nodes.Remove(node);
		}

		public void RemoveIntraEdges()
		{
			foreach (var node in Nodes)
			{
				var edgesToRemove = node.Edges.Where(e => e.EdgeType == EdgeType.Intra).ToArray();
				foreach (var edge in edgesToRemove)
				{

					node.Edges.Remove(edge);
				}
				//node.Edges.RemoveAll(e => e.EdgeType == EdgeType.Intra);
			}
		}

		public void Update(HashSet<CPos> cells)
		{
			var dirtyNodes = new List<Node>();

			foreach (var cell in cells)
			{
				var node = Nodes.SingleOrDefault(n => n.CPos == cell);

				if (node != null)
				{
					dirtyNodes.Add(node);
				}
			}
		}

		public bool Contains(Cluster other)
		{
			return ClusterBoundaries.Contains(other.ClusterBoundaries);

		}

		public void AddCluster(Cluster cluster)
		{
			Clusters.Add(cluster);
		}

		public bool Contains(Node node)
		{
			return ClusterBoundaries.Contains(node.CPos.X, node.CPos.Y);

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
		readonly World world;
		readonly Locomotor locomotor;
		readonly int maxLevel;
		const int MAX_ENTRANCE_WIDTH = 6;

		const int ClusterSize = 10;
		public List<Cluster>[] Clusters;

		public HierarchicalGraph(World world, Locomotor locomotor, int maxLevel)
		{
			map = world.Map;
			this.world = world;
			this.locomotor = locomotor;
			this.maxLevel = maxLevel;

			 Clusters = new List<Cluster>[maxLevel];
		}

		public void Build()
		{

			var clusterSize = ClusterSize;
			for (int level = 0; level < maxLevel; level++)
			{
				if (level != 0)
					clusterSize *= 3;

				Clusters[level] = BuildCluster(level, clusterSize);
			}
		}

		
		public List<Cluster> BuildCluster(int level, int clusterSize)
		{
			var hierarchicalMap = new HierarchicalMap(map);
			var clusters = new List<Cluster>();

			for (int top = 0, y = 0; top < map.MapSize.Y; top += clusterSize, y++)
			for (int left = 0, x = 0; left < map.MapSize.X; left += clusterSize, x++)
			{
					var width = Math.Min(clusterSize, map.MapSize.X - left);
					var height = Math.Min(clusterSize, map.MapSize.Y - top);

					var topLeft = new int2(left, top);
					var bottomRight = new int2(left + width - 1, top + height - 1);


					var cluster = new Cluster(world, locomotor, new int2(x, y), topLeft, bottomRight);

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
			var top = cluster.TopLeft.Y;
			var left = cluster.TopLeft.X;

			if (clusterAbove != null)
			{
				CreateEntrancesOnTop(
										left,
										cluster.BottomRight.X,//left + cluster.Width - 1,
										top,
										clusterAbove,
										cluster
										 );
			}

			if (clusterOnLeft != null)
			{
				CreateEntrancesOnLeft(
					top,
					cluster.BottomRight.Y, //top + cluster.Height - 1,
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

				if (size > MAX_ENTRANCE_WIDTH)
				{
					var nodes = getNodesInEdge(entranceStart);
					var srcNode = nodes.Item1;
					var destNode = nodes.Item2;

					var node1 = precedentCluster.AddNode(srcNode);

					var node2 = currentCluster.AddNode(destNode);

					var edge1 = new Edge(node1, node2, EdgeType.Inter, 1);
					node1.AddEdge(edge1);
					var edge2 = new Edge(node2, node1, EdgeType.Inter, 1);
					node2.AddEdge(edge2);

					nodes = getNodesInEdge(entranceEnd);
					srcNode = nodes.Item1;
					destNode = nodes.Item2;

					node1 = precedentCluster.AddNode(srcNode);

					node2 = currentCluster.AddNode(destNode);

					edge1 = new Edge(node1, node2, EdgeType.Inter, 1);
					node1.AddEdge(edge1);
					edge2 = new Edge(node2, node1, EdgeType.Inter, 1);
					node2.AddEdge(edge2);

				}
				else
				{
					var nodes = getNodesInEdge((entranceEnd + entranceStart) / 2);
					var srcNode = nodes.Item1;
					var destNode = nodes.Item2;

					var node1 = precedentCluster.AddNode(srcNode);


					var node2 = currentCluster.AddNode(destNode);

					var edge1 = new Edge(node1, node2, EdgeType.Inter, 1);
					node1.AddEdge(edge1);
					var edge2 = new Edge(node2, node1, EdgeType.Inter, 1);
					node2.AddEdge(edge2);
				}

				entranceStart = entranceEnd;
			}
		}

		int GetEntranceSize(int entranceStart, int end, Func<int, Tuple<CPos, CPos>> getNodesInEdge)
		{
			var size = 0;
			while (entranceStart + size <= end && EntranceIsOpen(entranceStart + size, getNodesInEdge))
			{
				size++;
			}

			return size;
		}

		bool EntranceIsOpen(int entrancePoint, Func<int, Tuple<CPos, CPos>> getNodesInEdge)
		{
			var nodes = getNodesInEdge(entrancePoint);

			return locomotor.Info.CanEnterCell(world, null, nodes.Item1, null) && 
			       locomotor.Info.CanEnterCell(world, null, nodes.Item2, null);
			//return nodes.Item1.Info.IsObstacle || nodes.Item2.Info.IsObstacle;
		}

		CPos GetNode(int left, int top)
		{
			return new CPos(left, top);
			//return null; //map.AllCells.
			//return _concreteMap.Graph.GetNode(_concreteMap.GetNodeIdFromPos(left, top));
		}

		public void UpdateEntrance(Cluster cluster1, Cluster cluster2)
		{
			// cluster to the left
			if (cluster1.TopLeft.X > cluster2.TopLeft.X || cluster1.TopLeft.X < cluster2.TopLeft.X)
			{
				var clusterLeft = cluster1.TopLeft.X > cluster2.TopLeft.X ? cluster2 : cluster1;
				var cluster = cluster1.TopLeft.X > cluster2.TopLeft.X ? cluster1 : cluster2;

				var top = cluster.TopLeft.Y;
				var left = cluster.TopLeft.X;

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
			if (cluster1.TopLeft.Y > cluster2.TopLeft.Y || cluster1.TopLeft.Y < cluster2.TopLeft.Y)
			{
				var clusterOnTop = cluster1.TopLeft.Y > cluster2.TopLeft.Y ? cluster2 : cluster1;
				var cluster = cluster1.TopLeft.Y > cluster2.TopLeft.Y ? cluster1 : cluster2;

				var top = cluster.TopLeft.Y;
				var left = cluster.TopLeft.X;

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
				var i = occupiedCell.First.X / 10;
				var j = occupiedCell.First.Y / 10;

				var cluster = GetCluster(clusters,0, i, j);

				// Todo: could be multiple nodes on this cell...
				var node = cluster.Nodes.SingleOrDefault(n => n.CPos == occupiedCell.First);

				if (node != null)
				{
					foreach (var edge in node.Edges.Where(e => e.EdgeType == EdgeType.Inter))
					{
						var i1 = edge.To.CPos.X / 10;
						var j1 = edge.To.CPos.Y / 10;

						var cluster2 = GetCluster(clusters, 0, i1, j1);

						updateCluster.AddClusters(cluster, cluster2);
					}
				}
				else
				{

				}





				//if (!updateCluster.ContainsKey(cluster))
				//{
				//	var hashSet = new HashSet<Node> {node};

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


	public struct ClusterBoundaries
	{
		readonly int left;
		readonly int top;
		readonly int right;
		readonly int bottom;

		public ClusterBoundaries(int left, int top, int right, int bottom)
		{
			this.left = left;
			this.top = top;
			this.right = right;
			this.bottom = bottom;
		}

		public bool Contains(int x, int y)
		{
			return x >= left && x <= right && y >= top && y <= bottom;
		}

		public bool Contains(ClusterBoundaries other)
		{
			return other.left >= left &&
					other.top >= top &&
					other.right <= right &&
					other.bottom <= bottom;
		}
	}

	public class UpdateClusterEdges
	{
		public List<Pair<Cluster, Cluster>> Pairs = new List<Pair<Cluster, Cluster>>();
		public HashSet<Cluster> UpdatedClusters = new HashSet<Cluster>();

		private readonly HashSet<Pair<int2, int2>> addedPairs = new HashSet<Pair<int2, int2>>();

		public void AddClusters(Cluster cluster1, Cluster cluster2)
		{
			var id = Pair.New(cluster1.Id, cluster2.Id);

			if(addedPairs.Contains(id))
				return;

			UpdatedClusters.Add(cluster1);
			UpdatedClusters.Add(cluster2);

			Pairs.Add(Pair.New(cluster1, cluster2));

			addedPairs.Add(id);
			addedPairs.Add(Pair.New(cluster2.Id, cluster1.Id));
		}
	}

	public class HiarhialGraph : IGraph<NodeInfo>
	{
		readonly Cluster cluster;
		readonly IDictionary<CPos, Node> nodes = new Dictionary<CPos, Node>();
		readonly IDictionary<CPos, NodeInfo> nodeInfos = new Dictionary<CPos, NodeInfo>();

		public HiarhialGraph(Cluster cluster)
		{
			this.cluster = cluster;

			foreach (var subCluster in cluster.Clusters)
			{
				foreach (var node in subCluster.Nodes)
				{
					nodes.Add(node.CPos, node);
				}
			}
		}

		public void Dispose()
		{
			
		}

		public List<GraphConnection> GetConnections(CPos position)
		{
			var node = nodes[position];

			var validNeighbors = new List<GraphConnection>(node.Edges.Count);
			foreach (var edge in node.Edges)
			{
				//
				if (!cluster.Contains(edge.To))
					continue;

				validNeighbors.Add(new GraphConnection(edge.To.CPos, edge.Cost));
			}

			return validNeighbors;
		}

		public NodeInfo this[CPos pos]
		{
			get
			{
				return nodeInfos.ContainsKey(pos)
					? nodeInfos[pos]
					: new NodeInfo(int.MaxValue, int.MaxValue, pos, NodeStatus.Unvisited); }
			set { nodeInfos[pos] = value; }
		}

		public Func<CPos, bool> CustomBlock { get; set; }
		public Func<CPos, int> CustomCost { get; set; }
		public int LaneBias { get; set; }
		public bool InReverse { get; set; }
		public Actor IgnoreActor { get; set; }
		public World World { get; private set; }
		public Actor Actor { get; private set; }
		public void Reset()
		{
			nodeInfos.Clear();
		}
	}
}