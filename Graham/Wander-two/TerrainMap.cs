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
		public class TerrainMap
		{
			const int MAXIMUM_DEPTH = 30;
			const int MINIMUM_POINTS = 1;
			const int MAXIMUM_POINTS = 3;
			int pointsAdded_;
			int currentTime_;
			OcTree points_;
			Collider edgeDetectionCollider_;
			Vector3D upDirection_;

			public interface Collider
			{
				/// <summary>
				/// Returns true if the collider contains the given point.
				/// </summary>
				bool Contains(Vector3D point);

				/// <summary>
				/// A heuristic value for the farthest a point could be and still possibly be inside the collider.
				/// </summary>
				double MaxExtent { get; }

				Vector3D Position { get; set; }
			}

			public class SphereCollider : Collider
			{
				Vector3D center_;
				double radius_;

				public SphereCollider(Vector3D center, double radius)
				{
					center_ = center;
					radius_ = radius;
				}

				public bool Contains(Vector3D point)
				{
					return (Vector3D.Distance(center_, point) <= radius_);
				}

				public double MaxExtent
				{
					get
					{
						return radius_;
					}
				}

				public Vector3D Position
				{
					get
					{
						return center_;
					}
					set
					{
						center_ = value;
					}
				}

				public double Radius
				{
					get
					{
						return radius_;
					}
					set
					{
						radius_ = value;
					}
				}
			}

			/*public class BoxCollider : Collider
			{
				Vector3D center_;
				Vector3D extents_;
				MatrixD rotation_;
				double maxDistance_;

				public BoxCollider(Vector3D center, Vector3D extents, MatrixD rotation)
				{
					center_ = center;
					extents_ = extents;
					rotation_ = rotation;
					maxDistance_ = Vector3D.Distance(center, center + extents);
				}

				public BoxCollider(Vector3D center, Vector3D extents, Vector3D rotation)
				{
					center_ = center;
					extents_ = extents;
					rotation_ = MatrixD.CreateFromYawPitchRoll(rotation.X, rotation.Y, rotation.Z);
					maxDistance_ = Vector3D.Distance(center, center + extents);
				}

				public bool Contains(Vector3D point)
				{
					// The point needs to be aligned to the rotation of the box.

					// Begin with creating a local vector that starts from box's center and ends at the point.
					Vector3D localPoint = center_ - point;

					// Next, rotate it OPPOSITE the box's rotation (this is the same as rotating the box towards it).
					localPoint = Vector3D.Rotate(localPoint, MatrixD.Invert(rotation_));

					// Finally, determine if the (now locally aligned) point is within the extents.
					return (localPoint.X >= -extents_.X && localPoint.X < extents_.X &&
							localPoint.Y >= -extents_.Y && localPoint.Y < extents_.Y &&
							localPoint.Z >= -extents_.Z && localPoint.Z < extents_.Z);
				}

				public double MaxExtent
				{
					get
					{
						return maxDistance_;
					}
				}

				public Vector3D Position
				{
					get
					{
						return center_;
					}
					set
					{
						center_ = value;
					}
				}

				public Vector3D Extents
				{
					get
					{
						return extents_;
					}
					set
					{
						extents_ = value;
						maxDistance_ = extents_.Length();
					}
				}

				public MatrixD Rotation
				{
					get
					{
						return rotation_;
					}
					set
					{
						rotation_ = value;
					}
				}
			}*/

			public class HollowSphereCollider : Collider
			{
				Vector3D center_;
				double innerRadius_;
				double outerRadius_;

				public HollowSphereCollider(Vector3D center, double innerRadius, double outerRadius)
				{
					center_ = center;
					innerRadius_ = innerRadius;
					outerRadius_ = outerRadius;
				}

				public bool Contains(Vector3D point)
				{
					double distance = Vector3D.Distance(center_, point);
					return (innerRadius_ < distance && distance < outerRadius_);
				}

				public double MaxExtent
				{
					get
					{
						return outerRadius_;
					}
				}

				public Vector3D Position
				{
					get
					{
						return center_;
					}
					set
					{
						center_ = value;
					}
				}

				public double InnerRadius
				{
					get
					{
						return innerRadius_;
					}
					set
					{
						innerRadius_ = value;
					}
				}
				public double OuterRadius
				{
					get
					{
						return outerRadius_;
					}
					set
					{
						outerRadius_ = value;
					}
				}
			}

			public struct Point3D
			{
				public Point3D(Vector3D position, int id, int timeout)
				{
					Position = position;
					ID = id;
					Timeout = timeout;
				}

				public Vector3D Position { get; }

				public int ID { get; }

				public int Timeout { get; }
			}

			class OcTree
			{
				Vector3D center_;
				Vector3D extents_;
				List<OcTree> children_;
				List<Point3D> points_;
				int depth_;

				public OcTree(Vector3D newCenter, Vector3D newExtents, int newDepth)
				{
					center_ = newCenter;
					extents_ = newExtents;
					children_ = new List<OcTree>();
					points_ = new List<Point3D>();
					depth_ = newDepth;
				}

				public bool Contains(Vector3D point)
				{
					return (point.X >= center_.X - extents_.X && point.X < center_.X + extents_.X &&
							point.Y >= center_.Y - extents_.Y && point.Y < center_.Y + extents_.Y &&
							point.Z >= center_.Z - extents_.Z && point.Z < center_.Z + extents_.Z);
				}

				public bool GetOccupied()
				{
					return (points_.Count() > 0 || children_.Count() > 0);
				}

				public void AddPoint(Point3D point)
				{
					// Add point to children if present
					if (children_.Count() > 0)
					{
						GetContainingChild(point.Position).AddPoint(point);
					}
					else if (points_.Count() < MAXIMUM_POINTS)
					{
						points_.Add(point);
						// Subdivide if reached minimum points (and not maximum depth)
						if (points_.Count() >= MINIMUM_POINTS && depth_ < MAXIMUM_DEPTH &&
							extents_.X > 1 && extents_.Y > 1 && extents_.Z > 1)
						{
							Subdivide();
						}
					}
				}

				public void Update(int currentTime)
				{
					// Remove any points that have timed out
					int i = 0;
					while (i < points_.Count())
					{
						if (points_[i].Timeout >= currentTime)
						{
							points_.RemoveAt(i);
						}
						else
						{
							i++;
						}
					}

					foreach (var child in children_)
					{
						child.Update(currentTime);
					}
				}

				public void GetCollisions(ref List<Point3D> collisions, Collider collider)
				{
					// Automatically disqualify this quadrant if no points in it could be contained by the collider.
					if (Vector3D.Distance(center_, collider.Position) <= extents_.Length() + collider.MaxExtent)
					{
						if (children_.Count() == 0)
						{
							foreach (var point in points_)
							{
								if (collider.Contains(point.Position))
								{
									collisions.Add(point);
								}
							}
						}
						else
						{
							foreach (var child in children_)
							{
								child.GetCollisions(ref collisions, collider);
							}
						}
					}
				}

				void Subdivide()
				{
					// Create new children
					for (int i = 0; i < 8; i++)
					{
						Vector3D newCenter = new Vector3D(center_);
						switch (i)
						{
							case 0: newCenter.X += extents_.X / 2;
								newCenter.Y += extents_.Y / 2;
								newCenter.Z += extents_.Z / 2;
								break;
							case 1:
								newCenter.X += extents_.X / 2;
								newCenter.Y += extents_.Y / 2;
								newCenter.Z -= extents_.Z / 2;
								break;
							case 2:
								newCenter.X += extents_.X / 2;
								newCenter.Y -= extents_.Y / 2;
								newCenter.Z += extents_.Z / 2;
								break;
							case 3:
								newCenter.X += extents_.X / 2;
								newCenter.Y -= extents_.Y / 2;
								newCenter.Z -= extents_.Z / 2;
								break;
							case 4:
								newCenter.X -= extents_.X / 2;
								newCenter.Y += extents_.Y / 2;
								newCenter.Z += extents_.Z / 2;
								break;
							case 5:
								newCenter.X -= extents_.X / 2;
								newCenter.Y += extents_.Y / 2;
								newCenter.Z -= extents_.Z / 2;
								break;
							case 6:
								newCenter.X -= extents_.X / 2;
								newCenter.Y -= extents_.Y / 2;
								newCenter.Z += extents_.Z / 2;
								break;
							case 7:
								newCenter.X -= extents_.X / 2;
								newCenter.Y -= extents_.Y / 2;
								newCenter.Z -= extents_.Z / 2;
								break;
						}

						children_.Add(new OcTree(newCenter, extents_ / 2, depth_ + 1));
					}

					// Distribute points
					foreach (var point in points_)
					{
						foreach (var child in children_)
						{
							if (child.Contains(point.Position))
							{
								child.AddPoint(point);
								break;
							}
						}
					}
					points_.Clear();
				}

				OcTree GetContainingChild(Vector3D point)
				{
					if (children_.Count() > 0)
					{
						int childToAddTo = 0;

						if (point.X < center_.X)
						{
							childToAddTo += 4;
						}
						if (point.Y < center_.Y)
						{
							childToAddTo += 2;
						}
						if (point.Z < center_.Z)
						{
							childToAddTo += 1;
						}

						return children_[childToAddTo].GetContainingChild(point);
					}
					else
					{
						return this;
					}
				}

				public void GetPoints(ref List<Vector3D> points)
				{
					foreach (var child in children_)
					{
						child.GetPoints(ref points);
					}

					foreach (var point in points_)
					{
						points.Add(point.Position);
					}
				}

				public Vector3D Center
				{
					get
					{
						return center_;
					}
				}

				public Vector3D Extents
				{
					get
					{
						return extents_;
					}
				}
			}
			
			public TerrainMap(Vector3D center, Vector3D extents)
			{
				pointsAdded_ = 0;
				points_ = new OcTree(center, extents, 0);
				currentTime_ = 0;
				upDirection_ = new Vector3D(0, 0, 0);

				// The radius of a small grid large wheel is 1.25 (2.5 small blocks)
				edgeDetectionCollider_ = new HollowSphereCollider(center, 1.25, 3.0);
			}

			/// <summary>
			/// Adds a point. Returns a list of Point3Ds that are part of dangerous bumps as a result.
			/// </summary>
			public List<Point3D> AddPoint(Vector3D point, int timeout)
			{
				// Center the edge detection collider on the new point
				edgeDetectionCollider_.Position = point;

				// Find possible dangerous edges
				var candidatePoints = new List<Point3D>();
				points_.GetCollisions(ref candidatePoints, edgeDetectionCollider_);

				// Decide whether each point pairing is a dangerous edge
				var dangerousPoints = new List<Point3D>();
				bool pointIsDangerous = false;
				foreach (var point2 in candidatePoints)
				{
					if (IsEdgeDangerous(point, point2.Position))
					{
						dangerousPoints.Add(point2);
						pointIsDangerous = true;
					}
				}

				var newPoint = new Point3D(point, pointsAdded_, timeout);
				points_.AddPoint(newPoint);
				pointsAdded_++;
				if (pointIsDangerous)
				{
					dangerousPoints.Add(newPoint);
				}

				return dangerousPoints;
			}

			/// <summary>
			/// Returns true if a line drawn between two points is estimated to be dangerous.
			/// </summary>
			bool IsEdgeDangerous(Vector3D p1, Vector3D p2)
			{
				var dot = VRageMath.Vector3D.Dot(upDirection_, Vector3D.Normalize(p2 - p1));
				var angle = (90 - Math.Acos(dot) * 180 / Math.PI);

				return angle > 50 || angle < -50;
			}

			public Vector3D UpDirection
			{
				get
				{
					return upDirection_;
				}
				set
				{
					upDirection_ = value;
				}
			}

			public void UpdateTick(int ticksSinceLastUpdate)
			{
				currentTime_ += ticksSinceLastUpdate;
				points_.Update(currentTime_);
			}

			public List<Vector3D> GetPoints()
			{
				var points = new List<Vector3D>();
				points_.GetPoints(ref points);
				return points;
			}

			public List<Point3D> GetCollisions(Collider collider)
			{
				var points = new List<Point3D>();
				points_.GetCollisions(ref points, collider);
				return points;
			}

			public int GetNumberOfPoints()
			{
				return pointsAdded_;
			}

			public Vector3D Center
			{
				get
				{
					return points_.Center;
				}
			}

			public Vector3D Extents
			{
				get
				{
					return points_.Extents;
				}
			}
		}

	}
}
