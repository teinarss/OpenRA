using OpenRA.GameRules;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits.Warheads
{
	public class DestroyIceWarhead : Warhead
	{
		[Desc("Size of the area. The resources are seeded within this area.", "Provide 2 values for a ring effect (outer/inner).")]
		public readonly int[] Size = { 0, 0 };

		// TODO: Allow maximum resource removal to be defined. (Per tile, and in total).
		public override void DoImpact(Target target, WarheadArgs args)
		{
			if (target.Type == TargetType.Invalid)
				return;

			var firedBy = args.SourceActor;
			var pos = target.CenterPosition;
			var world = firedBy.World;
			var dat = world.Map.DistanceAboveTerrain(pos);
			if (dat > AirThreshold)
				return;

			var targetTile = world.Map.CellContaining(pos);
			var resLayer = world.WorldActor.Trait<IceLayer>();

			var minRange = (Size.Length > 1 && Size[1] > 0) ? Size[1] : 0;
			var allCells = world.Map.FindTilesInAnnulus(targetTile, minRange, Size[0]);

			// Destroy all resources in the selected tiles
			foreach (var cell in allCells)
				resLayer.Destroy(cell);
		}
	}
}
