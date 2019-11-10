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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Commands;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Renders a debug overlay showing the terrain cells. Attach this to the world actor.")]
	public class ClusterOverlayInfo : TraitInfo<ClusterOverlay> { }

	public class ClusterOverlay : IRenderAboveWorld, IWorldLoaded, IChatCommand
	{
		const string CommandName = "clusteroverlay";
		const string CommandDesc = "toggles the terrain geometry overlay.";

		public bool Enabled;
		int level = 0;
		Color edgeColor;

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var console = w.WorldActor.TraitOrDefault<ChatCommands>();
			var help = w.WorldActor.TraitOrDefault<HelpCommand>();

			if (console == null || help == null)
				return;

			console.RegisterCommand(CommandName, this);
			help.RegisterHelp(CommandName, CommandDesc);
		}

		void IChatCommand.InvokeCommand(string name, string arg)
		{
			if (name == CommandName)
			{
				switch (arg)
				{
					case "off":
						Enabled = false;
						break;
					case "1":
						Enabled = true;
						level = int.Parse(arg);
						break;

					default:
						Enabled = true;
						level = 0;
						break;
				}
			}
		}

		Color[] componentColors = new Color[] { Color.FromArgb(128, 255, 0, 0), Color.FromArgb(128, 0, 255, 0), Color.FromArgb(128, 0, 0, 255) };
		List<AbstractPath> path;

		void IRenderAboveWorld.RenderAboveWorld(Actor self, WorldRenderer wr)
		{
			if (!Enabled)
				return;
			var map = wr.World.Map;
			var wcr = Game.Renderer.WorldRgbaColorRenderer;
			var locomotor = self.TraitsImplementing<Locomotor>().SingleOrDefault(l => l.Info.Name == "wheeled");
			var topLeftCorner = map.Grid.CellCorners[0][0];
			var topRightCorner = map.Grid.CellCorners[0][1];
			var bottomRightCorner = map.Grid.CellCorners[0][2];
			var bottomLeftCorner = map.Grid.CellCorners[0][3];
			var tileSet = wr.World.Map.Rules.TileSet;
			var width = 1 / wr.Viewport.Zoom;

			var color = Color.FromArgb(232, 12, 131);
			edgeColor = Color.FromArgb(48, 232, 12);

			var edgeDrawn = new HashSet<Tuple<CPos, CPos>>();

			var componentColor = 0;

			var locomotorClustersManager = locomotor.ClustersManager;
			foreach (var cluster in locomotorClustersManager.Clusters)
			{
				var boundaries = cluster.Boundaries;
				var tl = GetSceenPos(wr, boundaries.Left, boundaries.Top, map, topLeftCorner);
				var tr = GetSceenPos(wr, boundaries.Right, boundaries.Top, map, topRightCorner);
				var br = GetSceenPos(wr, boundaries.Right, boundaries.Bottom, map, bottomRightCorner);
				var bl = GetSceenPos(wr, boundaries.Left, boundaries.Bottom, map, bottomLeftCorner);

				wcr.DrawLine(tl, tr, width, color, color);
				wcr.DrawLine(tr, br, width, color, color);
				wcr.DrawLine(br, bl, width, color, color);
				wcr.DrawLine(bl, tl, width, color, color);

				foreach (var id in cluster.Components)
				{
					var component = locomotorClustersManager.GetComponent(id);

					foreach (var cell in component.Cells)
					{
						var tile = map.Tiles[cell];
						var ti = tileSet.GetTileInfo(tile);
						var ramp = ti != null ? ti.RampType : 0;

						var corners = map.Grid.CellCorners[ramp];
						var pos = map.CenterOfCell(cell);

						var topLeft = pos + corners[0];
						var topRight = pos + corners[1];
						var bottomRight = pos + corners[2];
						var bottomLeft = pos + corners[3];

						var tl1 = wr.Screen3DPosition(topLeft);
						var tr1 = wr.Screen3DPosition(topRight);

						var br1 = wr.Screen3DPosition(bottomRight);
						var bl1 = wr.Screen3DPosition(bottomLeft);

						wcr.FillRect(tl1, tr1, br1, bl1, componentColors[componentColor]);
					}

					componentColor++;

					if (componentColor >= componentColors.Length)
						componentColor = 0;

					foreach (var node in component.Entrances)
					{
						var pos = map.CenterOfCell(node);
						var wpos = wr.Screen3DPxPosition(pos);

						DrawNode(wr, Color.Orange, wpos);

						var edges = locomotorClustersManager.Graph.Edges(node);

						foreach (var edge in edges)
						{
							var tuple1 = Tuple.Create(node, edge.To);
							var tuple2 = Tuple.Create(edge.To, node);

							if (edgeDrawn.Contains(tuple1))
								continue;

							RenderEdge(wr, map, edge, node);

							edgeDrawn.Add(tuple1);
							edgeDrawn.Add(tuple2);
						}
					}
				}

				if (path != null && path.Count > 0)
				{
					var iz = 3 / wr.Viewport.Zoom;
					var a = GetScreenPos(wr, path.First().Exit, map);
					foreach (var b in path.Skip(1).Select(pos => GetScreenPos(wr, pos.Exit, map)))
					{
						Game.Renderer.WorldRgbaColorRenderer.DrawLine(a, b, iz, Color.Red);
						a = b;
					}
				}
			}
		}

		public static void DrawNode(WorldRenderer wr, Color color, float3 location)
		{
			var iz = 2 / wr.Viewport.Zoom;
			var offset = new float2(iz, iz);
			var tl = location - offset;
			var br = location + offset;
			Game.Renderer.WorldRgbaColorRenderer.FillRect(tl, br, color);
		}

		public void RenderEdge(WorldRenderer wr, Map map, Edge edge, CPos node)
		{
			var iz = 1 / wr.Viewport.Zoom;

			var first = GetScreenPos(wr, node, map);
			var last = GetScreenPos(wr, edge.To, map);

			if (edge.EdgeType == EdgeType.Intra)
			{
				var a = first;
				foreach (var b in edge.Path.Select(pos => GetScreenPos(wr, pos, map)))
				{
					Game.Renderer.WorldRgbaColorRenderer.DrawLine(a, b, iz, Color.CornflowerBlue);
					DrawTargetMarker(wr, Color.CornflowerBlue, b);
					a = b;
				}

				Game.Renderer.WorldRgbaColorRenderer.DrawLine(a, last, iz, Color.CornflowerBlue);

				// DrawTargetMarker(wr, Color.Green, last);
				// DrawTargetMarker(wr, Color.Green, first);
			}
			else
			{
				Game.Renderer.WorldRgbaColorRenderer.DrawLine(first, last, iz, edgeColor);
			}
		}

		public static void DrawTargetMarker(WorldRenderer wr, Color color, float3 location)
		{
			var iz = 1 / wr.Viewport.Zoom;
			var offset = new float2(iz, iz);
			var tl = location - offset;
			var br = location + offset;
			Game.Renderer.WorldRgbaColorRenderer.FillRect(tl, br, color);
		}

		static float3 GetScreenPos(WorldRenderer wr, CPos pos, Map map)
		{
			var worldPos = map.CenterOfCell(pos);
			var tl = wr.Screen3DPxPosition(worldPos);

			return tl;
		}

		static float3 GetSceenPos(WorldRenderer wr, int x, int y, Map map, WVec corner)
		{
			var topLeft = new CPos(x, y);

			var topLeftWPos = map.CenterOfCell(topLeft);
			var tl = wr.Screen3DPxPosition(topLeftWPos + corner);

			return tl;
		}

		public void AddPath(List<AbstractPath> path)
		{
			this.path = path;
		}
	}
}
