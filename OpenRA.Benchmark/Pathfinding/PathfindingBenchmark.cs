using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using OpenRA.Benchmark.PriorityQueue;
using OpenRA.Mods.Common.Pathfinder.PriorityQueue;

namespace OpenRA.Benchmark.Pathfinding
{
	public class PathfindingBenchmark
	{
		List<List<Container>> list;
		List<List<Container2>> list2;

		[Params(0, 1, 2, 3, 4)]
		public int N;

		Container Start;
		Container2 Start2;
		int Count;

		[Setup]
		public void Setup()
		{

			list = Data.Lists[N];
			list2 = Data2.Lists[N];

			Start = list.First().First();
			Start2 = list2.First().First();

			Count = list.Sum(l => l.Count);
		}

		//var random = new Random();

		//var listSize = N;//random.Next(0, 10);

		//for (int i = 0; i < listSize; i++)
		//{
		//	var positions = new List<Container>();

		//	var posSize = random.Next(0, 8);

		//	for (int y = 0; y < posSize; y++)
		//	{
		//		var cost = random.Next(0, 300);
		//		positions.Add(new Container(new CPos(64, 84), cost));
		//	}

		//}


		//Game.InitializeSettings(new Arguments("Game.Mod=ra"));
		//var searchPaths = new[] { ".\\mods" };
		//var explicitPaths = new string[]{};
		//var mods = new InstalledMods(searchPaths, explicitPaths);
		//var modData = new ModData(mods["cnc"], mods, true);

		//var map = modData.PrepareMap("");

		//var orderManager = new OrderManager("<no server>", -1, "", new EchoConnection());
		//world = new World(modData, map, orderManager, WorldType.Regular);

		//world.CreateActor("", new TypeDictionary
		//	{
		//		new LocationInit(location.Value),
		//		new OwnerInit("test"),

		//});

		[Benchmark(Description = "PQ current implementation", Baseline = true)]
		public void CurrentPriorityQueue()
		{
			var priorityQueue = new OpenRA.Primitives.PriorityQueue<GraphConnection3>(GraphConnection3.ConnectionCostComparer);

			priorityQueue.Add(new GraphConnection3(Start2.Cpos, Start2.Priority));

			foreach (var value in list2.Skip(1))
			{
				priorityQueue.Pop();
				foreach (var item in value)
				{
					priorityQueue.Add(new GraphConnection3(item.Cpos, item.Priority));
				}
			}
		}


		[Benchmark(Description = "PQ current implementation 64bits struct")]
		public void CurrentPriorityQueue64bits()
		{
			var priorityQueue = new OpenRA.Primitives.PriorityQueue<GraphConnection>(CostComparer.Instance);

			priorityQueue.Add(new GraphConnection(Start.Cpos, (short) Start.Priority));

			foreach (var value in list.Skip(1))
			{
				priorityQueue.Pop();
				foreach (var item in value)
				{
					priorityQueue.Add(new GraphConnection(item.Cpos, (short) item.Priority));
				}
			}
		}

		[Benchmark(Description = "BlueRaja Class")]
		public void FastPriorityQueue()
		{
			var priorityQueue = new OpenRA.Mods.Common.Pathfinder.PriorityQueue.FastPriorityQueue<Mods.Common.Pathfinder.GraphConnection>(Count);

			priorityQueue.Enqueue(new Mods.Common.Pathfinder.GraphConnection(Start.Cpos, Start.Priority));

			foreach (var value in list)
			{
				priorityQueue.Dequeue();
				foreach (var item in value)
				{
					priorityQueue.Enqueue(new Mods.Common.Pathfinder.GraphConnection(item.Cpos, item.Priority));
				}
			}
		}

		[Benchmark(Description = "hot")]
		public void HotPQ()
		{
			var priorityQueue = new HotPriorityQueue<CPos>((short) Count);
			priorityQueue.Enqueue((short) Start.Priority, Start.Cpos);

			foreach (var value in list)
			{
				HexKeyValuePair<short, CPos> value1;
				priorityQueue.TryDequeue(out value1);

				foreach (var item in value)
				{
					priorityQueue.Enqueue((short) item.Priority, item.Cpos);
				}
			}
		}


		[Benchmark(Description = "BlueRaja struct 64 bits")]
		public void Struct()
		{
			var priorityQueue = new FastPriorityQueueStruct(Count);

			priorityQueue.Enqueue(new GraphConnection2(Start.Cpos, (short) Start.Priority));

			foreach (var value in list)
			{
				priorityQueue.Dequeue();

				foreach (var item in value)
				{
					priorityQueue.Enqueue(new GraphConnection2(item.Cpos, (short) item.Priority));
				}
			}
		}
	}
}