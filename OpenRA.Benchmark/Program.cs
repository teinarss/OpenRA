using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenRA.Support;

namespace OpenRA.Benchmark
{
	class Program
	{
		public static void Main(string[] args) => BenchmarkRunner.Run<MersenneTwisterTest>();
	}

	public class MersenneTwisterTest
	{
		MersenneTwister twister = new MersenneTwister();

		// And define a method with the Benchmark attribute
		[Benchmark]
		[ArgumentsSource(nameof(Data))]
		public int Throw(int d)
		{
			return twister.Next(0, d);
		}

		[Benchmark]
		[ArgumentsSource(nameof(Data))]
		public int StaticThrow(int d)
		{
			return twister.Next2(0, d);
		}

		public int[] Data()
		{
			return new []{1, 3, 200, 450, 1023 };
		}
	}

	public class WangleTest
	{
		// And define a method with the Benchmark attribute
		[Benchmark]
		[ArgumentsSource(nameof(Data))]
		public WAngle WangleThrow(int d)
		{
			return WAngle.ArcSin(d);
		}

		[Benchmark]
		[ArgumentsSource(nameof(Data))]
		public WAngle WangleStaticThrow(int d)
		{
			return WAngle.ArcSin2(d);
		}

		public int[] Data()
		{
			return new[] { -1023, -450, 0, 450, 1023 };
		}
	}
}
