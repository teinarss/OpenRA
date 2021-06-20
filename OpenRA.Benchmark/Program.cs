using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenRA.Primitives;

namespace OpenRA.Benchmark
{
    class Program
    {
	    public static void Main(string[] args) => BenchmarkRunner.Run<ActionQueueBenchmark>();
    }

    public class ActionQueueBenchmark
    {

	    ActionQueue actionQueue = new ActionQueue();
	    ActionQueue1 actionQueue1 = new ActionQueue1();


[Benchmark]
	    public void Foo1() => actionQueue.PerformActions(1);

	    [Benchmark]
	    public void Foo2() => actionQueue1.PerformActions(1);

}
}
