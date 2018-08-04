using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Benchmark.CellMomentCostIndex
{

	public class Cache2
	{
		readonly IDictionary<Container, int> _cache = new Dictionary<Container, int>();


		public void WorldLoaded(List<LocomotorMap> locomotorMaps)
		{
			foreach (var locomotorMap in locomotorMaps)
			{
				foreach (var locomotorMapCPose in locomotorMap.MapCells)
				{
					_cache.Add(new Container(locomotorMap.Name, locomotorMapCPose.First), locomotorMapCPose.Second);
				}
			}
		}

		public int Get(string locomotorInfo, CPos destNode)
		{
			int cost = 0;
			var found = _cache.TryGetValue(new Container(locomotorInfo, destNode), out cost);

			if (found)
			{
				return cost;
			}

			return int.MaxValue;
		}
	}

	struct Container
	{
		public string Name;
		public CPos Cell;

		public Container(string name, CPos cell)
		{
			Name = name;
			Cell = cell;
		}

		public override int GetHashCode()
		{
			return new Tuple<string, int, int>(Name, Cell.X, Cell.Y).GetHashCode();
		}
	}
}