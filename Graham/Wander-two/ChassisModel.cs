using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
	partial class Program
	{
		public class ChassisModel
		{
			// Plan:
			// * Create a box to represent the wheel locations and a box to represent the upper body location.
			// * Create sphere colliders (on the fly) at opposing wheels to determine the corner positions.
			// * Orient the box based on this, then stack the chassis on top.
			//
			//	1. Don't worry about the stacked box. Just use the wheel base.
			//	2. Once the wheel base can reliably traverse terrain, start worrying about the stacked box.

			List<>
		}
	}
}
