using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using IngameScript;

namespace PointCloudViewer
{
	class MapDisplay
	{
		List<Ellipse> drawnPoints_;
		List<Ellipse> drawnNodes_;

		public MapDisplay()
		{
			MapManager = new Program.MapManager();
			Center2D = new VRageMath.Vector2D(0, 0);
			Zoom2D = 10;
			drawnPoints_ = new List<Ellipse>();
			drawnNodes_ = new List<Ellipse>();
		}

		public void ResetCenter2D()
		{
			Center2D = Get2D_Center(MapManager.MovementPlanner.GetPoints());
		}

		public void DrawMovementPlanner()
		{
			Draw2D_Points(MapManager.MovementPlanner.GetPoints());
		}

		public void DrawColliding2D_Points(Program.MovementPlanner.Collider collider)
		{
			Draw2D_Points(MapManager.MovementPlanner.GetCollidingPoints(collider));
		}

		VRageMath.Vector2D Get2D_Center(List<Program.MovementPlanner.Point2D> points)
		{
			if (points.Count > 0)
			{
				var lowSide = points.First().Position;
				var highSide = points.First().Position;

				foreach (var point in points)
				{
					if (point.Position.X < lowSide.X)
					{
						lowSide.X = point.Position.X;
					}
					else if (point.Position.X > highSide.X)
					{
						highSide.X = point.Position.X;
					}

					if (point.Position.Y < lowSide.Y)
					{
						lowSide.Y = point.Position.Y;
					}
					else if (point.Position.Y > highSide.Y)
					{
						highSide.Y = point.Position.Y;
					}
				}

				return (highSide - lowSide) / 2 + lowSide;
			}
			else
			{
				return new VRageMath.Vector2D(0, 0);
			}
		}

		public void Draw2D_Points(List<Program.MovementPlanner.Point2D> points)
		{
			foreach (var point in points)
			{
				if (!point.Dangerous)
				{
					Draw2D_Point(point);
				}
			}

			foreach (var point in points)
			{
				if (point.Dangerous)
				{
					Draw2D_Point(point);
				}
			}
		}
		
		public void Draw2D_Point(Program.MovementPlanner.Point2D point)
		{
			var flatPointBody = new Ellipse();
			flatPointBody.Width = 2;
			flatPointBody.Height = 2;
			if (point.Dangerous)
			{
				flatPointBody.Stroke = Brushes.Red;
				flatPointBody.Fill = Brushes.Red;
			}
			else
			{
				flatPointBody.Stroke = Brushes.Black;
				flatPointBody.Fill = Brushes.Black;
			}

			Canvas.SetLeft(flatPointBody, (point.Position.X - Center2D.X) * Zoom2D - flatPointBody.Width / 2);
			Canvas.SetTop(flatPointBody, (point.Position.Y - Center2D.Y) * Zoom2D - flatPointBody.Height / 2);

			View2D.Children.Add(flatPointBody);
			drawnPoints_.Add(flatPointBody);
		}

		public Ellipse DrawMovementNode(Program.MovementPlanner.MovementNode node)
		{
			var flatPointBody = new Ellipse();
			flatPointBody.Width = 20;
			flatPointBody.Height = 20;
			flatPointBody.Stroke = Brushes.Black;
			flatPointBody.Fill = Brushes.White;

			var centerX = (node.Position.X - Center2D.X) * Zoom2D - flatPointBody.Width / 2;
			var centerY = (node.Position.Y - Center2D.Y) * Zoom2D - flatPointBody.Height / 2;

			foreach (var child in node.Children)
			{
				var connectingLine = new Line();

				connectingLine.X1 = centerX;
				connectingLine.Y1 = centerY;

				connectingLine.X2 = (child.Position.X - Center2D.X) * Zoom2D - flatPointBody.Width / 2;
				connectingLine.Y2 = (child.Position.Y - Center2D.Y) * Zoom2D - flatPointBody.Height / 2;

				View2D.Children.Add(connectingLine);
			}

			Canvas.SetLeft(flatPointBody, centerX);
			Canvas.SetTop(flatPointBody, centerY);

			View2D.Children.Add(flatPointBody);
			drawnNodes_.Add(flatPointBody);

			return flatPointBody;
		}

		public void Recenter2D(VRageMath.Vector2D newCenter)
		{
			var offset = Center2D - newCenter;

			foreach (Shape shape in View2D.Children)
			{
				Canvas.SetLeft(shape, Canvas.GetLeft(shape) + offset.X * Zoom2D);
				Canvas.SetTop(shape, Canvas.GetTop(shape) + offset.Y * Zoom2D);
			}

			Center2D = newCenter;
		}

		VRageMath.Vector3D Get3D_Center(List<Program.TerrainMap.Point3D> points)
		{
			if (points.Count > 0)
			{
				var lowSide = new VRageMath.Vector3D(points.First().Position.X, points.First().Position.Y, points.First().Position.Z);
				var highSide = new VRageMath.Vector3D(points.First().Position.X, points.First().Position.Y, points.First().Position.Z);

				foreach (var point in points)
				{
					if (point.Position.X < lowSide.X)
					{
						lowSide.X = point.Position.X;
					}
					else if (point.Position.X > highSide.X)
					{
						highSide.X = point.Position.X;
					}

					if (point.Position.Y < lowSide.Y)
					{
						lowSide.Y = point.Position.Y;
					}
					else if (point.Position.Y > highSide.Y)
					{
						highSide.Y = point.Position.Y;
					}

					if (point.Position.Z < lowSide.Z)
					{
						lowSide.Z = point.Position.Z;
					}
					else if (point.Position.Z > highSide.Z)
					{
						highSide.Z = point.Position.Z;
					}
				}

				return (highSide - lowSide) / 2 + lowSide;
			}
			else
			{
				return new VRageMath.Vector3D();
			}
		}

		public void Clear2D()
		{
			View2D.Children.Clear();
			drawnPoints_.Clear();
			drawnNodes_.Clear();
		}

		public void ClearPoints()
		{
			foreach (var drawnPoint in drawnPoints_)
			{
				View2D.Children.Remove(drawnPoint);
			}
			drawnPoints_.Clear();
		}

		public void ClearNodes()
		{
			foreach (var drawnNode in drawnNodes_)
			{
				View2D.Children.Remove(drawnNode);
			}
			drawnNodes_.Clear();
		}

		public void Clear3D()
		{
			View3D.Children.Clear();
		}

		public HelixViewport3D View3D { get; set; }

		public Canvas View2D { get; set; }

		public Program.MapManager MapManager { get; set; }

		public VRageMath.Vector2D Center2D { get; set; }

		public double Zoom2D { get; set; }
	}
}
