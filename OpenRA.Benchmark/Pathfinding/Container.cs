namespace OpenRA.Benchmark.Pathfinding
{
	public class Container
	{
		public CPos Cpos { get; private set; }
		public int Priority { get; private set; }

		public Container(CPos cpos, int priority)
		{
			Cpos = cpos;
			Priority = priority;
		}

	}
}