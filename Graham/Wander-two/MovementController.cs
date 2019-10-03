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
		struct VehicleWheel
		{
			public SuspensionWrapper Wheel { get; set; }
			public bool IsFront { get; set; }
			public double SteeringAngleMultiplier { get; set; }
			public VehicleWheel(SuspensionWrapper wheel, bool isFront, double steeringAngleMultiplier)
			{
				Wheel = wheel;
				IsFront = isFront;
				SteeringAngleMultiplier = steeringAngleMultiplier;
			}
		}

		public class MovementController
		{
			List<VehicleWheel> Wheels;
			public IMyShipController Controller { get; set; }
			public double DesiredSpeed { get; set; }

			Vector3D CenterOfMass;
			MatrixD CenterOfMassMatrix;

			public MovementController()
			{
				DesiredSpeed = 0;
				Wheels = new List<VehicleWheel>();
			}

			public bool Update()
			{
				bool isValid = Controller != null;

				if (Controller != null)
				{
					CenterOfMass = Controller.CenterOfMass;
					CenterOfMassMatrix = MatrixD.CreateWorld(CenterOfMass, Controller.WorldMatrix.Forward, Controller.WorldMatrix.Up);
				}

				return isValid;
			}

			public void AddWheel(IMyMotorSuspension wheel)
			{
				Base6Directions.Direction blockOrientation = Controller.Orientation.TransformDirectionInverse(wheel.Orientation.Up);
				if (blockOrientation == Base6Directions.Direction.Left || blockOrientation == Base6Directions.Direction.Right)
				{
					double steeringAngle = Math.Abs(GetAngleFromCoM(wheel.GetPosition()));
					if (steeringAngle > Math.PI / 2)
					{
						steeringAngle -= Math.PI / 2;
					}
					if (!IsFrontSide(wheel.GetPosition()))
					{
						steeringAngle = Math.PI / 2 - steeringAngle;
					}
					Wheels.Add(new VehicleWheel(new SuspensionWrapper(wheel, blockOrientation), IsFrontSide(wheel.GetPosition()), steeringAngle / 90));
				}
			}

			public void Move(double angle)
			{
				double directionalVelocity = Vector3D.Dot(Controller.GetShipVelocities().LinearVelocity, CenterOfMassMatrix.Forward);

				var idealAngle = Math.Min(angle, GetMaximumSafeSteeringAngle(directionalVelocity));
				Steer(idealAngle);

				// Consider close enough if within 10% of desired speed
				if (Math.Abs(DesiredSpeed - directionalVelocity) > DesiredSpeed * 0.1)
				{
					bool accelerate = directionalVelocity < DesiredSpeed;

					foreach (var wheel in Wheels)
					{
						if (accelerate)
						{
							wheel.Wheel.PropulsionOverride -= 0.2;
						}
						else
						{
							wheel.Wheel.PropulsionOverride += 0.2;
						}
					}
				}
			}

			public void Steer(double angle)
			{
				foreach (var wheel in Wheels)
				{
					wheel.Wheel.SteerOverride = (float)(angle * wheel.SteeringAngleMultiplier);
				}
			}

			/// <summary>
			/// Determines the maximum safe angle that the vehicle can attempt to turn at a given speed. NOT per wheel!
			/// </summary>
			double GetMaximumSafeSteeringAngle(double speed)
			{
				return 30;
			}

			double GetAngleFromCoM(Vector3D point)
			{
				var localPosition = Vector3D.Transform(point, MatrixD.Invert(CenterOfMassMatrix));
				return Math.Atan2(-localPosition.X, -localPosition.Z);
			}

			bool IsFrontSide(Vector3D point)
			{
				var angle = GetAngleFromCoM(point);

				bool front = angle > -Math.PI / 2 && angle < Math.PI / 2;

				return front;
			}
		}
	}
}
