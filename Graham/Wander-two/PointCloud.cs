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
		public class PointCloud
		{
			public const int TIMEOUT_MOVING = 1000;
			public const int TIMEOUT_TERRAIN = 100000;
			const int MAXIMUM_DEPTH = 20;
			const int MINIMUM_POINTS = 1;
			int currentTime_;
			OcTree ocTree_;

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

			public class BoxCollider : Collider
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
			}

			public struct Point
			{
				Vector3D position_;
				int timeout_;

				public Point(Vector3D position, int timeout)
				{
					position_ = position;
					timeout_ = timeout;
				}

				public Vector3D Position
				{
					get
					{
						return position_;
					}
				}

				public int Timeout
				{
					get
					{
						return timeout_;
					}
				}
			}

			class OcTree
			{
				Vector3D center_;
				Vector3D extents_;
				List<OcTree> children_;
				List<Point> points_;
				int depth_;
				bool occupied_;

				public OcTree(Vector3D newCenter, Vector3D newExtents, int newDepth)
				{
					center_ = newCenter;
					extents_ = newExtents;
					children_ = new List<OcTree>();
					points_ = new List<Point>();
					depth_ = newDepth;
					occupied_ = false;
				}

				public bool Contains(Vector3D point)
				{
					return (point.X >= center_.X - extents_.X && point.X < center_.X + extents_.X &&
							point.Y >= center_.Y - extents_.Y && point.Y < center_.Y + extents_.Y &&
							point.Z >= center_.Z - extents_.Z && point.Z < center_.Z + extents_.Z);
				}

				public bool GetOccupied()
				{
					bool occupied = points_.Count() > 0;
					foreach (var child in children_)
					{
						if (child.GetOccupied())
						{
							occupied = true;
							break;
						}
					}
					return occupied;
				}

				public void AddPoint(Vector3D point, int timeout)
				{
					// Add point to children if present
					if (children_.Count() > 0)
					{
						GetContainingChild(point).AddPoint(point, timeout);
					}
					else
					{
						occupied_ = true;
						points_.Add(new Point(point, timeout));
						// Subdivide if reached minimum points (and not maximum depth)
						if (points_.Count() > MINIMUM_POINTS && depth_ < MAXIMUM_DEPTH)
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

					if (points_.Count() > 0)
					{
						// Undo subdivision if all children are empty
						bool allChildrenEmpty = true;
						foreach (var child in children_)
						{
							child.Update(currentTime);
							if (allChildrenEmpty && child.GetOccupied())
							{
								allChildrenEmpty = false;
							}
						}

						if (allChildrenEmpty)
						{
							children_.Clear();
						}
					}
				}

				public void GetPossibleCollisions(ref List<Vector3D> possibleCollisions, Collider collider)
				{
					// Automatically disqualify this quadrant if no points in it could be contained by the collider.
					if (Vector3D.Distance(center_, collider.Position) <= extents_.Length() + collider.MaxExtent)
					{
						if (children_.Count() == 0)
						{
							foreach (var point in points_)
							{
								if (Vector3D.Distance(collider.Position, point.Position) <= collider.MaxExtent)
								{
									possibleCollisions.Add(point.Position);
								}
							}
						}
						else
						{
							foreach (var child in children_)
							{
								GetPossibleCollisions(ref possibleCollisions, collider);
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
								child.AddPoint(point.Position, point.Timeout);
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

						return children_[childToAddTo];
					}
					else
					{
						return this;
					}
				}
			}

			public PointCloud(Vector3D center, Vector3D extents)
			{
				ocTree_ = new OcTree(center, extents, 0);
				currentTime_ = 0;
			}

			public List<Vector3D> GetCollidingPoints(Collider collider)
			{
				var collidingPoints = new List<Vector3D>();

				ocTree_.GetPossibleCollisions(ref collidingPoints, collider);

				// Cut list down to actual collisions.
				int i = 0;
				while (i < collidingPoints.Count())
				{
					if (!collider.Contains(collidingPoints[i]))
					{
						collidingPoints.RemoveAt(i);
					}
					else
					{
						i++;
					}
				}

				return collidingPoints;
			}

			public void UpdateTick(int ticksSinceLastUpdate)
			{
				currentTime_ += ticksSinceLastUpdate;
				ocTree_.Update(currentTime_);
			}
		}
	}
}
