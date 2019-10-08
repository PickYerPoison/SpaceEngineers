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
		int points;
		bool recording;

		MapManager mapManager_;

		IMyShipController Controller;

		List<MovementPlanner.Point2D> finalPoints;
		List<Vector3D> hitLocations;

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

			points = 0;

			recording = false;

			var x = new List<IMyShipController>();
			GridTerminalSystem.GetBlocksOfType<IMyShipController>(x);
			Controller = x.First();
			var g = Controller.GetNaturalGravity();
			Me.CustomData = g.X.ToString() + "," + g.Y.ToString() + "," + g.Z.ToString();

			mapManager_ = new MapManager();
			mapManager_.TerrainMap = new TerrainMap(Controller.GetPosition(), new Vector3D(500, 500, 500));
			mapManager_.UpDirection = -Controller.GetNaturalGravity();
			mapManager_.SetX_Direction(Controller.WorldMatrix.Left);
			mapManager_.SetY_Direction(Controller.WorldMatrix.Forward);
			mapManager_.GenerateMovementPlanner();

			var lwm = Controller.WorldMatrix.Left;
			Me.CustomData += lwm.X.ToString() + "," + lwm.Y.ToString() + "," + lwm.Z.ToString();
			lwm = Controller.WorldMatrix.Forward;
			Me.CustomData += lwm.X.ToString() + "," + lwm.Y.ToString() + "," + lwm.Z.ToString();

			hitLocations = new List<Vector3D>();
			finalPoints = new List<MovementPlanner.Point2D>();
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
				if (recording)
				{
					mapManager_.UpDirection = -Controller.GetNaturalGravity();

					var newPoints = cameras.ScanRandomAll(50);	

					foreach (var point in newPoints)
					{
						if (!point.IsEmpty())
						{
							hitLocations.Add((Vector3D)point.HitPosition);
							points++;
							//mapManager_.AddPoint((Vector3D)point.HitPosition, 500);
						}
					}

					Echo(points.ToString());
					Echo(hitLocations.Count().ToString());
					Me.CustomData = points.ToString();
				}
			}
			else
			{
				// Take terminal arguments
				if (argument == "dump")
				{
					string dumpText = "";

					/*for (int i = 0; i < 1000 && i < finalPoints.Count(); ++i)
					{
						var point = finalPoints.First();
						dumpText += "a(" + Math.Round(point.Position.X, 2).ToString() + "," + Math.Round(point.Position.Y, 2).ToString() + ",";
						if (point.Dangerous)
						{
							dumpText += "1";
						}
						else
						{
							dumpText += "0";
						}
						dumpText += ");\n";
						finalPoints.RemoveAt(0);
					}*/

					Me.CustomData = "";
					int couldPaste = 0;
					bool keepGoing = hitLocations.Count() > 0;
					while (keepGoing)
					{
						var point = hitLocations.First();
						dumpText += "a(" + Math.Round(point.X, 2).ToString() + "," + Math.Round(point.Y, 2).ToString() + "," + Math.Round(point.Z, 2).ToString() + ");";
						var oldData = Me.CustomData;
						Me.CustomData = dumpText;
						if (Me.CustomData.Length != dumpText.Length)
						{
							Me.CustomData = oldData;
							keepGoing = false;
						}
						else
						{
							couldPaste++;
							hitLocations.RemoveAt(0);

							keepGoing = hitLocations.Count() > 0;
						}
					}
					Me.CustomData += "\n";

					Echo(couldPaste.ToString());
					Echo(hitLocations.Count().ToString());
				}
				else if (argument == "reset")
				{
					mapManager_ = new MapManager();
					mapManager_.TerrainMap = new TerrainMap(Me.GetPosition(), new Vector3D(1000, 1000, 1000));
					mapManager_.UpDirection = -Controller.GetNaturalGravity();
					mapManager_.SetX_Direction(Controller.WorldMatrix.Left);
					mapManager_.SetY_Direction(Controller.WorldMatrix.Forward);
					mapManager_.GenerateMovementPlanner();
					points = 0;
				}
				else if (argument == "stop")
				{
					recording = false;
					//finalPoints = mapManager_.MovementPlanner.GetPoints();
				}
				else if (argument == "start")
				{
					recording = true;
				}
			}
		}
    }
}
