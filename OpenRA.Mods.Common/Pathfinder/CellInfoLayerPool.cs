#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Pathfinder
{
	sealed class CellInfoLayerPool
	{
		const int MaxPoolSize = 4;
		readonly Stack<CellLayer<NodeInfo>> pool = new Stack<CellLayer<NodeInfo>>(MaxPoolSize);
		readonly CellLayer<NodeInfo> defaultLayer;

		public CellInfoLayerPool(Map map)
		{
			defaultLayer =
				CellLayer<NodeInfo>.CreateInstance(
					mpos => new NodeInfo(int.MaxValue, int.MaxValue, mpos.ToCPos(map), NodeStatus.Unvisited),
					new Size(map.MapSize.X, map.MapSize.Y),
					map.Grid.Type);
		}

		public PooledCellInfoLayer Get()
		{
			return new PooledCellInfoLayer(this);
		}

		CellLayer<NodeInfo> GetLayer()
		{
			CellLayer<NodeInfo> layer = null;
			lock (pool)
				if (pool.Count > 0)
					layer = pool.Pop();

			if (layer == null)
				layer = new CellLayer<NodeInfo>(defaultLayer.GridType, defaultLayer.Size);
			layer.CopyValuesFrom(defaultLayer);
			return layer;
		}

		void ReturnLayer(CellLayer<NodeInfo> layer)
		{
			lock (pool)
			   if (pool.Count < MaxPoolSize)
					pool.Push(layer);
		}

		public class PooledCellInfoLayer : IDisposable
		{
			CellInfoLayerPool layerPool;
			List<CellLayer<NodeInfo>> layers = new List<CellLayer<NodeInfo>>();

			public PooledCellInfoLayer(CellInfoLayerPool layerPool)
			{
				this.layerPool = layerPool;
			}

			public CellLayer<NodeInfo> GetLayer()
			{
				var layer = layerPool.GetLayer();
				layers.Add(layer);
				return layer;
			}

			public void Dispose()
			{
				if (layerPool != null)
					foreach (var layer in layers)
						layerPool.ReturnLayer(layer);

				layers = null;
				layerPool = null;
			}
		}
	}
}
