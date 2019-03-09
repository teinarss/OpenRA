using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Pathfinder
{
	public class PathFinder : IPathFinder
	{
		static readonly Path EmptyPath = new Path(new List<CPos>(0), 0);
		readonly World world;
		DomainIndex domainIndex;
		bool cached;

		public PathFinder(World world)
		{
			this.world = world;
		}

		public Path FindUnitPath(CPos source, CPos target, Actor self, Actor ignoreActor)
		{
			var li = self.Info.TraitInfo<MobileInfo>().LocomotorInfo;
			if (!cached)
			{
				domainIndex = world.WorldActor.TraitOrDefault<DomainIndex>();
				cached = true;
			}

			// If a water-land transition is required, bail early
			if (domainIndex != null && !domainIndex.IsPassable(source, target, li))
				return EmptyPath;

			var distance = source - target;
			if (distance.LengthSquared < 3 && li.CanMoveFreelyInto(world, self, target, null, CellConditions.All))
				return new Path(new List<CPos> { target }, 0);

			Path pb;
			using (var fromSrc = PathSearch.FromPoint(world, li, self, target, source, true).WithIgnoredActor(ignoreActor))
			using (var fromDest = PathSearch.FromPoint(world, li, self, source, target, true).WithIgnoredActor(ignoreActor).Reverse())
				pb = FindBidiPath(fromSrc, fromDest);

			return pb;
		}

		public Path FindUnitPathToRange(CPos source, SubCell srcSub, WPos target, WDist range, Actor self)
		{
			if (!cached)
			{
				domainIndex = world.WorldActor.TraitOrDefault<DomainIndex>();
				cached = true;
			}

			var mi = self.Info.TraitInfo<MobileInfo>();
			var li = mi.LocomotorInfo;
			var targetCell = world.Map.CellContaining(target);

			// Correct for SubCell offset
			target -= world.Map.Grid.OffsetOfSubCell(srcSub);

			// Select only the tiles that are within range from the requested SubCell
			// This assumes that the SubCell does not change during the path traversal
			var tilesInRange = world.Map.FindTilesInCircle(targetCell, range.Length / 1024 + 1)
				.Where(t => (world.Map.CenterOfCell(t) - target).LengthSquared <= range.LengthSquared
				            && mi.CanEnterCell(self.World, self, t));

			// See if there is any cell within range that does not involve a cross-domain request
			// Really, we only need to check the circle perimeter, but it's not clear that would be a performance win
			if (domainIndex != null)
			{
				tilesInRange = new List<CPos>(tilesInRange.Where(t => domainIndex.IsPassable(source, t, li)));
				if (!tilesInRange.Any())
					return EmptyPath;
			}

			using (var fromSrc = PathSearch.FromPoints(world, li, self, tilesInRange, source, true))
			using (var fromDest = PathSearch.FromPoint(world, li, self, source, targetCell, true).Reverse())
				return FindBidiPath(fromSrc, fromDest);
		}

		public Path FindPath(IPathSearch search)
		{
			Path path = null;

			while (search.CanExpand)
			{
				var p = search.Expand();
				if (search.IsTarget(p))
				{
					path = MakePath(search.Graph, p);
					break;
				}
			}

			search.Graph.Dispose();

			if (path != null)
				return path;

			// no path exists
			return EmptyPath;
		}

		// Searches from both ends toward each other. This is used to prevent blockings in case we find
		// units in the middle of the path that prevent us to continue.
		public Path FindBidiPath(IPathSearch fromSrc, IPathSearch fromDest)
		{
			Path path = null;

			while (fromSrc.CanExpand && fromDest.CanExpand)
			{
				// make some progress on the first search
				var p = fromSrc.Expand();
				if (fromDest.Graph[p].Status == CellStatus.Closed &&
				    fromDest.Graph[p].CostSoFar < int.MaxValue)
				{
					path = MakeBidiPath(fromSrc, fromDest, p);
					break;
				}

				// make some progress on the second search
				var q = fromDest.Expand();
				if (fromSrc.Graph[q].Status == CellStatus.Closed &&
				    fromSrc.Graph[q].CostSoFar < int.MaxValue)
				{
					path = MakeBidiPath(fromSrc, fromDest, q);
					break;
				}
			}

			fromSrc.Graph.Dispose();
			fromDest.Graph.Dispose();

			if (path != null)
				return path;

			return EmptyPath;
		}

		// Build the path from the destination. When we find a node that has the same previous
		// position than itself, that node is the source node.
		static Path MakePath(IGraph<CellInfo> cellInfo, CPos destination)
		{
			var ret = new List<CPos>();
			var currentNode = destination;
			var pathCost = cellInfo[destination].CostSoFar;

			while (cellInfo[currentNode].PreviousPos != currentNode)
			{
				ret.Add(currentNode);
				currentNode = cellInfo[currentNode].PreviousPos;
			}

			ret.Add(currentNode);

			return new Path(ret, pathCost);
		}

		static Path MakeBidiPath(IPathSearch a, IPathSearch b, CPos confluenceNode)
		{
			var halfPath1 = MakePath(a.Graph, confluenceNode);
			var halfPath2 = MakePath(b.Graph, confluenceNode);

			halfPath1.PathNodes.Reverse();

			halfPath1.PathNodes.AddRange(halfPath2.PathNodes);

			//var ca = a.Graph;
			//var cb = b.Graph;

			//var ret = new List<CPos>();

			//var q = confluenceNode;
			//while (ca[q].PreviousPos != q)
			//{
			//	ret.Add(q);
			//	q = ca[q].PreviousPos;
			//}

			//ret.Add(q);

			//ret.Reverse();

			//q = confluenceNode;
			//while (cb[q].PreviousPos != q)
			//{
			//	q = cb[q].PreviousPos;
			//	ret.Add(q);
			//}

			return halfPath1;
		}
	}
}