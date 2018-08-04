using System;
using System.Collections.Generic;

namespace OpenRA.Benchmark.Pathfinding
{
	//public struct GraphConnection
	//{
	//	public CPos Destination;
	//	public int Priority;

	//	public override bool Equals(Object obj)
	//	{
	//		return obj is GraphConnection && this == (GraphConnection)obj;
	//	}

	//	public override int GetHashCode()
	//	{
	//		return Destination.GetHashCode(); //^ Priority.GetHashCode();
	//	}

	//	public static bool operator ==(GraphConnection x, GraphConnection y)
	//	{
	//		return x.Destination == y.Destination;// && x.im == y.im;
	//	}

	//	public static bool operator !=(GraphConnection x, GraphConnection y)
	//	{
	//		return !(x == y);
	//	}
	//}

	public struct GraphConnection : IEquatable<GraphConnection>
	{
		public readonly CPos Destination;
		public readonly short Cost;

		public GraphConnection(CPos destination, short cost)
		{
			Destination = destination;
			Cost = cost;
		}

		public bool Equals(GraphConnection other)
		{
			return Destination.Equals(other.Destination) && Cost == other.Cost;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is GraphConnection && Equals((GraphConnection) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (Destination.GetHashCode() * 397) ^ Cost.GetHashCode();
			}
		}
	}

	//public static readonly CostComparer ConnectionCostComparer = CostComparer.Instance;

	public sealed class CostComparer : IComparer<GraphConnection>
	{
		public static readonly CostComparer Instance = new CostComparer();
		CostComparer() { }
		public int Compare(GraphConnection x, GraphConnection y)
		{
			return x.Cost.CompareTo(y.Cost);
		}
	}


	public struct GraphConnection3
	{
		public static readonly CostComparer ConnectionCostComparer = CostComparer.Instance;

		public sealed class CostComparer : IComparer<GraphConnection3>
		{
			public static readonly CostComparer Instance = new CostComparer();
			CostComparer() { }
			public int Compare(GraphConnection3 x, GraphConnection3 y)
			{
				return x.Cost.CompareTo(y.Cost);
			}
		}

		public readonly CPos2 Destination;
		public readonly int Cost;

		public GraphConnection3(CPos2 destination, int cost)
		{
			Destination = destination;
			Cost = cost;
		}
	}
}