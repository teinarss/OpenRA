#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{

	[Desc("This actor can collect crates.")]
	public class MineDetonatorInfo : ITraitInfo
	{
		[Desc("Define collector type(s) checked by Crate and CrateAction for validity. Leave empty if actor is supposed to be able to collect any crate.")]
		public readonly BitSet<MineDetonatorType> DetonatorTypes = default(BitSet<MineDetonatorType>);
		public bool All { get { return DetonatorTypes == default(BitSet<MineDetonatorType>); } }

		public object Create(ActorInitializer init) { return new MineDetonator(this); }
	}
	public class MineDetonator
	{
		public readonly MineDetonatorInfo Info;
		public MineDetonator(MineDetonatorInfo info)
		{
			Info = info;
		}
	}

	public class MineDetonatorType
	{

	}

	class MineInfo : ITraitInfo
	{
		//public readonly BitSet<CrushClass> CrushClasses = default(BitSet<CrushClass>);
		public readonly bool AvoidFriendly = true;
		public readonly bool BlockFriendly = true;
		public readonly BitSet<CrushClass> DetonateClasses = default(BitSet<CrushClass>);

		[Desc("Define actors that can collect crates by setting one of these into the Collects field from the CrateCollector trait.")]
		public readonly BitSet<MineDetonatorType> ValidCollectorTypes = new BitSet<MineDetonatorType>("crate-collector");

		public object Create(ActorInitializer init) { return new Mine(init, this); }
	}

	class Mine : ITick, INotifyAddedToWorld, INotifyRemovedFromWorld
	{
		readonly MineInfo info;
		bool detonated;
		readonly Actor self;

		[Sync] int ticks;
		[Sync] public CPos Location;

		public Mine(ActorInitializer init, MineInfo info)
		{
			self = init.Self;
			this.info = info;
		}

		void CheckForCollectors(Actor self)
		{
			// Check whether any other (ground) actors are in this cell.
			var sameCellActors = self.World.ActorMap.GetActorsAt(self.Location).Where(a => a != self);

			// HACK: Currently needed to find aircraft actors.
			// TODO: Remove this once GetActorsAt supports aircraft.
			sameCellActors.Concat(self.World.FindActorsInCircle(self.CenterPosition, new WDist(724))
				.Where(a => a != self && a.Location == self.Location && !sameCellActors.Contains(a)));

			if (!sameCellActors.Any())
				return;

			var collector = sameCellActors.FirstOrDefault(a =>
			{
				if (!a.IsAtGroundLevel())
					return false;

				var crateCollectorTraitInfo = a.Info.TraitInfoOrDefault<MineDetonatorInfo>();
				if (crateCollectorTraitInfo == null)
					return false;

				// Make sure that the actor can collect this crate type
				return crateCollectorTraitInfo.All || crateCollectorTraitInfo.DetonatorTypes.Overlaps(info.ValidCollectorTypes);
			});

			if (collector != null)
				OnCollectInner(collector, self);
		}

		void OnCollectInner(Actor detonator, Actor mine)
		{
			if (detonated)
				return;

			if (detonator.Info.HasTraitInfo<MineImmuneInfo>() || (self.Owner.Stances[mine.Owner] == Stance.Ally && info.AvoidFriendly))
				return;

			self.Kill(detonator);

			detonated = true;

			mine.Dispose();
		}

		void ITick.Tick(Actor self)
		{
			CheckForCollectors(self);
		}

		public void AddedToWorld(Actor self)
		{
			var mineLocation = self.World.WorldActor.TraitOrDefault<MineLocations>();
			if (mineLocation != null)
				mineLocation.Add(self);
		}

		public void RemovedFromWorld(Actor self)
		{
			var mineLocation = self.World.WorldActor.TraitOrDefault<MineLocations>();
			if (mineLocation != null)
				mineLocation.Remove(self);
		}
	}

	[Desc("Tag trait for stuff that should not trigger mines.")]
	class MineImmuneInfo : TraitInfo<MineImmune> { }
	class MineImmune { }

	class MineLocationsInfo : ITraitInfo
	{
		public object Create(ActorInitializer init)
		{
			return new MineLocations();
		}
	}

	class MineLocations : IWorldLoaded
	{
		CellLayer<bool> _locations;

		public void Add(Actor self)
		{
			_locations[self.Location] = true;
		}

		public void Remove(Actor self)
		{
			_locations[self.Location] = false;
		}

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			_locations = new CellLayer<bool>(w.Map);
		}

		public bool Occupied(CPos cPos)
		{
			return _locations[cPos];
		}
	}
}
