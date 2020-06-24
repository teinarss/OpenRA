﻿#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Cnc.Traits;
using OpenRA.Mods.Common.Commands;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	[Desc("Displays ice strength above the tile.")]
	class RenderIceStateInfo : TraitInfo, Requires<IceLayerInfo>
	{
		public readonly Color Color = Color.White;
		public readonly string Font = "TinyBold";

		public override object Create(ActorInitializer init) { return new RenderIceState(init.Self, this); }
	}

	class RenderIceState : IWorldLoaded, IChatCommand, IRenderAnnotations
	{
		const string CommandName = "debugice";
		const string CommandDesc = "Toggles the ice layer debug overlay. Optional parameter: 'allcells'";

		public bool Enabled;
		public bool AllCells;

		readonly SpriteFont font;
		readonly Color color;
		readonly IceLayer icelayer;

		public RenderIceState(Actor self, RenderIceStateInfo info)
		{
			color = info.Color;
			font = Game.Renderer.Fonts[info.Font];
			icelayer = self.Trait<IceLayer>();
		}

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
				Enabled ^= true;

			if (arg.Contains("allcells"))
				AllCells ^= true;
		}

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (!Enabled)
				yield break;

			foreach (var uv in wr.Viewport.VisibleCellsInsideBounds.CandidateMapCoords)
			{
				var cell = uv.ToCPos(wr.World.Map);
				var center = wr.World.Map.CenterOfCell(cell);
				var strength = icelayer.Strength[cell];
				if (!AllCells && strength == 0)
					continue;

				yield return new TextAnnotationRenderable(font, center, 0, color, strength.ToString());
			}
		}

		bool IRenderAnnotations.SpatiallyPartitionable { get { return false; } }
	}
}
