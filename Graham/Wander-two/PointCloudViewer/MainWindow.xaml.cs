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
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		List<VRageMath.Vector3D> pointsToAdd;

		public MainWindow()
		{
			InitializeComponent();

			var upDirection = new VRageMath.Vector3D(0, 1, 0);

			var terrainMap = new Program.TerrainMap(new VRageMath.Vector3D(0, 0, 0),
												new VRageMath.Vector3D(500, 500, 500));
			terrainMap.SetUpDirection(upDirection);

			// Create the movement planner
			var movementPlanner = new Program.MovementPlanner(new VRageMath.Vector2D(0, 0), new VRageMath.Vector2D(100, 100));

			// Create the ground plane
			var groundPlane = new VRageMath.PlaneD(upDirection, 0);

			pointsToAdd = new List<VRageMath.Vector3D>();

			var pointOffset = new VRageMath.Vector3D(pointsToAdd.First().X, pointsToAdd.First().Y, pointsToAdd.First().Z);

			foreach (var referencedPointFromList in pointsToAdd)
			{
				var pointToAdd = referencedPointFromList - pointOffset;

				var dangerousPoints = terrainMap.AddPoint(pointToAdd, 1);

				/*var referencePoint = new VRageMath.Vector3D(pointToAdd);
				var projectedPoint = groundPlane.ProjectPoint(ref referencePoint);
				movementPlanner.AddPoint(new VRageMath.Vector2D(projectedPoint.X, projectedPoint.Z), dangerousPoints.Count() > 0, 1);

				foreach (var dangerousPoint in dangerousPoints)
				{
					// Add to visual display
					var pointLine = new HelixToolkit.Wpf.LinesVisual3D();
					pointLine.Points.Add(new Point3D(pointToAdd.X, pointToAdd.Z, pointToAdd.Y));
					pointLine.Points.Add(new Point3D(dangerousPoint.Position.X, dangerousPoint.Position.Z, dangerousPoint.Position.Y));
					Viewport3D.Children.Add(pointLine);

					// Add to movement planner
					referencePoint = new VRageMath.Vector3D(dangerousPoint.Position);
					projectedPoint = groundPlane.ProjectPoint(ref referencePoint);
					movementPlanner.AddPoint(new VRageMath.Vector2D(projectedPoint.X, projectedPoint.Z), true, dangerousPoint.Timeout);
				}*/
			}

			var points = terrainMap.GetPoints();

			var centerPoint = new VRageMath.Vector3D(0, 0, 0);

			/*var upLine = new HelixToolkit.Wpf.LinesVisual3D();
			upLine.Points.Add(new Point3D(0, 0, 0));
			upLine.Points.Add(new Point3D(0, 0, 5));
			Viewport3D.Children.Add(upLine);

			var centerPointBody = new HelixToolkit.Wpf.SphereVisual3D();
			centerPointBody.Center = new Point3D(0, 0, 0);
			centerPointBody.Radius = 0.1;
			centerPointBody.Material = new DiffuseMaterial();
			Viewport3D.Children.Add(centerPointBody);*/

			var connectPoints = new HashSet<Tuple<int, int>>();

			var collider = new Program.TerrainMap.SphereCollider(new VRageMath.Vector3D(0, 0, 0), 4);

			for (int i = 0; i < points.Count(); i++)
			{
				var point = points[i];

				collider.Position = point;
				var nearbyPoints = terrainMap.GetCollisions(collider);

				foreach (var nearPoint in nearbyPoints)
				{
					int otherIndex = points.FindIndex(
						delegate (VRageMath.Vector3D v)
						{
							return (nearPoint.Position.X == v.X && nearPoint.Position.Y == v.Y && nearPoint.Position.Z == v.Z);
						});

					if (i != otherIndex)
					{
						Tuple<int, int> newTuple;

						if (i < otherIndex)
						{
							newTuple = new Tuple<int, int>(i, otherIndex);
						}
						else
						{
							newTuple = new Tuple<int, int>(otherIndex, i);
						}

						connectPoints.Add(newTuple);
					}
				}
			}

			foreach (var link in connectPoints)
			{
				var p1 = points[link.Item1];
				var p2 = points[link.Item2];

				// Add to visual display
				var pointLine = new HelixToolkit.Wpf.LinesVisual3D();
				pointLine.Points.Add(new Point3D(p1.X, p1.Y, p1.Z));
				pointLine.Points.Add(new Point3D(p2.X, p2.Y, p2.Z));
				Viewport3D.Children.Add(pointLine);

				/*var pointBody = new HelixToolkit.Wpf.SphereVisual3D();
				pointBody.Center = new Point3D(point.X, point.Z, point.Y);
				pointBody.Radius = 0.5;

				Viewport3D.Children.Add(pointBody);*/
			}

			var flatPoints = movementPlanner.GetPoints();
			flatPoints.Add(new VRageMath.Vector2D(0, 0));

			foreach (var point in flatPoints)
			{
				var flatPointBody = new Ellipse();
				flatPointBody.Width = 10;
				flatPointBody.Height = 10;
				flatPointBody.Stroke = Brushes.Black;
				flatPointBody.Fill = Brushes.Black;

				Canvas.SetLeft(flatPointBody, point.X * 5);
				Canvas.SetTop(flatPointBody, point.Y * 5);
				
				Canvas2D.Children.Add(flatPointBody);
			}
		}

		void a(double x, double y, double z)
		{
			pointsToAdd.Add(new VRageMath.Vector3D(x, y, z);
		}
	}
}
