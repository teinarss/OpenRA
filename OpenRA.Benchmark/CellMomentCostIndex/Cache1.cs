using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Benchmark.CellMomentCostIndex
{

	public class Cache1
	{
		readonly IDictionary<Pair<string, CPos>, int> _cache = new Dictionary<Pair<string, CPos>, int>();


		public void WorldLoaded(List<LocomotorMap> locomotorMaps)
		{
			foreach (var locomotorMap in locomotorMaps)
			{
				foreach (var locomotorMapCPose in locomotorMap.MapCells)
				{
					_cache.Add(new Pair<string, CPos>(locomotorMap.Name, locomotorMapCPose.First), locomotorMapCPose.Second);
				}
			}
		}

		public int Get(string locomotorInfo, CPos destNode)
		{
			int cost = 0;
			var found = _cache.TryGetValue(new Pair<string, CPos>(locomotorInfo, destNode), out cost);

			if (found)
			{
				return cost;
			}

			return int.MaxValue;
		}
	}

	public class LocomotorMap
	{
		public string Name { get; set; }
		public List<Pair<CPos,int>> MapCells { get; set; }
	}
}