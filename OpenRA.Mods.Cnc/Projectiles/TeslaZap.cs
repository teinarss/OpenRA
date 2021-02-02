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

using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Cnc.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Projectiles
{
	public class TeslaZapInfo : IProjectileInfo
	{
		public readonly string Image = "litning";

		[SequenceReference(nameof(Image))]
		public readonly string BrightSequence = "bright";

		[SequenceReference(nameof(Image))]
		public readonly string DimSequence = "dim";

		[PaletteReference]
		public readonly string Palette = "effect";

		public readonly int BrightZaps = 1;
		public readonly int DimZaps = 2;

		public readonly int Duration = 2;

		public readonly int DamageDuration = 1;

		public readonly bool TrackTarget = true;

		public IProjectile Create(in ProjectileArgs args) { return new TeslaZap(this, args); }
	}

	public class TeslaZap : IProjectile, ISync
	{
		readonly ProjectileArgs args;
		readonly TeslaZapInfo info;
		TeslaZapRenderable zap;
		int ticksUntilRemove;
		int damageDuration;

		[Sync]
		WPos target;

		public TeslaZap(TeslaZapInfo info, in ProjectileArgs args)
		{
			this.args = args;
			this.info = info;
			ticksUntilRemove = info.Duration;
			damageDuration = info.DamageDuration > info.Duration ? info.Duration : info.DamageDuration;
			target = args.PassiveTarget;
		}

		public void Tick(World world)
		{
			ref readonly var pargs = ref args;
			if (ticksUntilRemove-- <= 0)
				world.AddFrameEndTask(w => w.Remove(this));

			// Zap tracks target
			if (info.TrackTarget && pargs.GuidedTarget.IsValidFor(pargs.SourceActor))
				target = pargs.Weapon.TargetActorCenter ? pargs.GuidedTarget.CenterPosition : pargs.GuidedTarget.Positions.PositionClosestTo(pargs.Source);

			if (damageDuration-- > 0)
				pargs.Weapon.Impact(Target.FromPos(target), new WarheadArgs(pargs));
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			zap = new TeslaZapRenderable(args.Source, 0, target - args.Source,
				info.Image, info.BrightSequence, info.BrightZaps, info.DimSequence, info.DimZaps, info.Palette);

			yield return zap;
		}
	}
}
