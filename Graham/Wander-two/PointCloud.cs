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
			int currentTime;
			OcTree ocTree;

			public interface Collider
			{
				/// <summary>
				/// Returns true if the collider contains the given point.
				/// </summary>
				bool Contains(Vector3D point);

				/// <summary>
				/// A heuristic value for the farthest a point could be and still possibly be inside the collider.
				/// </summary>
				double GetMaximumPossibleExtent();

				Vector3D Position { get; set; }
			}

			public class SphereCollider : Collider
			{
				Vector3D center;
				double radius;

				public SphereCollider(Vector3D center, double radius)
				{
					this.center = center;
					this.radius = radius;
				}

				public bool Contains(Vector3D point)
				{
					return (Vector3D.Distance(center, point) <= radius);
				}

				public double GetMaximumPossibleExtent()
				{
					return radius;
				}

				public override Vector3D Position
				{
					get
					{
						return center;
					}
					set
					{
						center = value;
					}
				}

				public double Radius
				{
					get
					{
						return radius;
					}
					set
					{
						radius = value;
					}
				}
			}

			public class BoxCollider : Collider
			{
				Vector3D center;
				Vector3D extents;
				MatrixD rotation;
				double maxDistance;

				public BoxCollider(Vector3D center, Vector3D extents, MatrixD rotation)
				{
					this.center = center;
					this.extents = extents;
					this.rotation = rotation;
					maxDistance = Vector3D.Distance(center, center + extents);
				}

				public BoxCollider(Vector3D center, Vector3D extents, Vector3D rotation)
				{
					this.center = center;
					this.extents = extents;
					this.rotation = MatrixD.CreateFromYawPitchRoll(rotation.X, rotation.Y, rotation.Z);
					maxDistance = Vector3D.Distance(center, center + extents);
				}

				public bool Contains(Vector3D point)
				{
					// The point needs to be aligned to the rotation of the box.

					// Begin with creating a local vector that starts from box's center and ends at the point.
					Vector3D localPoint = center - point;

					// Next, rotate it OPPOSITE the box's rotation (this is the same as rotating the box towards it).
					localPoint = Vector3D.Rotate(localPoint, MatrixD.Invert(rotation));

					// Finally, determine if the (now locally aligned) point is within the extents.
					return (localPoint.X >= -extents.X && localPoint.X < extents.X &&
							localPoint.Y >= -extents.Y && localPoint.Y < extents.Y &&
							localPoint.Z >= -extents.Z && localPoint.Z < extents.Z);
				}

				public double GetMaximumPossibleExtent()
				{
					return maxDistance;
				}

				public Vector3D Position
				{
					get
					{
						return center;
					}
					set
					{
						center = value;
					}
				}

				public Vector3D Extents
				{
					get
					{
						return extents;
					}
					set
					{
						extents = value;
						maxDistance = extents.Length();
					}
				}

				public MatrixD Rotation
				{
					get
					{
						return rotation;
					}
					set
					{
						rotation = value;
					}
				}
			}

			public struct Point
			{
				public Vector3D Position;
				public int timeout;

				public Point(Vector3D position, int invalidAfter)
				{
					Position = position;
					timeout = invalidAfter;
				}

				public Point(Point p)
				{
					Position = p.Position;
					timeout = p.timeout;
				}
			}

			class OcTree
			{
				Vector3D center;
				Vector3D extents;
				List<OcTree> children;
				List<Point> points;
				int depth;
				bool occupied;

				public OcTree(Vector3D newCenter, Vector3D newExtents, int newDepth)
				{
					center = newCenter;
					extents = newExtents;
					children = new List<OcTree>();
					points = new List<Point>();
					depth = newDepth;
					occupied = false;
				}

				public bool Contains(Vector3D point)
				{
					return (point.X >= center.X - extents.X && point.X < center.X + extents.X &&
							point.Y >= center.Y - extents.Y && point.Y < center.Y + extents.Y &&
							point.Z >= center.Z - extents.Z && point.Z < center.Z + extents.Z);
				}

				public bool GetOccupied()
				{
					bool occupied = points.Count() > 0;
					foreach (var child in children)
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
					if (children.Count() > 0)
					{
						GetContainingChild(point).AddPoint(point, timeout);
					}
					else
					{
						occupied = true;
						points.Add(new Point(point, timeout));
						// Subdivide if reached minimum points (and not maximum depth)
						if (points.Count() > MINIMUM_POINTS && depth < MAXIMUM_DEPTH)
						{
							Subdivide();
						}
					}
				}

				public void Update(int currentTime)
				{
					// Remove any points that have timed out
					int i = 0;
					while (i < points.Count())
					{
						if (points[i].timeout >= currentTime)
						{
							points.RemoveAt(i);
						}
						else
						{
							i++;
						}
					}

					if (points.Count() > 0)
					{
						// Undo subdivision if all children are empty
						bool allChildrenEmpty = true;
						foreach (var child in children)
						{
							child.Update(currentTime);
							if (allChildrenEmpty && child.GetOccupied())
							{
								allChildrenEmpty = false;
							}
						}

						if (allChildrenEmpty)
						{
							children.Clear();
						}
					}
				}

				public void GetPossibleCollisions(ref List<Vector3D> possibleCollisions, Collider collider)
				{
					// Automatically disqualify this quadrant if no points in it could be contained by the collider.
					if (Vector3D.Distance(center, collider.Position) <= extents.Length() + collider.GetMaximumPossibleExtent())
					{
						if (children.Count() == 0)
						{
							foreach (var point in points)
							{
								if (Vector3D.Distance(collider.Position, point.Position) <= collider.GetMaximumPossibleExtent())
								{
									possibleCollisions.Add(point.Position);
								}
							}
						}
						else
						{
							foreach (var child in children)
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
						Vector3D newCenter = new Vector3D(center);
						switch (i)
						{
							case 0: newCenter.X += extents.X / 2;
								newCenter.Y += extents.Y / 2;
								newCenter.Z += extents.Z / 2;
								break;
							case 1:
								newCenter.X += extents.X / 2;
								newCenter.Y += extents.Y / 2;
								newCenter.Z -= extents.Z / 2;
								break;
							case 2:
								newCenter.X += extents.X / 2;
								newCenter.Y -= extents.Y / 2;
								newCenter.Z += extents.Z / 2;
								break;
							case 3:
								newCenter.X += extents.X / 2;
								newCenter.Y -= extents.Y / 2;
								newCenter.Z -= extents.Z / 2;
								break;
							case 4:
								newCenter.X -= extents.X / 2;
								newCenter.Y += extents.Y / 2;
								newCenter.Z += extents.Z / 2;
								break;
							case 5:
								newCenter.X -= extents.X / 2;
								newCenter.Y += extents.Y / 2;
								newCenter.Z -= extents.Z / 2;
								break;
							case 6:
								newCenter.X -= extents.X / 2;
								newCenter.Y -= extents.Y / 2;
								newCenter.Z += extents.Z / 2;
								break;
							case 7:
								newCenter.X -= extents.X / 2;
								newCenter.Y -= extents.Y / 2;
								newCenter.Z -= extents.Z / 2;
								break;
						}

						children.Add(new OcTree(newCenter, extents / 2, depth + 1));
					}

					// Distribute points
					foreach (var point in points)
					{
						foreach (var child in children)
						{
							if (child.Contains(point.Position))
							{
								child.AddPoint(point.Position, point.timeout);
								break;
							}
						}
					}
					points.Clear();
				}

				OcTree GetContainingChild(Vector3D point)
				{
					if (children.Count() > 0)
					{
						int childToAddTo = 0;

						if (point.X < center.X)
						{
							childToAddTo += 4;
						}
						if (point.Y < center.Y)
						{
							childToAddTo += 2;
						}
						if (point.Z < center.Z)
						{
							childToAddTo += 1;
						}

						return children[childToAddTo];
					}
					else
					{
						return this;
					}
				}
			}

			public PointCloud(Vector3D center, Vector3D extents)
			{
				ocTree = new OcTree(center, extents, 0);
				currentTime = 0;
			}

			public List<Vector3D> GetCollidingPoints(Collider collider)
			{
				ocTree.Update(currentTime);

				var collidingPoints = new List<Vector3D>();

				ocTree.GetPossibleCollisions(ref collidingPoints, collider);

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

			/// <summary>
			/// Call this once per tick to make sure points timeout when they should!
			/// </summary>
			public void UpdateTick()
			{
				currentTime++;
			}
		}
	}
}
