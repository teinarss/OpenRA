#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Pathfinder.PriorityQueue;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	class PriorityQueueTest
	{
		[TestCase(TestName = "PriorityQueue maintains invariants when adding and removing items.")]
		public void PriorityQueueGeneralTest()
		{
			var queue = new PriorityQueue<int>();

			Assert.IsTrue(queue.Empty, "New queue should start out empty.");
			Assert.Throws<InvalidOperationException>(() => queue.Peek(), "Peeking at an empty queue should throw.");
			Assert.Throws<InvalidOperationException>(() => queue.Pop(), "Popping an empty queue should throw.");

			foreach (var value in new[] { 4, 3, 5, 1, 2 })
			{
				queue.Add(value);
				Assert.IsFalse(queue.Empty, "Queue should not be empty - items have been added.");
			}

			foreach (var value in new[] { 1, 2, 3, 4, 5 })
			{
				Assert.AreEqual(value, queue.Peek(), "Peek returned the wrong item - should be in order.");
				Assert.IsFalse(queue.Empty, "Queue should not be empty yet.");
				Assert.AreEqual(value, queue.Pop(), "Pop returned the wrong item - should be in order.");
			}

			Assert.IsTrue(queue.Empty, "Queue should now be empty.");
			Assert.Throws<InvalidOperationException>(() => queue.Peek(), "Peeking at an empty queue should throw.");
			Assert.Throws<InvalidOperationException>(() => queue.Pop(), "Popping an empty queue should throw.");
		}
	}

	[TestFixture]
	class PriorityQueueTestStruct
	{
		[TestCase(TestName = "PriorityQueue maintains invariants when adding and removing items.")]
		public void PriorityQueueGeneralTest()
		{
			var queue = new FastPriorityQueueStruct(5);

			Assert.IsTrue(queue.Count == 0, "New queue should start out empty.");
			//Assert.Throws<InvalidOperationException>(() => queue.Peek(), "Peeking at an empty queue should throw.");
			//Assert.Throws<InvalidOperationException>(() => queue.Pop(), "Popping an empty queue should throw.");

			var values = new List<GraphConnection2>
			{
				new GraphConnection2(new CPos(4, 0 ), 4),
				new GraphConnection2(new CPos( 3, 0), 3),
				new GraphConnection2(new CPos( 5, 0), 5),
				new GraphConnection2(new CPos( 1, 0), 1),
				new GraphConnection2(new CPos( 2, 0), 2)

			};

			foreach (var value in values)
			{
				queue.Enqueue(value);
				//Assert.IsFalse(queue.Empty, "Queue should not be empty - items have been added.");
			}


			var sorted = values.OrderBy(v => v.Priority);


			foreach (var value in sorted)
			{
				var dequeue = queue.Dequeue();

				Assert.AreEqual(value.Priority, dequeue.Priority);
				//Assert.AreEqual(value, queue.Peek(), "Peek returned the wrong item - should be in order.");
				//Assert.IsFalse(queue.Empty, "Queue should not be empty yet.");
				//Assert.AreEqual(value, queue.Pop(), "Pop returned the wrong item - should be in order.");
			}

			//Assert.IsTrue(queue.Empty, "Queue should now be empty.");
			//Assert.Throws<InvalidOperationException>(() => queue.Peek(), "Peeking at an empty queue should throw.");
			//Assert.Throws<InvalidOperationException>(() => queue.Pop(), "Popping an empty queue should throw.");
		}
	}
}