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
		/*

		This wrapper was made by Wanderer as part of Driver Assist System (DAS)
		https://steamcommunity.com/sharedfiles/filedetails/?id=1089115113

		*/
		public class SuspensionWrapper
		{
			public IMyMotorSuspension Obj { get; }
			public Base6Directions.Direction OrientationInVehicle { get; }
			public Vector3D WheelPositionAgainstCoM { get; set; }
			public Vector3D WheelPositionAgainstRef { get; set; }
			public double WheelPositionAgainstVelocity { get; set; }
			public double HeightOffsetMin { get; }
			public double HeightOffsetMax { get; }
			public double HeightOffsetRange { get; }
			public double WheelRadius { get; }
			public double PropulsionSign { get; }
			public bool IsSubgrid { get; }
			public double LeftMaxSteerAngle;
			public double RightMaxSteerAngle;
			public double TurnRadiusCurrent;
			public double TurnRadiusLeftMin;
			public double TurnRadiusRightMin;
			public double WeightDistributionRatio;
			public double BrakeFrictionDistributionRatio;
			public double SpeedLimit { get { return Obj.GetValueFloat("Speed Limit"); } set { Obj.SetValueFloat("Speed Limit", (float)value); } }
			public double PropulsionOverride { get { return Obj.GetValueFloat("Propulsion override"); } set { Obj.SetValueFloat("Propulsion override", (float)value); } }
			public double SteerOverride { get { return Obj.GetValueFloat("Steer override"); } set { Obj.SetValueFloat("Steer override", (float)value); } }
			public double Power { get { return Obj.Power; } set { Obj.Power = (float)value; } }
			public double Friction { get { return Obj.Friction; } set { Obj.Friction = (float)value; } }
			public double Strength { get { return Obj.Strength; } set { Obj.Strength = (float)value; } }
			public double Height { get { return Obj.Height; } set { Obj.Height = (float)value; } }
			public double MaxSteerAngle { get { return Obj.MaxSteerAngle; } set { Obj.MaxSteerAngle = (float)value; } }

			public SuspensionWrapper(IMyMotorSuspension suspension, Base6Directions.Direction orientation, bool subgrid = false)
			{
				Obj = suspension;
				OrientationInVehicle = orientation;
				IsSubgrid = subgrid;
				if (orientation == Base6Directions.Direction.Left)
					PropulsionSign = -1;
				else if (orientation == Base6Directions.Direction.Right)
					PropulsionSign = 1;
				HeightOffsetMin = suspension.GetMinimum<float>("Height");
				HeightOffsetMax = suspension.GetMaximum<float>("Height");
				HeightOffsetRange = HeightOffsetMax - HeightOffsetMin;

				if (suspension.CubeGrid.GridSizeEnum == MyCubeSize.Small)
				{
					if (suspension.BlockDefinition.SubtypeName.Contains("5x5")) WheelRadius = 1.25;
					else if (suspension.BlockDefinition.SubtypeName.Contains("3x3")) WheelRadius = 0.75;
					else if (suspension.BlockDefinition.SubtypeName.Contains("2x2")) WheelRadius = 0.5;// modded
					else if (suspension.BlockDefinition.SubtypeName.Contains("1x1")) WheelRadius = 0.25;
					else // some other modded wheels
						WheelRadius = suspension.IsAttached ? suspension.Top.WorldVolume.Radius * 0.79 / MathHelper.Sqrt2 : 0;
				}
				else
				{
					if (suspension.BlockDefinition.SubtypeName.Contains("5x5")) WheelRadius = 6.25;
					else if (suspension.BlockDefinition.SubtypeName.Contains("3x3")) WheelRadius = 3.75;
					else if (suspension.BlockDefinition.SubtypeName.Contains("2x2")) WheelRadius = 2.5;// modded
					else if (suspension.BlockDefinition.SubtypeName.Contains("1x1")) WheelRadius = 1.25;
					else // some other modded wheels
						WheelRadius = suspension.IsAttached ? suspension.Top.WorldVolume.Radius * 0.79 / MathHelper.Sqrt2 : 0;
				}
			}

			public Vector3 GetVelocityAtPoint(IMyShipController anchor)
			{
				Vector3 value = Vector3D.Zero;
				if (Obj.IsAttached)
				{
					Vector3 v = Obj.Top.GetPosition() - anchor.CenterOfMass;
					value = anchor.GetShipVelocities().LinearVelocity + anchor.GetShipVelocities().AngularVelocity.Cross(v);
				}
				return value;
			}

			public bool AddTopPart()
			{
				Obj.ApplyAction("Add Top Part");
				return Obj.IsAttached;
			}

			public void UpdateLocalPosition(IMyShipController anchor, Vector3D focalPointRef)
			{
				if (Obj.IsAttached)
				{
					Vector3D temp1, temp2;
					temp2 = Obj.Top.GetPosition() - anchor.CenterOfMass;
					temp1.X = anchor.WorldMatrix.Right.Dot(temp2);
					temp1.Y = anchor.WorldMatrix.Up.Dot(temp2);
					temp1.Z = anchor.WorldMatrix.Backward.Dot(temp2);
					WheelPositionAgainstCoM = temp1;
					temp2 = Obj.Top.GetPosition() - focalPointRef;
					temp1.X = anchor.WorldMatrix.Right.Dot(temp2);
					temp1.Y = anchor.WorldMatrix.Up.Dot(temp2);
					temp1.Z = anchor.WorldMatrix.Backward.Dot(temp2);
					WheelPositionAgainstRef = temp1;
				}
				else
					WheelPositionAgainstRef = WheelPositionAgainstCoM = Vector3D.Zero;
			}

			public void UpdatePositionVelocity(Vector3D velocity)
			{
				if (Obj.IsAttached)
					WheelPositionAgainstVelocity = velocity.Dot(WheelPositionAgainstCoM);
				else
					WheelPositionAgainstVelocity = 0;
			}
		}
	}
}
