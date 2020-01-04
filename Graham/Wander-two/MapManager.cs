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

			public MapManager()
			{
				terrainMap_ = new TerrainMap(new Vector3D(0, 0, 0), new Vector3D(0, 0, 0));
				movementPlanner_ = new MovementPlanner(new Vector2D(0, 0), new Vector2D(0, 0));
				numPoints_ = 0;
				CurrentLocation = new Vector3D(0, 0, 0);
				UpDirection = new Vector3D(0, 1, 0);
				X_Axis = new Vector3D(1, 0, 0);
				Y_Axis = new Vector3D(0, 0, 1);
			}

			/// <summary>
			/// Generates a movement planner from a terrain map with a valid up direction.
			/// </summary>
			/// <remarks>Does not copy points. Must be done before adding any points.</remarks>
			public void GenerateMovementPlanner()
			{
				var groundPlane = new PlaneD(CurrentLocation, UpDirection);

				var referenceCenter = new Vector3D(terrainMap_.Center);
				var projectedCenter = groundPlane.ProjectPoint(ref referenceCenter);
				var projectedCenterX = (projectedCenter - CurrentLocation).Dot(X_Axis);
				var projectedCenterY = (projectedCenter - CurrentLocation).Dot(Y_Axis);
				var flatCenter = new Vector2D(projectedCenterX, projectedCenterY);

				var referenceExtents = new Vector3D(terrainMap_.Extents);
				var projectedExtents = groundPlane.ProjectPoint(ref referenceExtents);
				var maxExtent = Math.Abs((projectedExtents - CurrentLocation).Dot(X_Axis));
				maxExtent = Math.Max(maxExtent, Math.Abs((projectedExtents - CurrentLocation).Dot(Y_Axis)));
				var flatExtents = new Vector2D(maxExtent, maxExtent);

				movementPlanner_ = new MovementPlanner(flatCenter, flatExtents);
			}

			public void AddPoint(Vector3D point, int timeout)
			{
				var dangerousPoints = terrainMap_.AddPoint(point, timeout);

				var groundPlane = new PlaneD(CurrentLocation, UpDirection);

				/*var referencePoint = new Vector3D(point);
				var projectedPoint = groundPlane.ProjectPoint(ref referencePoint);
				var projectedX = (projectedPoint - CurrentLocation).Dot(X_Axis);
				var projectedY = (projectedPoint - CurrentLocation).Dot(Y_Axis);
				movementPlanner_.AddPoint(new Vector2D(projectedX, projectedY), numPoints_, dangerousPoints.Count() > 0, timeout);
				numPoints_++;*/

				Vector3D referencePoint, projectedPoint;
				double projectedX, projectedY;

				foreach (var dangerousPoint in dangerousPoints)
				{
					// Add to movement planner
					referencePoint = new Vector3D(dangerousPoint.Position);
					projectedPoint = groundPlane.ProjectPoint(ref referencePoint);
					projectedX = (projectedPoint - CurrentLocation).Dot(X_Axis);
					projectedY = (projectedPoint - CurrentLocation).Dot(Y_Axis);
					movementPlanner_.AddPoint(new Vector2D(projectedX, projectedY), dangerousPoint.ID, true, dangerousPoint.Timeout);
				}
			}

			public MovementPlanner.MovementNode GenerateNode(Vector3D position, double facingAngle, double desiredSpeed, Vector3D goal)
			{
				var groundPlane = new PlaneD(CurrentLocation, UpDirection);

				var referencePoint = new Vector3D(position);
				var projectedPoint = groundPlane.ProjectPoint(ref referencePoint);
				var projectedX = (projectedPoint - CurrentLocation).Dot(X_Axis);
				var projectedY = (projectedPoint - CurrentLocation).Dot(Y_Axis);
				var projectedPosition = new Vector2D(projectedX, projectedY);

				referencePoint = new Vector3D(goal);
				projectedPoint = groundPlane.ProjectPoint(ref referencePoint);
				projectedX = (projectedPoint - CurrentLocation).Dot(X_Axis);
				projectedY = (projectedPoint - CurrentLocation).Dot(Y_Axis);
				var projectedGoal = new Vector2D(projectedX, projectedY);

				return new MovementPlanner.MovementNode(projectedPosition, facingAngle, desiredSpeed, projectedGoal, 0);
			}

			public Vector2D ProjectPoint(Vector3D point)
			{
				var groundPlane = new PlaneD(CurrentLocation, UpDirection);
				var referencePoint = point;
				var projectedPoint = groundPlane.ProjectPoint(ref referencePoint);
				var projectedX = (projectedPoint - CurrentLocation).Dot(X_Axis);
				var projectedY = (projectedPoint - CurrentLocation).Dot(Y_Axis);
				return new Vector2D(projectedX, projectedY);
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

			public void SetX_Direction(Vector3D xDirection)
			{
				var groundPlane = new PlaneD(CurrentLocation, UpDirection);
				xDirection.Normalize();
				X_Axis = xDirection;
			}

			public void SetY_Direction(Vector3D yDirection)
			{
				var groundPlane = new PlaneD(CurrentLocation, UpDirection);
				yDirection.Normalize();
				Y_Axis = yDirection;
			}

			public Vector3D CurrentLocation { get; set; }

			public Vector3D UpDirection
			{
				get
				{
					return terrainMap_.UpDirection;
				}
				set
				{
					terrainMap_.UpDirection = value;
				}
			}

			public Vector3D X_Axis { get; set; }
			public Vector3D Y_Axis { get; set; }
		}
	}
}
