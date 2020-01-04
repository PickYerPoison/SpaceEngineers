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
		CameraCluster cameras;
		int points;
		bool recording;
		int tick;

		MapManager mapManager_;
		MovementController movementController_;

		IMyShipController Controller;

		List<MovementPlanner.Point2D> finalPoints;
		List<Vector3D> hitLocations;

		Vector3D goal;

		public Program()
        {
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
			Me.CustomData = g.X.ToString() + "," + g.Y.ToString() + "," + g.Z.ToString() + "\n";

			var p = Controller.GetPosition();
			Me.CustomData += p.X.ToString() + "," + p.Y.ToString() + "," + p.Z.ToString() + "\n";

			mapManager_ = new MapManager();
			mapManager_.TerrainMap = new TerrainMap(Controller.GetPosition(), new Vector3D(500, 500, 500));
			mapManager_.UpDirection = -Controller.GetNaturalGravity();
			mapManager_.SetX_Direction(Controller.WorldMatrix.Left);
			mapManager_.SetY_Direction(Controller.WorldMatrix.Forward);
			mapManager_.GenerateMovementPlanner();

			var lwm = Controller.WorldMatrix.Left;
			Me.CustomData += lwm.X.ToString() + "," + lwm.Y.ToString() + "," + lwm.Z.ToString() + "\n";
			lwm = Controller.WorldMatrix.Forward;
			Me.CustomData += lwm.X.ToString() + "," + lwm.Y.ToString() + "," + lwm.Z.ToString();

			hitLocations = new List<Vector3D>();
			finalPoints = new List<MovementPlanner.Point2D>();

			movementController_ = new MovementController();
			movementController_.Controller = Controller;

			var wheels = new List<IMyMotorSuspension>();
			GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(wheels);
			foreach (var wheel in wheels)
			{
				movementController_.AddWheel(wheel);
			}

			tick = 0;

			goal = new Vector3D();
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
			// Per-tick updates
			if ((updateSource & UpdateType.Update1) != 0)
			{
				// Update 2D position for MovementController
				movementController_.Position2D = mapManager_.ProjectPoint(movementController_.Position3D);

				switch (tick)
				{
					case 0: // Generate movement nodes once per second
						// THIS ISN'T ACTUALLY THE FACING DIRECTION, FIX THIS!
						var planningNode = mapManager_.GenerateNode(Me.GetPosition(), 0, movementController_.DesiredSpeed, goal);

						for (int i = 0; i < 5; i++)
						{
							mapManager_.MovementPlanner.CreateChildren(planningNode);
						}
						break;
						
					default: // Scan points on non-dedicated ticks
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
						break;
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
