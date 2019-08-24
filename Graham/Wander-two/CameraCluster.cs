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
			/// <summary>
			/// Magic constant for how much scan distance is gained per tick. Derived from testing.
			/// </summary>
			const double CHARGE_PER_TICK = 1;

			Random randomGenerator_;
			List<IMyCameraBlock> cameraList_;
			MyDetectedEntityInfo lastScanHit_;
			double raycastConeLimit_;

			public CameraCluster()
			{
				cameraList_ = new List<IMyCameraBlock>();
				randomGenerator_ = new Random();
				raycastConeLimit_ = 45;     // 45 is the server default
				lastScanHit_ = default(MyDetectedEntityInfo);
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
			/// Scans a random point within a random camera's cone at a distance.
			/// </summary>
			/// <param name="distance">The distance to scan at.</param>
			/// <returns>Whether there was a hit</returns>
			public bool ScanRandom(double distance)
			{
				foreach (var camera in cameraList_)
				{
					if (camera.CanScan(distance))
					{
						float randomPitch = (float)(randomGenerator_.NextDouble() * raycastConeLimit_ * 2 - raycastConeLimit_);
						float randomYaw = (float)(randomGenerator_.NextDouble() * raycastConeLimit_ * 2 - raycastConeLimit_);

						lastScanHit_ = camera.Raycast(distance, randomPitch, randomYaw);

						return (!lastScanHit_.IsEmpty());
					}
				}
				return false;
			}

			public List<MyDetectedEntityInfo> ScanRandomAll(double distance)
			{
				var listToReturn = new List<MyDetectedEntityInfo>();

				foreach (var camera in cameraList_)
				{
					if (camera.CanScan(distance))
					{
						float randomPitch = (float)(randomGenerator_.NextDouble() * raycastConeLimit_ * 2 - raycastConeLimit_);
						float randomYaw = (float)(randomGenerator_.NextDouble() * raycastConeLimit_ * 2 - raycastConeLimit_);

						lastScanHit_ = camera.Raycast(distance, randomPitch, randomYaw);

						if (!lastScanHit_.IsEmpty())
						{
							listToReturn.Add(lastScanHit_);
						}
					}
				}

				return listToReturn;
			}

			public MyDetectedEntityInfo GetScanInfo()
			{
				return lastScanHit_;
			}

			/// <summary>
			/// Calculates how many ticks to wait before requesting a scan to ensure at least one camera is ready.
			/// </summary>
			public double GetDelay(double distance)
			{
				if (cameraList_.Count() > 0)
				{
					if (cameraList_.First().RaycastDistanceLimit == -1)
					{
						return 0;
					}
					else
					{
						return (distance / CHARGE_PER_TICK) / cameraList_.Count();
					}
				}
				else
				{
					return -1;
				}
			}

			/// <summary>
			/// The maximum positive angle the camera can apply to pitch and yaw for raycasting.
			/// </summary>
			public double RaycastConeLimit
			{
				get
				{
					return raycastConeLimit_;
				}
				set
				{
					if (value > 45)
					{
						raycastConeLimit_ = 45;
					}
					else if (value < 0)
					{
						raycastConeLimit_ = 0;
					}
					else
					{
						raycastConeLimit_ = value;
					}
				}
			}
		}
	}
}
