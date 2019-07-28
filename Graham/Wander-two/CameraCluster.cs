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
		public class CameraCluster
		{
			Random random;

			List<IMyCameraBlock> cameraList_;

			public CameraCluster()
			{
				cameraList_ = new List<IMyCameraBlock>();
				random = new Random();
			}

			/// <summary>
			/// Adds a camera to the cluster.
			/// </summary>
			public void AddCamera(IMyCameraBlock theCamera)
			{
				if (!cameraList_.Contains(theCamera))
				{
					theCamera.EnableRaycast = true;
					cameraList_.Add(theCamera);
				}
			}

			/// <summary>
			/// Scans a random point within each camera's range at a distance.
			/// </summary>
			/// <param name="distance">The distance to scan at. If 0, scans at the highest range the camera can.</param>
			/// <returns>A list of encountered entities.</returns>
			public List<MyDetectedEntityInfo> ScanRandom(double distance = 0)
			{
				var detectedEntities = new List<MyDetectedEntityInfo>();

				foreach (var camera in cameraList_)
				{
					var individualCameraDistance = distance;
					if (distance <= 0)
					{
						individualCameraDistance = camera.RaycastDistanceLimit;
					}
					float randomPitch = (float)(random.NextDouble() * camera.RaycastConeLimit * 2 - camera.RaycastConeLimit);
					float randomYaw = (float)(random.NextDouble() * camera.RaycastConeLimit * 2 - camera.RaycastConeLimit);

					var hitData = camera.Raycast(individualCameraDistance, randomPitch, randomYaw);

					if (!hitData.IsEmpty())
					{
						detectedEntities.Add(hitData);
					}
				}

				return detectedEntities;
			}
		}
	}
}
