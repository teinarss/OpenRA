using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenRA.Benchmark.CellMomentCostIndex;
using OpenRA.Benchmark.Pathfinding;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Pathfinder.PriorityQueue;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Primitives;
using GraphConnection = OpenRA.Benchmark.Pathfinding.GraphConnection;

namespace OpenRA.Benchmark
{
	public class TestCode
	{
		public static int ApplyPercentageModifiers(int number, IEnumerable<int> percentages)
		{
			// See the comments of PR#6079 for a faster algorithm if this becomes a performance bottleneck
			var a = (float)number;
			foreach (var p in percentages)
				a *= p / 100f;

			return (int)a;
		}

		public static int ApplyPercentageModifiers2(int number, int[] percentages)
		{
			// See the comments of PR#6079 for a faster algorithm if this becomes a performance bottleneck
			var a = (decimal)number;
			var count = percentages.Length;
			for (int i = 0; i < count; i++)
			{
				a *= percentages[i] / 100m;
			}

			//foreach (var p in percentages)
			//	a *= p / 100f;

			return (int)a;
		}

		public static int ApplyPercentageModifiersUnsafe(int number, int[] percentages)
		{
			var a = (float)number;
			var lenght = percentages.Length;

			unsafe
			{
				fixed (int* p = percentages)
				{
					for (int i = 0; i < lenght; i++)
					{
						a *= *(p + i) / 100f;
					}

				}
			}

			return (int)a;
		}

		public static int ApplyPercentageModifiersUnsafe2(int number, int[] percentages)
		{
			var a = (float)number;
			var lenght = percentages.Length;

			unsafe
			{
				fixed (int* p = percentages)
				{
					for (int i = 0; i < lenght; i++)
					{
						a *= *(p + i);
					}

				}
			}

			return (int)(a / (lenght * 100));
		}

		public static int MultiplyNumberByPercentages(int number, IEnumerable<int> percentages)
		{
			var a = (long)number;
			var b = 1L;

			checked
			{
				foreach (var p in percentages)
				{
					// Catch integer overflow and discard some precision from
					// both the numerator and denominator
					while (true)
					{
						try
						{
							var temp = a * p;
							b *= 100;
							a = temp;

							break;
						}
						catch (OverflowException)
						{
							a /= 2;
							b /= 2;
						}
					}
				}
			}

			return (int)(a / b);
		}
	}
	public class ApplyPercentageModifiersBenchmark
	{
		readonly List<int> _percentages = new List<int>();


		[Setup]
		public void Setup()
		{
			Random rand = new Random();

			while (_percentages.Count < 300)
			{
				_percentages.Add(rand.Next(1, 100));
			}
		}

		[Benchmark(Description = "test1")]
		public void CurrentImplementation()
		{
			Util.ApplyPercentageModifiers(10, _percentages);
		}

		[Benchmark(Description = "test2")]
		public void NewCode()
		{
			TestCode.ApplyPercentageModifiers(10, _percentages);
		}


		//[Benchmark(Description = "test3")]
		//public void NewCode2()
		//{
		//	TestCode.MultiplyNumberByPercentages(10, _percentages);
		//}

		[Benchmark(Description = "test4")]
		public void NewCode4()
		{
			TestCode.ApplyPercentageModifiers2(10, _percentages.ToArray());
		}

		[Benchmark(Description = "test5")]
		public void NewCode5()
		{
			TestCode.ApplyPercentageModifiersUnsafe(10, _percentages.ToArray());
		}

		[Benchmark(Description = "test6")]
		public void NewCode6()
		{
			TestCode.ApplyPercentageModifiersUnsafe2(10, _percentages.ToArray());
		}
	}

	public class CellMovementCostCacheBenchmark
	{
		Cache1 cache1 = new Cache1();
		Cache2 cache2 = new Cache2();
		Cache3 cache3 = new Cache3();

		List<CPos> mapCellsToCheck = new List<CPos>();

		[Setup]
		public void Setup()
		{
			var locomotors = new List<string>
			{
				"FOOT",
				"WHEELED",
				"HEAVYWHEELED",
				"LIGHTTRACKED",
				"TRACKED",
				"HEAVYTRACKED",
				"NAVAL",
				"LANDINGCRAFT",
				"IMMOBILE"
			};

			List<LocomotorMap> list = new List<LocomotorMap>();


			var mapCells = new List<Pair<CPos, int>>();

			var count = 0;



			for (short x = 0; x < 256; x++)
			{
				for (short y = 0; y < 256; y++)
				{
					mapCells.Add(new Pair<CPos, int>(new CPos(x, y), 1));

					if (count < 500)
					{
						mapCellsToCheck.Add(new CPos(x, y));
						count++;
					}
				}
			}

			foreach (var locomotor in locomotors)
			{
				list.Add(new LocomotorMap
				{
					Name = locomotor,
					MapCells = mapCells
				});
			}

			cache1.WorldLoaded(list);
			cache2.WorldLoaded(list);
			cache3.WorldLoaded(list);
		}

		[Benchmark(Description = "test1")]
		public void Test1()
		{
			foreach (var cell in mapCellsToCheck)
			{
				cache1.Get("FOOT", cell);
			}
		}

		[Benchmark(Description = "test2")]
		public void Test2()
		{
			foreach (var cell in mapCellsToCheck)
			{
				cache2.Get("FOOT", cell);
			}
		}

		[Benchmark(Description = "test3")]
		public void Test3()
		{
			foreach (var cell in mapCellsToCheck)
			{
				cache3.Get("FOOT", cell);
			}
		}
	}

	class Program
	{


		static void Main(string[] args)
		{

			//var size = Marshal.SizeOf(typeof(GraphConnection2));
			BenchmarkRunner.Run<PathfindingBenchmark>();
			//BenchmarkRunner.Run<CellMovementCostCacheBenchmark>();

			//var cellMovementCostCacheBenchmark = new CellMovementCostCacheBenchmark();
			//cellMovementCostCacheBenchmark.Setup();
			//cellMovementCostCacheBenchmark.Test2();


			//var bench = new PathfindingBenchmark();
			//bench.Setup();
		}
	}
}
