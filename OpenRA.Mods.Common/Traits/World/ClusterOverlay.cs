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
						level = Int32.Parse(arg);
						break;

					default:
						Enabled = true;
						level = 0;
						break;
				}
			}
		}

		void IRenderAboveWorld.RenderAboveWorld(Actor self, WorldRenderer wr)
		{
			if (!Enabled)
				return;
			var map = wr.World.Map;
			var wcr = Game.Renderer.WorldRgbaColorRenderer;
			var locomotor = self.TraitsImplementing<Locomotor>().SingleOrDefault(l => l.Info.Name == "WHEELED");
			var topLeftCorner = map.Grid.CellCorners[0][0];
			var topRightCorner = map.Grid.CellCorners[0][1];
			var bottomRightCorner = map.Grid.CellCorners[0][2];
			var bottomLeftCorner = map.Grid.CellCorners[0][3];

			var width = 1 / wr.Viewport.Zoom;

			var color = Color.FromArgb(232, 12, 131);
			edgeColor = Color.FromArgb(48, 232, 12);

			var edgeDrawn = new HashSet<Tuple<CPos, CPos>>();

			foreach (var cluster in locomotor.ClustersManager.Clusters)
			{
				var boundaries = cluster.Bounds;
				var tl = GetSceenPos(wr, boundaries.Left, boundaries.Top, map, topLeftCorner);
				var tr = GetSceenPos(wr, boundaries.Right, boundaries.Top, map, topRightCorner);
				var br = GetSceenPos(wr, boundaries.Right, boundaries.Bottom, map, bottomRightCorner);
				var bl = GetSceenPos(wr, boundaries.Left, boundaries.Bottom, map, bottomLeftCorner);

				//var screen = cellCorner.Select(c => wr.Screen3DPxPosition(pos + c)).ToArray();
				  //cluster.X.
				  //
				wcr.DrawLine(tl, tr, width, color, color);
				wcr.DrawLine(tr,br, width, color, color);
				wcr.DrawLine(br, bl, width, color, color);
				wcr.DrawLine(bl, tl, width, color, color);

				foreach (var node in cluster.Nodes)
				{
					var pos = map.CenterOfCell(node.CPos);
					var wpos = wr.Screen3DPxPosition(pos);

					DrawNode(wr, Color.Orange, wpos);

					foreach (var edge in node.Edges)
					{
						var tuple1 = Tuple.Create(edge.From.CPos, edge.To.CPos);
						var tuple2 = Tuple.Create(edge.To.CPos, edge.From.CPos);

						if (edgeDrawn.Contains(tuple1))
							continue;

						RenderEdge(wr, map, edge);
						//foreach (var cPose in edge.Path)
						//{
							
						//}

						//var e1p = map.CenterOfCell(edge.From.CPos);
						//var e1wp = wr.Screen3DPxPosition(e1p);

						//var e2p = map.CenterOfCell(edge.To.CPos);
						//var e2wp = wr.Screen3DPxPosition(e2p);

						//wcr.DrawLine(e1wp, e2wp, width, edgeColor, edgeColor);

						edgeDrawn.Add(tuple1);
						edgeDrawn.Add(tuple2);

					}
				}


			}



			//var tileSet = wr.World.Map.Rules.TileSet;

			//var colors = tileSet.HeightDebugColors;
			//var mouseCell = wr.Viewport.ViewToWorld(Viewport.LastMousePos).ToMPos(wr.World.Map);

			//foreach (var uv in wr.Viewport.AllVisibleCells.CandidateMapCoords)
			//{
			//	if (!map.Height.Contains(uv))
			//		continue;

			//	var height = (int)map.Height[uv];
			//	var tile = map.Tiles[uv];
			//	var ti = tileSet.GetTileInfo(tile);
			//	var ramp = ti != null ? ti.RampType : 0;

			//	var corners = map.Grid.CellCorners[ramp];
			//	var color = corners.Select(c => colors[height + c.Z / 512]).ToArray();
			//	var cPos = uv.ToCPos(map);


			//	var pos = map.CenterOfCell(cPos);




			//	var screen = corners.Select(c => wr.Screen3DPxPosition(pos + c)).ToArray();
			//	//var width = (uv == mouseCell ? 3 : 1) / wr.Viewport.Zoom;

			//	// Colors change between points, so render separately
			//	for (var i = 0; i < 4; i++)
			//	{
			//		var j = (i + 1) % 4;
			//		wcr.DrawLine(screen[i], screen[j], width, color[i], color[j]);
			//	}
			//}

			// Projected cell coordinates for the current cell
			//var projectedCorners = map.Grid.CellCorners[0];
			//foreach (var puv in map.ProjectedCellsCovering(mouseCell))
			//{
			//	var pos = map.CenterOfCell(((MPos)puv).ToCPos(map));
			//	var screen = projectedCorners.Select(c => wr.Screen3DPxPosition(pos + c - new WVec(0, 0, pos.Z))).ToArray();
			//	for (var i = 0; i < 4; i++)
			//	{
			//		var j = (i + 1) % 4;
			//		wcr.DrawLine(screen[i], screen[j], 3 / wr.Viewport.Zoom, Color.Navy);
			//	}
			//}
		}

		public static void DrawNode(WorldRenderer wr, Color color, float3 location)
		{
			var iz = 2 / wr.Viewport.Zoom;
			var offset = new float2(iz, iz);
			var tl = location - offset;
			var br = location + offset;
			Game.Renderer.WorldRgbaColorRenderer.FillRect(tl, br, color);
		}

		public void RenderEdge(WorldRenderer wr, Map map, Edge edge)
		{
			var iz = 1 / wr.Viewport.Zoom;

			var first = GetScreenPos(wr, edge.From.CPos, map);
			var last = GetScreenPos(wr, edge.To.CPos, map);

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

				//DrawTargetMarker(wr, Color.Green, last);
				//DrawTargetMarker(wr, Color.Green, first);
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
	}
}
