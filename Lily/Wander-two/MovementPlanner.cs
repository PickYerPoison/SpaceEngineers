﻿using Sandbox.Game.EntityComponents;
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
		public class MovementPlanner
		{
			/// <summary>
			/// The maximum degrees the wheels can turn in either direction.
			/// </summary>
			const double MAXIMUM_TURN_DEGREES = 30;

			/// <summary>
			/// The maximum speed the vehicle will attempt to travel at.
			/// </summary>
			const double MAXIMUM_SPEED = 5;

			/// <summary>
			/// The number of additional angles motion planning should consider, beyond no turning and both extremes. If greater than zero, the extreme angle is divided by this to obtain the interval between angles.
			/// </summary>
			const int EXTRA_TURN_ANGLES = 0;

			/// <summary>
			/// How deep the planning should go.
			/// </summary>
			const int MAXIMUM_THINKAHEAD = 5;

			const int MAXIMUM_DEPTH = 50;
			const int MINIMUM_POINTS = 5;
			const int MAXIMUM_POINTS = 10;

			QuadTree points_;

			public enum ColliderTypeEnum
			{
				CIRCLE,
				RECTANGLE
			}

			public interface Collider
			{
				/// <summary>
				/// Returns true if the collider contains the given point.
				/// </summary>
				bool Contains(Vector2D point);

				/// <summary>
				/// Centers the collider on a movement node.
				/// </summary>
				void CenterOn(MovementNode node);

				/// <summary>
				/// A heuristic value for the farthest a point could be and still possibly be inside the collider.
				/// </summary>
				double MaxExtent { get; }

				Vector2D Position { get; set; }

				ColliderTypeEnum Type { get; }
			}

			public class CircleCollider : Collider
			{
				Vector2D center_;
				double radius_;

				public CircleCollider(Vector2D center, double radius)
				{
					center_ = center;
					radius_ = radius;
				}

				public bool Contains(Vector2D point)
				{
					return (Vector2D.Distance(center_, point) <= radius_);
				}

				public void CenterOn(MovementNode node)
				{
					center_ = node.Position;
				}

				public double MaxExtent
				{
					get
					{
						return radius_;
					}
				}

				public Vector2D Position
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

				public ColliderTypeEnum Type
				{
					get
					{
						return ColliderTypeEnum.CIRCLE;
					}
				}
			}

			public class RectangleCollider : Collider
			{
				Vector2D center_;
				Vector2D extents_;
				double rotation_;
				double maxDistance_;

				public RectangleCollider(Vector2D center, Vector2D extents, double rotation)
				{
					center_ = center;
					extents_ = extents;
					rotation_ = rotation;
					maxDistance_ = Vector2D.Distance(center, center + extents);
				}

				public bool Contains(Vector2D point)
				{
					// The point needs to be aligned to the rotation of the box.

					// Begin with creating a local vector that starts from box's center and ends at the point.
					Vector2D localPoint = center_ - point;

					// Next, rotate it OPPOSITE the box's rotation (this is the same as rotating the box towards it).
					localPoint.Rotate(-rotation_);

					// Finally, determine if the (now locally aligned) point is within the extents.
					return (localPoint.X >= -extents_.X && localPoint.X < extents_.X &&
							localPoint.Y >= -extents_.Y && localPoint.Y < extents_.Y);
				}

				public void CenterOn(MovementNode node)
				{
					center_ = node.Position;
					rotation_ = node.Rotation;
				}

				public double MaxExtent
				{
					get
					{
						return maxDistance_;
					}
				}

				public Vector2D Position
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

				public Vector2D Extents
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

				public double Rotation
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

				public ColliderTypeEnum Type
				{
					get
					{
						return ColliderTypeEnum.RECTANGLE;
					}
				}
			}

			public struct Point2D
			{
				public Point2D(Vector2D position, int id, bool dangerous, int timeout)
				{
					Position = position;
					ID = id;
					Dangerous = dangerous;
					Timeout = timeout;
				}

				public Vector2D Position { get; }

				public int ID { get; }

				public bool Dangerous { get; }

				public int Timeout { get; }
			}

			public class MovementNode
			{
				Vector2D position_;
				double facingAngle_;
				double desiredSpeed_;

				Vector2D goal_;

				/// <summary>
				/// Distance to the goal from this node. Heuristic value.
				/// </summary>
				double distanceToGoal_;

				/// <summary>
				/// Best distance to the goal from a leaf on this branch.
				/// </summary>
				double bestBranchDistanceToGoal_;

				List<MovementNode> children_;

				public List<MovementNode> Children { get { return children_; } }

				public bool GeneratedChildren { get; set; }

				int depth_;

				bool markedForDeletion_;

				public MovementNode(Vector2D position, double facingAngle, double desiredSpeed, Vector2D goal, int depth)
				{
					position_ = position;
					facingAngle_ = facingAngle;
					desiredSpeed_ = desiredSpeed;
					goal_ = goal;
					distanceToGoal_ = Vector2D.Distance(position_, goal_);
					bestBranchDistanceToGoal_ = distanceToGoal_;
					depth_ = depth;

					children_ = new List<MovementNode>();
					markedForDeletion_ = false;
					GeneratedChildren = false;
				}

				public void CreateChildNodes(ref QuadTree tree)
				{
					// Create extremes
					CreateChildNode(ref tree, desiredSpeed_, facingAngle_);
					CreateChildNode(ref tree, Math.Max(0, desiredSpeed_ - 5), facingAngle_);
					CreateChildNode(ref tree, Math.Max(MAXIMUM_SPEED, desiredSpeed_ + 5), facingAngle_);
					CreateChildNode(ref tree, desiredSpeed_, facingAngle_ + DegreesToRadians(MAXIMUM_TURN_DEGREES));
					CreateChildNode(ref tree, desiredSpeed_, facingAngle_ - DegreesToRadians(MAXIMUM_TURN_DEGREES));

					// Create additional angles
					/*if (EXTRA_TURN_ANGLES > 0)
					{
						double increment = MAXIMUM_TURN_DEGREES / EXTRA_TURN_ANGLES;
						for (int i = 1; i <= EXTRA_TURN_ANGLES; i++)
						{
							CreateChildNode(ref tree, facingAngle_ + increment * i);
							CreateChildNode(ref tree, facingAngle_ - increment * i);
						}
					}*/

					GeneratedChildren = true;
				}

				void CreateChildNode(ref QuadTree tree, double desiredSpeed, double angle)
				{
					var newPosition = new Vector2D(Math.Cos(angle) * desiredSpeed_, Math.Sin(angle) * desiredSpeed_) + position_;

					var newNode = new MovementNode(newPosition, angle, desiredSpeed, goal_, depth_ + 1);

					if (tree.AddNode(newNode))
					{
						children_.Add(newNode);
					}
				}

				public void MarkForDeletion()
				{
					markedForDeletion_ = true;
				}

				/// <summary>
				/// Updates the heuristic values of this node and nodes that branch from it.
				/// </summary>
				public void UpdateHeuristicValue()
				{
					bool hadChildren = children_.Count() > 0;

					// Remove all children that are marked for deletion.
					children_.RemoveAll(delegate (MovementNode node) { return node.markedForDeletion_; });

					if (children_.Count() > 0)
					{
						// Update heuristic values for all non-deleted children.
						foreach (var child in children_)
						{
							child.UpdateHeuristicValue();
						}

						// Sort remaining children in ascending order by heuristic. 
						children_.Sort(delegate (MovementNode a, MovementNode b)
						{
							return a.distanceToGoal_.CompareTo(b.distanceToGoal_);
						});

						// Update for best branch distance.
						bestBranchDistanceToGoal_ = children_.First().bestBranchDistanceToGoal_;
					}
					else if (hadChildren)
					{
						// If all the children of this node have been removed, this is a dead path.
						MarkForDeletion();
					}
					else
					{
						bestBranchDistanceToGoal_ = distanceToGoal_;
					}
				}

				public Vector2D Position
				{
					get
					{
						return position_;
					}
					set
					{
						position_ = value;
					}
				}

				public double Rotation
				{
					get
					{
						return facingAngle_;
					}
					set
					{
						facingAngle_ = value;
					}
				}

				public double Speed
				{
					get
					{
						return desiredSpeed_;
					}
					set
					{
						desiredSpeed_ = value;
					}
				}

				/// <summary>
				/// Returns the leaf node with the best heuristic value.
				/// </summary>
				public MovementNode GetBestChild()
				{
					if (children_.Count() == 0)
					{
						return this;
					}
					else
					{
						return children_.First();
					}
				}

				/// <summary>
				/// Returns if this is a leaf node.
				/// </summary>
				public bool IsLeaf()
				{
					return children_.Count() == 0;
				}

				public void DebugSetAngle(double angleInDegrees)
				{
					facingAngle_ = DegreesToRadians(angleInDegrees);
					GeneratedChildren = false;
					children_.Clear();
				}

				public static double DegreesToRadians(double angleInDegrees)
				{
					return angleInDegrees * (Math.PI / 180.0);
				}

				public static double RadiansToDegrees(double angleInRadians)
				{
					return angleInRadians * (180.0 / Math.PI);
				}
			}

			public class QuadTree
			{
				Vector2D center_;
				Vector2D extents_;
				List<QuadTree> children_;
				List<Point2D> points_;
				List<MovementNode> nodes_;
				int depth_;
				Collider invalidNodeDetectionCollider_;

				public QuadTree(Vector2D center, Vector2D extents, int depth)
				{
					center_ = center;
					extents_ = extents;
					children_ = new List<QuadTree>();
					points_ = new List<Point2D>();
					nodes_ = new List<MovementNode>();
					depth_ = depth;

					invalidNodeDetectionCollider_ = new RectangleCollider(new Vector2D(0, 0), new Vector2D(0, 0), 0);
				}

				public bool Contains(Vector2D point)
				{
					return (point.X >= center_.X - extents_.X && point.X < center_.X + extents_.X &&
							point.Y >= center_.Y - extents_.Y && point.Y < center_.Y + extents_.Y);
				}

				/// <summary>
				/// Adds a new point. Purges any movement nodes that are violated by it and reports if any were.
				/// </summary>
				public bool AddPoint(Point2D point)
				{
					// Add point to children if present
					if (children_.Count() > 0)
					{
						GetContainingChild(point.Position).AddPoint(point);
					}
					else if (points_.Count() < MAXIMUM_POINTS)
					{
						// Remove any matching points to avoid re-adding the same points
						points_.RemoveAll(delegate (Point2D p1) { return p1.ID.Equals(point.ID); });

						points_.Add(point);
						// Subdivide if reached minimum points (and not maximum depth)
						if (points_.Count() >= MINIMUM_POINTS && depth_ < MAXIMUM_DEPTH &&
							extents_.X > 0.5 && extents_.Y > 0.5)
						{
							Subdivide();
						}
					}

					// Purge movement nodes that are violated by the addition of this point!
					if (point.Dangerous)
					{
						invalidNodeDetectionCollider_.Position = point.Position;
						return PurgeInvalidNodes(invalidNodeDetectionCollider_);
					}
					else
					{
						return false;
					}
				}

				/// <summary>
				/// Attempts to add a node. Fails if there is a potentially dangerous collision, or if no points were nearby.
				/// </summary>
				public bool AddNode(MovementNode node)
				{
					// Check for collisions around the node.
					invalidNodeDetectionCollider_.CenterOn(node);

					// Track how many points were within the collider here.
					int numPointsDetected = 0;

					// I need to make this properly check for collisions around it. Just the same tile isn't enough!
					if (!GetAnyDangerousPointCollisions(invalidNodeDetectionCollider_, ref numPointsDetected))
					{
						// If no points were detected within the selected area, count it as dangerous.
						if (numPointsDetected == 0)
						{
							return false;
						}

						var container = GetContainingChild(node.Position);
						container.nodes_.Add(node);
						return true;
					}
					else
					{
						return false;
					}
				}

				public bool GetAnyDangerousPointCollisions(Collider collider, ref int pointsCollided)
				{
					// Automatically disqualify this quadrant if no points in it could be contained by the collider.
					if (Vector2D.Distance(center_, collider.Position) <= extents_.Length() + collider.MaxExtent)
					{
						if (children_.Count() == 0)
						{
							foreach (var point in points_)
							{
								if (collider.Contains(point.Position))
								{
									if (point.Dangerous)
									{
										return true;
									}

									pointsCollided++;
								}
							}
						}
						else
						{
							foreach (var child in children_)
							{
								if (child.GetAnyDangerousPointCollisions(collider, ref pointsCollided))
								{
									return true;
								}
							}
						}
					}

					return false;
				}

				/// <summary>
				/// Get all points within a collider.
				/// </summary>
				public void GetPointCollisions(ref List<Point2D> collisions, Collider collider)
				{
					// Automatically disqualify this quadrant if no points in it could be contained by the collider.
					if (Vector2D.Distance(center_, collider.Position) <= extents_.Length() + collider.MaxExtent)
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
								child.GetPointCollisions(ref collisions, collider);
							}
						}
					}
				}

				/// <summary>
				/// Purge invalid nodes within a collider.
				/// </summary>
				bool PurgeInvalidNodes(Collider collider)
				{
					bool foundInvalidNodes = false;

					// Automatically disqualify this quadrant if no points in it could be contained by the collider.
					if (Vector2D.Distance(center_, collider.Position) <= extents_.Length() + collider.MaxExtent)
					{
						if (children_.Count() == 0)
						{
							foreach (var node in nodes_)
							{
								if (collider.Contains(node.Position))
								{
									node.MarkForDeletion();
									foundInvalidNodes = true;
								}
							}
						}
						else
						{
							foreach (var child in children_)
							{
								foundInvalidNodes &= child.PurgeInvalidNodes(collider);
							}
						}
					}

					return foundInvalidNodes;
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
				}

				void Subdivide()
				{
					// Create new children
					for (int i = 0; i < 8; i++)
					{
						Vector2D newCenter = new Vector2D(center_.X, center_.Y);
						switch (i)
						{
							case 0:
								newCenter.X += extents_.X / 2;
								newCenter.Y += extents_.Y / 2;
								break;
							case 1:
								newCenter.X += extents_.X / 2;
								newCenter.Y -= extents_.Y / 2;
								break;
							case 2:
								newCenter.X -= extents_.X / 2;
								newCenter.Y += extents_.Y / 2;
								break;
							case 3:
								newCenter.X -= extents_.X / 2;
								newCenter.Y -= extents_.Y / 2;
								break;
						}

						children_.Add(new QuadTree(newCenter, extents_ / 2, depth_ + 1));
					}

					// Distribute points
					foreach (var point in points_)
					{
						foreach (var child in children_)
						{
							GetContainingChild(point.Position).AddPoint(point);
						}
					}
					points_.Clear();

					// Distribute movement nodes
					foreach (var node in nodes_)
					{
						foreach (var child in children_)
						{
							GetContainingChild(node.Position).AddNode(node);
						}
					}
					nodes_.Clear();
				}

				QuadTree GetContainingChild(Vector2D point)
				{
					if (children_.Count() > 0)
					{
						int childToAddTo = 0;

						if (point.X < center_.X)
						{
							childToAddTo += 2;
						}
						if (point.Y < center_.Y)
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

				public void GetPoints(ref List<Point2D> points)
				{
					foreach (var child in children_)
					{
						child.GetPoints(ref points);
					}

					foreach (var point in points_)
					{
						points.Add(point);
					}
				}

				public void GetDangerousPoints(ref List<Point2D> points)
				{
					foreach (var child in children_)
					{
						child.GetDangerousPoints(ref points);
					}

					foreach (var point in points_)
					{
						if (point.Dangerous)
						{
							points.Add(point);
						}
					}
				}

				public Collider InvalidNodeDetectionCollider
				{
					get
					{
						return invalidNodeDetectionCollider_;
					}
					set
					{
						invalidNodeDetectionCollider_ = value;
					}
				}

				public List<QuadTree> Children
				{
					get
					{
						return children_;
					}
				}

				public Vector2D Position
				{
					get
					{
						return center_;
					}
				}

				public Vector2D Extents
				{
					get
					{
						return extents_;
					}
				}
			}

			public MovementPlanner(Vector2D center, Vector2D extents)
			{
				points_ = new QuadTree(center, extents, 0);
				points_.InvalidNodeDetectionCollider = new RectangleCollider(center, new Vector2D(3, 3), 0);
			}

			/// <summary>
			/// Adds a new point. Purges any movement nodes that are violated by it and reports if any were.
			/// </summary>
			public bool AddPoint(Vector2D point, int id, bool dangerous, int timeout)
			{
				return points_.AddPoint(new Point2D(point, id, dangerous, timeout));
			}

			public List<Point2D> GetPoints()
			{
				var points = new List<Point2D>();

				points_.GetPoints(ref points);

				return points;
			}

			public List<Point2D> GetDangerousPoints()
			{
				var points = new List<Point2D>();

				points_.GetDangerousPoints(ref points);

				return points;
			}

			public void SetInvalidNodeDetectionCollider(Collider collider)
			{
				points_.InvalidNodeDetectionCollider = collider;
			}

			public List<Point2D> GetCollidingPoints(Collider collider)
			{
				var collidingPoints = new List<Point2D>();
				points_.GetPointCollisions(ref collidingPoints, collider);
				return collidingPoints;
			}

			public void CreateChildren(MovementNode node)
			{
				if (node.GeneratedChildren)
				{
					foreach (var child in node.Children)
					{
						CreateChildren(child);
					}
				}
				else
				{
					node.CreateChildNodes(ref points_);
				}
			}

			public QuadTree GetTree()
			{
				return points_;
			}
		}
	}
}
