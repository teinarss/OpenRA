using System;
using System.Runtime.CompilerServices;

namespace OpenRA.Mods.Common.Pathfinder.PriorityQueue
{
	public class FastPriorityQueueStruct
	{
		private short _numNodes;
		private GraphConnection2[] _nodes;

		/// <summary>
		/// Instantiate a new Priority Queue
		/// </summary>
		/// <param name="maxNodes">The max nodes ever allowed to be enqueued (going over this will cause undefined behavior)</param>
		public FastPriorityQueueStruct(int maxNodes)
		{
			_numNodes = 0;
			_nodes = new GraphConnection2[maxNodes + 1];
		}

		/// <summary>
		/// Returns the number of nodes in the queue.
		/// O(1)
		/// </summary>
		public int Count
		{
			get
			{
				return _numNodes;
			}
		}

		/// <summary>
		/// Enqueue a node to the priority queue.  Lower values are placed in front. Ties are broken arbitrarily.
		/// If the queue is full, the result is undefined.
		/// If the node is already enqueued, the result is undefined.
		/// O(log n)
		/// </summary>

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Enqueue(GraphConnection2 node)
		{
			if (_numNodes >= _nodes.Length - 1)
			{
				Resize(_numNodes * 2);
			}

			_numNodes++;
			//node = new GraphConnection2(node.Priority, node.X, node.Y);
			_nodes[_numNodes] = node;// node;

			CascadeUp(node, _numNodes);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CascadeUp(GraphConnection2 node, short index)
		{
			//aka Heapify-up
			short parent;
			if (index > 1)
			{
				parent = (short)(index >> 1);
				GraphConnection2 parentNode = _nodes[parent];
				if (HasHigherOrEqualPriority(parentNode, node))
					return;

				//Node has lower priority value, so move parent down the heap to make room
				_nodes[index] = parentNode;// new GraphConnection2(parentNode.Priority, parentNode.X, parentNode.Y);
				_nodes[parent] = node;// = new GraphConnection2(node.Priority, node.X, node.Y);
				index = parent;
			}
			else
			{
				return;
			}

			while (parent > 1)
			{
				parent >>= 1;
				GraphConnection2 parentNode = _nodes[parent];
				if (HasHigherOrEqualPriority(parentNode, node))
					break;

				//Node has lower priority value, so move parent down the heap to make room
				_nodes[index] = parentNode;// new GraphConnection2(parentNode.Priority, parentNode.X, parentNode.Y); ;
				_nodes[parent] = node; // new GraphConnection2(node.Priority, node.X, node.Y);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CascadeDown(GraphConnection2 node, short finalQueueIndex)
		{
			//aka Heapify-down
			//short finalQueueIndex = i;
			short childLeftIndex = (short)(2 * finalQueueIndex);

			// If leaf node, we're done
			if (childLeftIndex > _numNodes)
			{
				return;
			}

			// Check if the left-child is higher-priority than the current node
			short childRightIndex = (short)(childLeftIndex + 1);
			GraphConnection2 childLeft = _nodes[childLeftIndex];
			if (HasHigherPriority(childLeft, node))
			{
				// Check if there is a right child. If not, swap and finish.
				if (childRightIndex > _numNodes)
				{
					_nodes[finalQueueIndex] = childLeft;
					_nodes[childLeftIndex] = node;
					return;
				}
				// Check if the left-child is higher-priority than the right-child
				GraphConnection2 childRight = _nodes[childRightIndex];
				if (HasHigherPriority(childLeft, childRight))
				{
					// left is highest, move it up and continue
					_nodes[finalQueueIndex] = childLeft;
					finalQueueIndex = childLeftIndex;
				}
				else
				{
					// right is even higher, move it up and continue
					_nodes[finalQueueIndex] = childRight;
					finalQueueIndex = childRightIndex;
				}
			}
			// Not swapping with left-child, does right-child exist?
			else if (childRightIndex > _numNodes)
			{
				return;
			}
			else
			{
				// Check if the right-child is higher-priority than the current node
				GraphConnection2 childRight = _nodes[childRightIndex];
				if (HasHigherPriority(childRight, node))
				{
					_nodes[finalQueueIndex] = childRight;
					finalQueueIndex = childRightIndex;
				}
				// Neither child is higher-priority than current, so finish and stop.
				else
				{
					return;
				}
			}

			while (true)
			{
				childLeftIndex = (short)(2 * finalQueueIndex);

				// If leaf node, we're done
				if (childLeftIndex > _numNodes)
				{
					_nodes[finalQueueIndex] = node;
					break;
				}

				// Check if the left-child is higher-priority than the current node
				childRightIndex = (short)(childLeftIndex + 1);
				childLeft = _nodes[childLeftIndex];
				if (HasHigherPriority(childLeft, node))
				{
					// Check if there is a right child. If not, swap and finish.
					if (childRightIndex > _numNodes)
					{
						_nodes[finalQueueIndex] = childLeft;
						_nodes[childLeftIndex] = node;
						break;
					}
					// Check if the left-child is higher-priority than the right-child
					GraphConnection2 childRight = _nodes[childRightIndex];
					if (HasHigherPriority(childLeft, childRight))
					{
						// left is highest, move it up and continue
						_nodes[finalQueueIndex] = childLeft;
						finalQueueIndex = childLeftIndex;
					}
					else
					{
						// right is even higher, move it up and continue
						_nodes[finalQueueIndex] = childRight;

						finalQueueIndex = childRightIndex;
					}
				}
				// Not swapping with left-child, does right-child exist?
				else if (childRightIndex > _numNodes)
				{
					_nodes[finalQueueIndex] = node;
					break;
				}
				else
				{
					// Check if the right-child is higher-priority than the current node
					GraphConnection2 childRight = _nodes[childRightIndex];
					if (HasHigherPriority(childRight, node))
					{
						_nodes[finalQueueIndex] = childRight;
						finalQueueIndex = childRightIndex;
					}
					// Neither child is higher-priority than current, so finish and stop.
					else
					{
						_nodes[finalQueueIndex] = node;
						break;
					}
				}
			}
		}

		/// <summary>
		/// Returns true if 'higher' has higher priority than 'lower', false otherwise.
		/// Note that calling HasHigherPriority(node, node) (ie. both arguments the same node) will return false
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool HasHigherPriority(GraphConnection2 higher, GraphConnection2 lower)
		{
			return (higher.Priority < lower.Priority);
		}

		/// <summary>
		/// Returns true if 'higher' has higher priority than 'lower', false otherwise.
		/// Note that calling HasHigherOrEqualPriority(node, node) (ie. both arguments the same node) will return true
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool HasHigherOrEqualPriority(GraphConnection2 higher, GraphConnection2 lower)
		{
			return (higher.Priority <= lower.Priority);
		}

		/// <summary>
		/// Removes the head of the queue and returns it.
		/// If queue is empty, result is undefined
		/// O(log n)
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GraphConnection2 Dequeue()
		{
			//#if DEBUG
			//			if (_numNodes <= 0)
			//			{
			//				throw new InvalidOperationException("Cannot call Dequeue() on an empty queue");
			//			}

			//			if (!IsValidQueue())
			//			{
			//				throw new InvalidOperationException("Queue has been corrupted (Did you update a node priority manually instead of calling UpdatePriority()?" +
			//													"Or add the same node to two different queues?)");
			//			}
			//#endif

			GraphConnection2 returnMe = _nodes[1];
			//If the node is already the last node, we can remove it immediately
			if (_numNodes == 1)
			{
				_numNodes = 0;
				return returnMe;
			}

			//Swap the node with the last node
			GraphConnection2 formerLastNode = _nodes[_numNodes];
			_nodes[1] = formerLastNode;// = new GraphConnection2(formerLastNode.Priority, formerLastNode.X, formerLastNode.Y);
			//formerLastNode.QueueIndex = 1;
			_numNodes--;

			//Now bubble formerLastNode (which is no longer the last node) down
			CascadeDown(formerLastNode, 1);
			return returnMe;
		}

		/// <summary>
		/// Resize the queue so it can accept more nodes.  All currently enqueued nodes are remain.
		/// Attempting to decrease the queue size to a size too small to hold the existing nodes results in undefined behavior
		/// O(n)
		/// </summary>
		public void Resize(int maxNodes)
		{
			//#if DEBUG
			//            if (maxNodes <= 0)
			//            {
			//                throw new InvalidOperationException("Queue size cannot be smaller than 1");
			//            }

			//            if (maxNodes < _numNodes)
			//            {
			//                throw new InvalidOperationException("Called Resize(" + maxNodes + "), but current queue contains " + _numNodes + " nodes");
			//            }
			//#endif

			GraphConnection2[] newArray = new GraphConnection2[maxNodes + 1];
			int highestIndexToCopy = Math.Min(maxNodes, _numNodes);
			Array.Copy(_nodes, newArray, highestIndexToCopy + 1);
			_nodes = newArray;
		}
	}
}