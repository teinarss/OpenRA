#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public class CastInt
	{
		[TestCase(TestName = "Packing x,y and layer into int")]
		public void PackUnpackBits()
		{
			var value1 = 2 / 10;

			var value2 = 9 / 10;

			var value3 = 10 / 10;
			var value4 = 18 / 10;


			Assert.AreEqual(0, value1);
			Assert.AreEqual(0, value2);

			Assert.AreEqual(1, value3);
			Assert.AreEqual(1, value4);



		}
	}
}
