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
		public MainWindow()
		{
			InitializeComponent();

			var upDirection = new VRageMath.Vector3D(0, 1, 0);

			var terrainMap = new Program.TerrainMap(new VRageMath.Vector3D(0, 0, 0),
												new VRageMath.Vector3D(100, 100, 100));
			terrainMap.SetUpDirection(upDirection);

			// Create the movement planner
			var movementPlanner = new Program.MovementPlanner(new VRageMath.Vector2D(0, 0), new VRageMath.Vector2D(100, 100));

			// Create the ground plane
			var groundPlane = new VRageMath.PlaneD(upDirection, 0);

			var pointsToAdd = new List<VRageMath.Vector3D>();
			pointsToAdd.Add(new VRageMath.Vector3D(0, 0, 0));
			pointsToAdd.Add(new VRageMath.Vector3D(18, 12, 5));
			pointsToAdd.Add(new VRageMath.Vector3D(-6, 16, -4));
			pointsToAdd.Add(new VRageMath.Vector3D(12, 12, 15));
			pointsToAdd.Add(new VRageMath.Vector3D(3, 7, 4));
			pointsToAdd.Add(new VRageMath.Vector3D(2, -9, -7));
			pointsToAdd.Add(new VRageMath.Vector3D(8, 7, 3));
			pointsToAdd.Add(new VRageMath.Vector3D(14, 3, 9));
			pointsToAdd.Add(new VRageMath.Vector3D(-14, -18, 25));
			pointsToAdd.Add(new VRageMath.Vector3D(11, 15, -4));
			pointsToAdd.Add(new VRageMath.Vector3D(-20, 1, 22));

			foreach (var pointToAdd in pointsToAdd)
			{
				var dangerousPoints = terrainMap.AddPoint(pointToAdd, 1);

				var referencePoint = new VRageMath.Vector3D(pointToAdd);
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
				}
			}

			/*terrainMap.AddPoint(new VRageMath.Vector3D(0, 5, 5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(0, 5, -5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(0, -5, 5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(0, -5, -5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(5, 0, 5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(5, 0, -5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(-5, 0, 5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(-5, 0, -5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(5, 5, 0), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(5, -5, 0), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(-5, 5, 0), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(-5, -5, 0), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(5, 5, 5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(5, 5, -5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(5, -5, 5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(5, 5, -5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(5, -5, -5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(-5, 5, 5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(-5, 5, -5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(-5, -5, 5), 100);
			terrainMap.AddPoint(new VRageMath.Vector3D(-5, -5, -5), 100);*/

			var points = terrainMap.GetPoints();

			var centerPoint = new VRageMath.Vector3D(0, 0, 0);

			var upLine = new HelixToolkit.Wpf.LinesVisual3D();
			upLine.Points.Add(new Point3D(0, 0, 0));
			upLine.Points.Add(new Point3D(0, 0, 5));
			Viewport3D.Children.Add(upLine);

			var centerPointBody = new HelixToolkit.Wpf.SphereVisual3D();
			centerPointBody.Center = new Point3D(0, 0, 0);
			centerPointBody.Radius = 0.5;
			centerPointBody.Material = new DiffuseMaterial();
			Viewport3D.Children.Add(centerPointBody);

			foreach (var point in points)
			{
				var pointBody = new HelixToolkit.Wpf.SphereVisual3D();
				pointBody.Center = new Point3D(point.X, point.Z, point.Y);
				pointBody.Radius = 0.5;

				Viewport3D.Children.Add(pointBody);
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
	}
}
