using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;
using Color = OpenRA.Primitives.Color;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Required for the A* PathDebug from DeveloperMode. Attach this to the world actor.")]
	public class PathfinderDebugOverlayInfo : TraitInfo<PathfinderDebugOverlay> { }

	public class PathfinderDebugOverlay : IRenderAboveShroud, IWorldLoaded
	{
		Dictionary<Actor, CellLayer<int>> layers;
		int refreshTick;
		World world;
		public bool Visible;

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			world = w;
			refreshTick = 0;
			layers = new Dictionary<Actor, CellLayer<int>>(8);

			// Enabled via Cheats menu
			Visible = true;
		}

		public void Clear(Actor actor)
		{
			CellLayer<int> layer;
			if (layers.TryGetValue(actor, out layer))
			{
				layer.Clear(0);
			}
		}

		public void AddLayer(Actor actor, IEnumerable<Pair<CPos, int>> cellWeights, int maxWeight, Player pl)
		{
			if (maxWeight == 0) return;

			CellLayer<int> layer;
			if (!layers.TryGetValue(actor, out layer))
			{
				layer = new CellLayer<int>(world.Map);
				layers.Add(actor, layer);
			}

			foreach (var p in cellWeights)
				layer[p.First] = Math.Min(128, layer[p.First] + (maxWeight - p.Second) * 64 / maxWeight);
		}

		/*
		public void Render(WorldRenderer wr)
		{
			if (!Visible)
				return;
			var map = wr.World.Map;
			var qr = Game.Renderer.WorldQuadRenderer;
			var doDim = refreshTick - world.WorldTick <= 0;
			if (doDim) refreshTick = world.WorldTick + 20;

			foreach (var pair in layers)
			{
				var c = (pair.Key != null) ? pair.Key.Color.RGB : Color.PaleTurquoise;
				var layer = pair.Value;

				// Only render quads in viewing range:
				foreach (var cell in wr.Viewport.VisibleCells)
				{
					if (layer[cell] <= 0)
						continue;

					var w = Math.Max(0, Math.Min(layer[cell], 128));
					if (doDim)
						layer[cell] = layer[cell] * 5 / 6;

					// TODO: This doesn't make sense for isometric terrain
					var pos = wr.World.Map.CenterOfCell(cell);
					var tl = wr.ScreenPxPosition(pos - new WVec(512, 512, 0));
					var br = wr.ScreenPxPosition(pos + new WVec(511, 511, 0));
					qr.FillRect(RectangleF.FromLTRB(tl.X, tl.Y, br.X, br.Y), Color.FromArgb(w, c));
				}

				foreach (var uv in wr.Viewport.AllVisibleCells.CandidateMapCoords)
				{
					if (layer[uv] <= 0)
						continue;

					if (!map.Height.Contains(uv) || self.World.ShroudObscures(uv))
						continue;

					var height = (int)map.Height[uv];
					var tile = map.Tiles[uv];
					var ti = tileSet.GetTileInfo(tile);
					var ramp = ti != null ? ti.RampType : 0;

					var corners = map.Grid.CellCorners[ramp];
					var pos = map.CenterOfCell(uv.ToCPos(map));
					var width = 1;

					// Colors change between points, so render separately
					for (var i = 0; i < 4; i++)
					{
						var j = (i + 1) % 4;
						var start = pos + corners[i];
						var end = pos + corners[j];

						yield return new PolygonAnnotationRenderable()
						yield return new LineAnnotationRenderable(start, end, width, startColor, endColor);
					}
				}
			}
		}
		*/

		public IEnumerable<IRenderable> RenderAboveShroud(Actor self, WorldRenderer wr)
		{
			if (!Visible)
				yield break;

			var map = wr.World.Map;
			var tileSet = wr.World.Map.Rules.TileSet;
			var doDim = refreshTick - world.WorldTick <= 0;
			if (doDim) refreshTick = world.WorldTick + 20;
			var c = Color.PaleTurquoise;
			var actor = world.Selection.Actors.FirstOrDefault();

			if (actor == null)
				yield break;

			CellLayer<int> layer;
			if (layers.TryGetValue(actor, out layer))
			{
				foreach (var uv in wr.Viewport.AllVisibleCells.CandidateMapCoords)
				{
					if (!map.Height.Contains(uv) || self.World.ShroudObscures(uv))
						continue;

					if (layer[uv] <= 0)
						continue;

					var w = 200; // sMath.Max(0, Math.Min(layer[uv], 128));

					var tile = map.Tiles[uv];
					var ti = tileSet.GetTileInfo(tile);
					var ramp = ti != null ? ti.RampType : 0;

					var corners = map.Grid.CellCorners[ramp];
					var pos = map.CenterOfCell(uv.ToCPos(map));

					var topLeft = pos + corners[0];
					var topRight = pos + corners[1];
					var bottomRight = pos + corners[2];
					var bottomLeft = pos + corners[3];

					yield return new FillRectAnnotationRenderable(topLeft, topRight, bottomRight, bottomLeft, pos, Color.FromArgb(w, c));
				}
			}
		}

		public bool SpatiallyPartitionable { get; private set; }
	}
}
