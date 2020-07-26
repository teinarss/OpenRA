using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	[Desc("Attach this to the world layer for regrowable ice terrain.")]
	class IceLayerInfo : TraitInfo
	{
		[Desc("Tileset IDs where the trait is activated.")]
		public readonly string[] Tilesets = { "SNOW" };

		public readonly string ImpassableTerrainType = "Water";

		public readonly string MaxStrengthTerrainType = "Ice";

		public readonly string HalfStrengthTerrainType = "Cracked";

		public int MaxStrength = 1024;

		[Desc("Measured in game ticks")]
		public int GrowthRate = 10;

		[Desc("Palette to render the layer sprites in.")]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		public override object Create(ActorInitializer init) { return new IceLayer(init.Self, this); }
	}

	class IceLayer : ITick, IWorldLoaded, IRenderOverlay, ITickRender
	{
		readonly IceLayerInfo info;
		readonly Dictionary<CPos, Sprite> dirty = new Dictionary<CPos, Sprite>();
		readonly Queue<CPos> dirtyToRemove = new Queue<CPos>();
		readonly Dictionary<string, int[]> tiles;

		public readonly CellLayer<int> Strength;

		readonly Dictionary<ushort, int> strengthPerTile;
		readonly List<CPos> iceCells = new List<CPos>();

		int growthTicks;
		bool initialIceLoaded;
		Theater theater;
		World world;

		TerrainSpriteLayer terrainSpriteLayer;

		[Flags]
		public enum ClearSides : byte
		{
			None = 0x0,
			Left = 0x1,
			Top = 0x2,
			Right = 0x4,
			Bottom = 0x8,

			TopLeft = 0x10,
			TopRight = 0x20,
			BottomLeft = 0x40,
			BottomRight = 0x80,

			All = 0xFF
		}

		public static readonly Dictionary<ClearSides, int[]> SpriteMap = new Dictionary<ClearSides, int[]>()
		{
			{ ClearSides.None, new[] { 0, 1, 31 } },
			{ ClearSides.Left | ClearSides.Top | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Top | ClearSides.Right | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Left | ClearSides.Bottom | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Right | ClearSides.Bottom | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Left | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight,  new[] { 0 } },
			{ ClearSides.Right | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Top | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Bottom | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Left | ClearSides.Top | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft, new[] { 0 } },
			{ ClearSides.Top | ClearSides.Right | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Left | ClearSides.Bottom | ClearSides.TopLeft | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 49 } },
			{ ClearSides.Right | ClearSides.Bottom | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Left | ClearSides.Top | ClearSides.Right | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Left | ClearSides.Right | ClearSides.Bottom | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Left | ClearSides.Top | ClearSides.Bottom | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Top | ClearSides.Right | ClearSides.Bottom | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Top | ClearSides.TopLeft | ClearSides.TopRight, new[] { 20 } },
			{ ClearSides.Right | ClearSides.TopRight | ClearSides.BottomRight, new[] { 24 } },
			{ ClearSides.Left | ClearSides.TopLeft | ClearSides.BottomLeft, new[] { 18 } },
			{ ClearSides.Bottom | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 17 } },
			{ ClearSides.TopLeft, new[] { 0 } },
			{ ClearSides.TopRight, new[] { 0 } },
			{ ClearSides.BottomLeft, new[] { 0 } },
			{ ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Left | ClearSides.TopLeft | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Right | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Top | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Top | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft, new[] { 0 } },
			{ ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.TopLeft | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft, new[] { 0 } },
			{ ClearSides.TopRight | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.TopLeft | ClearSides.TopRight, new[] { 0 } },
			{ ClearSides.TopRight | ClearSides.BottomLeft, new[] { 0 } },
			{ ClearSides.TopLeft | ClearSides.BottomLeft, new[] { 0 } },
			{ ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.TopLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Left | ClearSides.Right | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Top | ClearSides.Bottom | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.All, new[] { 0 } },
			{ ClearSides.Left | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomLeft, new[] { 0 } },
			{ ClearSides.Right | ClearSides.TopLeft | ClearSides.TopRight | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Bottom | ClearSides.TopRight | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
			{ ClearSides.Bottom | ClearSides.TopLeft | ClearSides.BottomLeft | ClearSides.BottomRight, new[] { 0 } },
		};

		public IceLayer(Actor self, IceLayerInfo info)
		{
			this.info = info;

			if (!info.Tilesets.Contains(self.World.Map.Tileset))
				return;

			tiles = new Dictionary<string, int[]>
			{
				{
					"ice1", new[]
					{
						439,
						440, 441, 442, 443, 444, 445,  446, 447, // 8
						448, 449, 450, 451, 452, 453,  454, 455,
						456, 457, 458, 459, 460, 461,  462, 463,
						464, 465, 466, 467, 468, 469,  470, 471,
						472, 473, 474, 475, 476, 477,  478, 479,
						480, 481, 482, 483, 484, 485,  486,
						487, 488, 489, 490, 491, 492,  493, 494,
						495, 496, 497, 498, 499, 500,  501,
						502
					}
				},
				{
					"ice2", new[]
					{
						503,
						504, 505, 506, 507, 508, 509, 510, 511, // 8
						512, 513, 514, 515, 516, 517, 518, 519,
						520, 521, 522, 523, 524, 525, 526, 527,
						528, 529, 530, 531, 532, 533, 534, 535,
						536, 537, 538, 539, 540, 541, 542, 543,
						544, 545, 546, 547, 548, 549, 550,
						551, 552, 553, 554, 555, 556, 557, 558,
						559, 560, 561, 562, 563, 564, 565,
						566
					}
				},
				{
					"ice3", new[]
					{
						567,
						568, 569, 570, 571, 572, 573, 574, 575,
						576, 577, 578, 579, 580, 581, 582, 583,
						584, 585, 586, 587, 588, 589, 590, 591,
						592, 593, 594, 595, 596, 597, 598, 599,
						600, 601, 602, 603, 604, 605, 606, 607,
						608, 609, 610, 611, 612, 613, 614,
						615, 616, 617, 618, 619, 620, 621, 622,
						623, 624, 625, 626, 627, 628, 629,
						630
					}
				}
			};

			strengthPerTile = new Dictionary<ushort, int>
			{
				// Ice 01
				{ 439, 1 },
				{ 440, 2 }, { 441, 2 }, { 442, 2 }, { 443, 2 }, { 444, 2 }, { 445, 2 }, { 446, 2 }, { 447, 2 },
				{ 448, 2 }, { 449, 2 }, { 450, 2 }, { 451, 2 }, { 452, 2 }, { 453, 2 }, { 454, 2 }, { 455, 2 },
				{ 456, 4 }, { 457, 4 }, { 458, 4 }, { 459, 4 }, { 460, 4 }, { 461, 4 }, { 462, 4 }, { 463, 4 },
				{ 464, 4 }, { 465, 4 }, { 466, 4 }, { 467, 4 }, { 468, 4 }, { 469, 4 }, { 470, 4 }, { 471, 4 },
				{ 472, 4 }, { 473, 4 }, { 474, 4 }, { 475, 4 }, { 476, 4 }, { 477, 4 }, { 478, 4 }, { 479, 4 },
				{ 480, 4 }, { 481, 4 }, { 482, 4 }, { 483, 4 }, { 484, 4 }, { 485, 4 }, { 486, 4 },
				{ 487, 8 }, { 488, 8 }, { 489, 8 }, { 490, 8 }, { 491, 8 }, { 492, 8 }, { 493, 8 }, { 494, 8 },
				{ 495, 8 }, { 496, 8 }, { 497, 8 }, { 498, 8 }, { 499, 8 }, { 500, 8 }, { 501, 8 },
				{ 502, 16 },

				// Ice 02
				{ 503, 1 },
				{ 504, 2 }, { 505, 2 }, { 506, 2 }, { 507, 2 }, { 508, 2 }, { 509, 2 }, { 510, 2 }, { 511, 2 },
				{ 512, 2 }, { 513, 2 }, { 514, 2 }, { 515, 2 }, { 516, 2 }, { 517, 2 }, { 518, 2 }, { 519, 2 },
				{ 520, 4 }, { 521, 4 }, { 522, 4 }, { 523, 4 }, { 524, 4 }, { 525, 4 }, { 526, 4 }, { 527, 4 },
				{ 528, 4 }, { 529, 4 }, { 530, 4 }, { 531, 4 }, { 532, 4 }, { 533, 4 }, { 534, 4 }, { 535, 4 },
				{ 536, 4 }, { 537, 4 }, { 538, 4 }, { 539, 4 }, { 540, 4 }, { 541, 4 }, { 542, 4 }, { 543, 4 },
				{ 544, 4 }, { 545, 4 }, { 546, 4 }, { 547, 4 }, { 548, 4 }, { 549, 4 }, { 550, 4 },
				{ 551, 8 }, { 552, 8 }, { 553, 8 }, { 554, 8 }, { 555, 8 }, { 556, 8 }, { 557, 8 }, { 558, 8 },
				{ 559, 8 }, { 560, 8 }, { 561, 8 }, { 562, 8 }, { 563, 8 }, { 564, 8 }, { 565, 8 },
				{ 566, 16 },

				// Ice 03
				{ 567, 1 },
				{ 568, 2 }, { 569, 2 }, { 570, 2 }, { 571, 2 }, { 572, 2 }, { 573, 2 }, { 574, 2 }, { 575, 2 },
				{ 576, 2 }, { 577, 2 }, { 578, 2 }, { 579, 2 }, { 580, 2 }, { 581, 2 }, { 582, 2 }, { 583, 2 },
				{ 584, 4 }, { 585, 4 }, { 586, 4 }, { 587, 4 }, { 588, 4 }, { 589, 4 }, { 590, 4 }, { 591, 4 },
				{ 592, 4 }, { 593, 4 }, { 594, 4 }, { 595, 4 }, { 596, 4 }, { 597, 4 }, { 598, 4 }, { 599, 4 },
				{ 600, 4 }, { 601, 4 }, { 602, 4 }, { 603, 4 }, { 604, 4 }, { 605, 4 }, { 606, 4 }, { 607, 4 },
				{ 608, 4 }, { 609, 4 }, { 610, 4 }, { 611, 4 }, { 612, 4 }, { 613, 4 }, { 614, 4 },
				{ 615, 8 }, { 616, 8 }, { 617, 8 }, { 618, 8 }, { 619, 8 }, { 620, 8 }, { 621, 8 }, { 622, 8 },
				{ 623, 8 }, { 624, 8 }, { 625, 8 }, { 626, 8 }, { 627, 8 }, { 628, 8 }, { 629, 8 },
				{ 630, 16 }
			};

			growthTicks = info.GrowthRate;

			Strength = new CellLayer<int>(self.World.Map);
		}

		bool CellContains(CPos c)
		{
			return Strength[c] > 0;
		}

		bool Oouo(CPos c)
		{
			var tile = world.Map.Tiles[c];
			return !CellContains(c) && (tile.Type >= 333 && tile.Type <= 346);
		}

		ClearSides FindClearSides(CPos p)
		{
			var ret = ClearSides.None;

			if (Oouo(p + new CVec(0, -1)))
				ret |= ClearSides.Top | ClearSides.TopLeft | ClearSides.TopRight;

			if (Oouo(p + new CVec(-1, 0)))
				ret |= ClearSides.Left | ClearSides.TopLeft | ClearSides.BottomLeft;

			if (Oouo(p + new CVec(1, 0)))
				ret |= ClearSides.Right | ClearSides.TopRight | ClearSides.BottomRight;

			if (Oouo(p + new CVec(0, 1)))
				ret |= ClearSides.Bottom | ClearSides.BottomLeft | ClearSides.BottomRight;

			if (Oouo(p + new CVec(-1, -1)))
				ret |= ClearSides.TopLeft;

			if (Oouo(p + new CVec(1, -1)))
				ret |= ClearSides.TopRight;

			if (Oouo(p + new CVec(-1, 1)))
				ret |= ClearSides.BottomLeft;

			if (Oouo(p + new CVec(1, 1)))
				ret |= ClearSides.BottomRight;

			return ret;
		}

		void UpdateCell(CPos cell)
		{
			var strength = Strength[cell];

			var clearSide = FindClearSides(cell);

			var i = SpriteMap[clearSide];
			var index = i[0];
			if (i.Length > 1)
			{
				if (strength >= info.MaxStrength)
				{
					index = i[0];
					world.Map.CustomTerrain[cell] = world.Map.Rules.TileSet.GetTerrainIndex(info.MaxStrengthTerrainType);
				}
				else if (strength >= info.MaxStrength / 2)
				{
					index = i[1];
					world.Map.CustomTerrain[cell] = world.Map.Rules.TileSet.GetTerrainIndex(info.HalfStrengthTerrainType);
				}
				else if (strength <= info.MaxStrength / 16)
				{
					index = i[2];
					world.Map.CustomTerrain[cell] = world.Map.Rules.TileSet.GetTerrainIndex(info.ImpassableTerrainType);
				}
			}

			var t = ChooseRandomVariant();
			var template = (ushort)t[index];

			var s = theater.TileSprite(new TerrainTile(template, 0));
			dirty[cell] = s;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			if (!info.Tilesets.Contains(w.Map.Tileset))
				return;

			theater = wr.Theater;
			terrainSpriteLayer = new TerrainSpriteLayer(w, wr, theater.Sheet, BlendMode.Alpha, wr.Palette(info.Palette), wr.World.Type != WorldType.Editor);
			world = w;
			foreach (var cell in w.Map.AllCells)
			{
				var tile = w.Map.Tiles[cell];
				var template = w.Map.Rules.TileSet.Templates[tile.Type];
				if (strengthPerTile.ContainsKey(template.Id))
				{
					iceCells.Add(cell);
					var factor = strengthPerTile[template.Id];
					var strength = Game.CosmeticRandom.Next(info.MaxStrength / factor / 2, info.MaxStrength / factor);
					Strength[cell] = strength;
					UpdateCell(cell);
				}
			}

			initialIceLoaded = true;
		}

		void ITick.Tick(Actor self)
		{
			if (!info.Tilesets.Contains(self.World.Map.Tileset))
				return;

			if (!initialIceLoaded)
				return;

			if (--growthTicks <= 0)
			{
				foreach (var cell in iceCells)
				{
					var strength = Strength[cell];
					if (strength >= info.MaxStrength)
						continue;

					Strength[cell] = strength + 1;
					UpdateCell(cell);
				}

				growthTicks = info.GrowthRate;
			}
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			if (terrainSpriteLayer != null)
				terrainSpriteLayer.Draw(wr.Viewport);
		}

		int[] ChooseRandomVariant()
		{
			var key = tiles.Keys.Random(Game.CosmeticRandom);

			return tiles[key];
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			foreach (var kv in dirty)
			{
				if (!self.World.FogObscures(kv.Key))
				{
					terrainSpriteLayer.Update(kv.Key, kv.Value);
					dirtyToRemove.Enqueue(kv.Key);
				}
			}

			while (dirtyToRemove.Count > 0)
				dirty.Remove(dirtyToRemove.Dequeue());
		}

		public void Destroy(CPos cell)
		{
			var str = Strength[cell];
			str -= 512;
			Strength[cell] = Math.Max(0, str);
			UpdateCell(cell);
		}
	}
}
