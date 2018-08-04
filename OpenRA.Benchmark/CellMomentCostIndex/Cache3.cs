using System.Collections.Generic;
using System.Drawing;

namespace OpenRA.Benchmark.CellMomentCostIndex
{
	public class Cache3
	{
		IDictionary<string, CellLayer<int>> index = new Dictionary<string, CellLayer<int>>();

		public void WorldLoaded(List<LocomotorMap> locomotorMaps)
		{
			foreach (var locomotorMap in locomotorMaps)
			{
				var cellLayer = new CellLayer<int>(MapGridType.Rectangular, new Size(256, 256));

				foreach (var locomotorMapCPose in locomotorMap.MapCells)
				{
					cellLayer[locomotorMapCPose.First] = locomotorMapCPose.Second;
				}

				index.Add(locomotorMap.Name,  cellLayer);
			}
		}

		public int Get(string name, CPos cell)
		{
			CellLayer<int> layer;
			if (index.TryGetValue(name, out layer))
			{
				if (layer.Contains(cell))
				{
					return layer[cell];
				};
			}

			return int.MaxValue;
		}
	}
}