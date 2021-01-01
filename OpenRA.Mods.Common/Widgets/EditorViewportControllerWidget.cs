#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class EditorViewportControllerWidget : Widget
	{
		public IEditorBrush CurrentBrush { get; private set; }

		public readonly string TooltipContainer;
		public readonly string TooltipTemplate;
		public readonly EditorDefaultBrush DefaultBrush;

		readonly Lazy<TooltipContainerWidget> tooltipContainer;
		readonly WorldRenderer worldRenderer;
		readonly EditorActionManager editorActionManager;

		bool enableTooltips;

		[ObjectCreator.UseCtor]
		public EditorViewportControllerWidget(World world, WorldRenderer worldRenderer)
		{
			this.worldRenderer = worldRenderer;
			tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
			CurrentBrush = DefaultBrush = new EditorDefaultBrush(this, worldRenderer);
			editorActionManager = world.WorldActor.Trait<EditorActionManager>();

			editorActionManager.OnChange += EditorActionManagerOnChange;

			// Allow zooming out to full map size
			worldRenderer.Viewport.UnlockMinimumZoom(0.25f);
		}

		void EditorActionManagerOnChange()
		{
			DefaultBrush.SelectedActor = null;
		}

		public void ClearBrush() { SetBrush(null); }
		public void SetBrush(IEditorBrush brush)
		{
			CurrentBrush?.Dispose();

			CurrentBrush = brush ?? DefaultBrush;
		}

		public override void MouseEntered()
		{
			enableTooltips = true;
		}

		public override void MouseExited()
		{
			tooltipContainer.Value.RemoveTooltip();
			enableTooltips = false;
		}

		public void SetTooltip(string tooltip)
		{
			if (!enableTooltips)
				return;

			if (tooltip != null)
			{
				Func<string> getTooltip = () => tooltip;
				tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs() { { "getText", getTooltip } });
			}
			else
				tooltipContainer.Value.RemoveTooltip();
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Scroll && mi.Modifiers.HasModifier(Game.Settings.Game.ZoomModifier))
			{
				worldRenderer.Viewport.AdjustZoom(mi.Delta.Y * Game.Settings.Game.ZoomSpeed, mi.Location);
				return true;
			}

			if (CurrentBrush.HandleMouseInput(mi))
				return true;

			return base.HandleMouseInput(mi);
		}

		WPos cachedViewportPosition;
		public override void Tick()
		{
			// Clear any tooltips when the viewport is scrolled using the keyboard
			if (worldRenderer.Viewport.CenterPosition != cachedViewportPosition)
				SetTooltip(null);

			cachedViewportPosition = worldRenderer.Viewport.CenterPosition;
			CurrentBrush.Tick();
		}

		public override void Removed()
		{
			base.Removed();
			editorActionManager.OnChange -= EditorActionManagerOnChange;
		}
	}

	public class EditorGuidesWidget : Widget
	{
		int2 location;
		WorldRenderer worldRenderer;
		GuideLinesEdge topEdge;
		List<GuideLine> lines = new List<GuideLine>();
		GuideLine activeLine = null;
		int2 mapMapSize;
		bool activated;
		GuideLinesEdge leftEdge;

		public bool IsLocked { get; set; }

		[ObjectCreator.UseCtor]
		public EditorGuidesWidget(World world, WorldRenderer worldRenderer)
		{
			this.worldRenderer = worldRenderer;
			mapMapSize = world.Map.MapSize * 24;

			topEdge = new GuideLinesEdge(new int2(0, -10), new int2(mapMapSize.X, 0), Edge.Vertical);
			topEdge.OnEvent = OnEvent;

			leftEdge = new GuideLinesEdge(new int2(-10, 0), new int2(0, mapMapSize.Y), Edge.Horizontal);
			leftEdge.OnEvent = OnEvent;
		}

		void OnEvent(Edge edge)
		{
			var angle = edge == Edge.Horizontal ? Math.PI / 2 : 0;
			var guideLine = new GuideLine(mapMapSize, angle);
			lines.Add(guideLine);
			activeLine = guideLine;
		}

		public override void Tick()
		{
		}

		public override void Draw()
		{
			var font = Game.Renderer.Fonts["Regular"];

			var pos = new WPos(-1024, location.Y, location.Y);
			var text = "";
			var screenPos = worldRenderer.Viewport.Zoom * (worldRenderer.ScreenPosition(pos) - worldRenderer.Viewport.TopLeft.ToFloat2()) - 0.5f * font.Measure(text).ToFloat2();
			var screenPxPos = new float2((float)Math.Round(screenPos.X), (float)Math.Round(screenPos.Y));

			var pos1 = new float2(0, location.Y);
			font.DrawText(text, pos1, Color.LightGreen);
			var cr = Game.Renderer.RgbaColorRenderer;

			foreach (var line in lines)
			{
				line.Draw(cr, worldRenderer);
			}

			leftEdge.Draw(cr, worldRenderer);
			topEdge.Draw(cr, worldRenderer);
		}

		public override bool EventBoundsContains(int2 location)
		{
			if (IsLocked)
				return false;

			if (activated)
				return true;

			var loc = worldRenderer.Viewport.ViewToWorldPx(location);

			if (topEdge.IsOver(loc))
				return true;

			if (leftEdge.IsOver(loc))
				return true;

			foreach (var line in lines)
				if (line.Hit(loc))
					return true;

			return false;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Move)
			{
				location = worldRenderer.Viewport.ViewToWorldPx(mi.Location);
				Debug.WriteLine($"{location.X}, {location.Y}");

				if (activeLine != null)
					activeLine.UpdateLocation(location);
			}

			if (mi.Event == MouseInputEvent.Up && mi.Button == MouseButton.Right)
				DeleteLine(location);

			if (mi.Event == MouseInputEvent.Up)
			{
				activeLine = null;
				activated = false;
			}

			if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Left)
			{
				activated = true;
				activeLine = FindLine(location);
			}

			topEdge.HandleMouseInput(mi, location);
			leftEdge.HandleMouseInput(mi, location);

			return true;
		}

		GuideLine FindLine(int2 loc)
		{
			return lines.FirstOrDefault(l => l.Hit(loc));
		}

		void DeleteLine(int2 loc)
		{
			var line = FindLine(loc);

			if (line != null)
				lines.Remove(line);
		}
	}

	internal class GuideLine
	{
		const int Width = 4;
		int2 location;
		readonly int2 mapMapSize;
		double angle = Math.PI / 2;
		int2[] bounds = new[] { new int2(1, 2) };

		public GuideLine(int2 mapMapSize, double angle)
		{
			this.mapMapSize = mapMapSize;
			this.angle = angle;
		}

		public void UpdateLocation(int2 location)
		{
			this.location = location;
		}

		public bool Hit(int2 location)
		{
			return bounds.PolygonContains(location);
		}

		public void Draw(RgbaColorRenderer cr, WorldRenderer worldRenderer)
		{
			var lenght = 1000;
			var loc = worldRenderer.Viewport.WorldToViewPx(new float2(location.X, location.Y)).ToFloat2();

			var x2 = (float)(loc.X + lenght * Math.Cos(angle));
			var y2 = (float)(mapMapSize.Y * Math.Sin(angle));

			var startPos = new float3(loc.X, loc.Y, loc.Y);

			var endPos = new float3(x2, y2, y2);
			cr.DrawLine(startPos, endPos, 1, Color.Red);

			x2 = (float)(loc.X - lenght * Math.Cos(angle));
			y2 = (float)(loc.Y - lenght * Math.Sin(angle));

			startPos = new float3(loc.X, loc.Y, loc.Y);
			endPos = new float3(x2, y2, y2);
		}
	}

	class GuideLinesEdge
	{
		readonly Edge vertical;
		Rectangle bounds;
		bool dragStarted;

		public Action<Edge> OnEvent;

		public GuideLinesEdge(int2 topLeft, int2 bottomLeft, Edge vertical)
		{
			this.vertical = vertical;
			var width = bottomLeft.X - topLeft.X;
			var height = bottomLeft.Y - topLeft.Y;
			bounds = new Rectangle(topLeft.X, topLeft.Y, width, height);
		}

		public void HandleMouseInput(MouseInput mi, int2 location)
		{
			if (dragStarted && !bounds.Contains(location))
			{
				dragStarted = false;
				if (OnEvent != null)
					OnEvent(vertical);

				return;
			}

			if (!bounds.Contains(location))
				return;

			if (mi.Event == MouseInputEvent.Down)
				dragStarted = true;

			if (mi.Event == MouseInputEvent.Up)
				dragStarted = false;
		}

		public bool IsOver(int2 location)
		{
			return bounds.Contains(location);
		}

		public void Draw(RgbaColorRenderer cr, WorldRenderer worldRenderer)
		{
			var tl = worldRenderer.Viewport.WorldToViewPx(new float2(bounds.Left, bounds.Top)).ToFloat2();
			var br = worldRenderer.Viewport.WorldToViewPx(new float2(bounds.Right, bounds.Bottom)).ToFloat2();

			var color = Color.FromArgb(128, 128, 128, 128);
			cr.FillRect(new float3(tl.X, tl.Y, tl.Y), new float3(br.X, br.Y, br.Y), color);
		}
	}

	enum Edge
	{
		Vertical,
		Horizontal
	}
}
