namespace OpenRA.Mods.Common.Pathfinder
{
	public class Edge
	{
		public Node From { get; private set; }
		public Node To { get; private set; }
		public EdgeType EdgeType { get; private set; }
		public int Cost { get; private set; }

		public Edge(Node from, Node to, EdgeType edgeType, int cost)
		{
			From = @from;
			To = to;
			EdgeType = edgeType;
			Cost = cost;
		}
	}
}