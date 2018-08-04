using System;
using System.Diagnostics;

namespace OpenRA.Mods.Common.Pathfinder.PriorityQueue
{
	[DebuggerDisplay("Prio: {Priority} X: {X}")]
	public struct GraphConnection2 : IEquatable<GraphConnection2>
	{
		public readonly short Priority;
		//public readonly short X;
		//public readonly short Y;
		public readonly CPos Destination;


		public GraphConnection2(CPos destination, short priority)
		{
			Priority = priority;
			//X = x;
			//Y = y;
			Destination = destination;
		}


		public override bool Equals(Object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is GraphConnection2 && Equals((GraphConnection2)obj);
		}

		public bool Equals(GraphConnection2 other)
		{
			return Priority == other.Priority && Destination == other.Destination;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = Priority.GetHashCode();
				hashCode = (hashCode * 397) ^ Destination.GetHashCode();
				return hashCode;
			}
		}

		public static bool operator ==(GraphConnection2 x, GraphConnection2 y)
		{
			return x.Equals(y);// && x.im == y.im;
		}

		public static bool operator !=(GraphConnection2 x, GraphConnection2 y)
		{
			return !(x == y);
		}
	}

}