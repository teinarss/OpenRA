using System.Collections.Generic;

namespace OpenRA.Mods.Common.Pathfinder
{
	public class Node
	{
		public int2 ClusterId { get; private set; }
		public CPos CPos { get; private set; }
		public LinkedList<Edge> Edges = new LinkedList<Edge>();


		public Node(int2 clusterId, CPos cPos)
		{
			ClusterId = clusterId;
			CPos = cPos;
		}

		public void AddEdge(Edge edge)
		{
			Edges.AddLast(edge);
		}
	}
}