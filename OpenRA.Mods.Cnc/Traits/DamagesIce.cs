using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	class DamagesIceInfo : TraitInfo
	{
		public readonly int Interval = 8;

		public override object Create(ActorInitializer init)
		{
			return new DamagesIce(this);
		}
	}

	class DamagesIce : INotifyCreated, ITick
	{
		readonly DamagesIceInfo info;
		IceLayer iceLayer;
		[Sync]
		int ticks;

		public DamagesIce(DamagesIceInfo info)
		{
			this.info = info;
		}

		void ITick.Tick(Actor self)
		{
			if (--ticks <= 0)
			{
				foreach (var cell in self.OccupiesSpace.OccupiedCells())
				{
					iceLayer.Damage(cell.First, 200);
				}

				ticks = info.Interval;
			}
		}

		public void Created(Actor self)
		{
			iceLayer = self.World.WorldActor.TraitOrDefault<IceLayer>();
		}
	}
}
