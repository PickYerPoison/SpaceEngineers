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
		public class MapManager
		{
			TerrainMap terrainMap_;
			MovementPlanner movementPlanner_;
			int numPoints_;
			Vector3D addFromPoint;

			public MapManager()
			{
				terrainMap_ = new TerrainMap(new Vector3D(0, 0, 0), new Vector3D(0, 0, 0));
				movementPlanner_ = new MovementPlanner(new Vector2D(0, 0), new Vector2D(0, 0));
				numPoints_ = 0;
				addFromPoint = new Vector3D(-37271.87, -21511.58, -43519.86);
			}

			/// <summary>
			/// Generates a movement planner from a terrain map with a valid up direction.
			/// </summary>
			/// <remarks>Does not copy points. Must be done before adding any points.</remarks>
			public void GenerateMovementPlanner()
			{
				var groundPlane = new PlaneD(addFromPoint, terrainMap_.UpDirection);

				var referenceCenter = new Vector3D(terrainMap_.Center);
				var projectedCenter = groundPlane.ProjectPoint(ref referenceCenter);
				var flatCenter = new Vector2D(projectedCenter.X, projectedCenter.Z);

				var referenceExtents = new Vector3D(terrainMap_.Extents);
				var projectedExtents = groundPlane.ProjectPoint(ref referenceExtents);
				var flatExtents = new Vector2D(Math.Abs(projectedExtents.X), Math.Abs(projectedExtents.Z));

				movementPlanner_ = new MovementPlanner(flatCenter, flatExtents);
			}

			public void AddPoint(Vector3D point, int timeout)
			{
				var dangerousPoints = terrainMap_.AddPoint(point, timeout);

				var groundPlane = new PlaneD(addFromPoint, terrainMap_.UpDirection);

				var referencePoint = new Vector3D(point);
				var projectedPoint = groundPlane.ProjectPoint(ref referencePoint);
				movementPlanner_.AddPoint(new Vector2D(projectedPoint.X, projectedPoint.Z), numPoints_, dangerousPoints.Count() > 0, timeout);
				numPoints_++;

				foreach (var dangerousPoint in dangerousPoints)
				{
					// Add to movement planner
					referencePoint = new Vector3D(dangerousPoint.Position);
					projectedPoint = groundPlane.ProjectPoint(ref referencePoint);
					movementPlanner_.AddPoint(new Vector2D(projectedPoint.X, projectedPoint.Z), dangerousPoint.ID, true, dangerousPoint.Timeout);
				}
			}

			public TerrainMap TerrainMap
			{
				get
				{
					return terrainMap_;
				}
				set
				{
					terrainMap_ = value;
					numPoints_ = terrainMap_.GetNumberOfPoints();
				}
			}

			public MovementPlanner MovementPlanner
			{
				get
				{
					return movementPlanner_;
				}
				set
				{
					movementPlanner_ = value;
				}
			}
		}
	}
}
