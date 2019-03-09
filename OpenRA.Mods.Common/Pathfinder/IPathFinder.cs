using OpenRA.Traits;

namespace OpenRA.Mods.Common.Pathfinder
{
	public interface IPathFinder
	{
		/// <summary>
		/// Calculates a path for the actor from source to destination
		/// </summary>
		/// <returns>A path from start to target</returns>
		Path FindUnitPath(CPos source, CPos target, Actor self, Actor ignoreActor);

		Path FindUnitPathToRange(CPos source, SubCell srcSub, WPos target, WDist range, Actor self);

		/// <summary>
		/// Calculates a path given a search specification
		/// </summary>
		Path FindPath(IPathSearch search);

		/// <summary>
		/// Calculates a path given two search specifications, and
		/// then returns a path when both search intersect each other
		/// TODO: This should eventually disappear
		/// </summary>
		Path FindBidiPath(IPathSearch fromSrc, IPathSearch fromDest);
	}
}