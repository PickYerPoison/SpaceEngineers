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
    public partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

		CameraCluster cameras;
		List<Vector3D> points;
		bool recording;

		TerrainMap terrainMap;
		MovementPlanner movementPlanner;

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.

            Runtime.UpdateFrequency = UpdateFrequency.Update1;

			cameras = new CameraCluster();

			var cameraList = new List<IMyCameraBlock>();
			GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>(cameraList);
			
			foreach (var camera in cameraList)
			{
				cameras.AddCamera(camera);
			}

			points = new List<Vector3D>();

			recording = false;
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

		public void Main(string argument, UpdateType updateSource)
		{
			// Update terrain map and movement planner
			if ((updateSource & UpdateType.Update1) != 0)
			{

			}
			else
			{
				// Take terminal arguments
				if (argument == "dump")
				{
					string dumpText = "";
					for (int i = 0; i < 1000; i++)
					{
						var point = points.First();
						dumpText += "a(" + Math.Round(point.X, 2).ToString() + "," + Math.Round(point.Y, 2).ToString() + "," + Math.Round(point.Z, 2).ToString() + ");\n";
						points.RemoveAt(0);
					}
					Me.CustomData = dumpText;
				}
				else if (argument == "reset")
				{
					points.Clear();
				}
				else if (argument == "stop")
				{
					recording = false;
				}
				else if (argument == "start")
				{
					recording = true;
				}

				if (recording)
				{
					var newPoints = cameras.ScanRandomAll(30);

					foreach (var point in newPoints)
					{
						points.Add((Vector3D)point.HitPosition);
					}

					Echo(points.Count().ToString());
				}
			}
		}
    }
}
