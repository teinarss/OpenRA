using System.Collections.Generic;

namespace OpenRA.Mods.Common.Pathfinder
{
	public class Path
	{
		public List<CPos> PathNodes { get; private set; }
		public int PathCost { get; private set; }
		public static Path Empty { get {return new Path(new List<CPos>(), 0);} }

		public Path(List<CPos> pathNodes, int pathCost)
		{
			PathNodes = pathNodes;
			PathCost = pathCost;
		}
	}
}