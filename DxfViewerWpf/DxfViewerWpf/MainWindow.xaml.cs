using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using netDxf;
using netDxf.Tables;
using System.Linq;

using WpfShapes = System.Windows.Shapes;
using DxfEntities = netDxf.Entities;

namespace DxfViewerWpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenDxfFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DXF Files (*.dxf)|*.dxf|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                LoadDxf(filePath);
            }
        }

        private void LoadDxf(string filePath)
        {
            try
            {
                DrawingCanvas.Children.Clear();

                DxfDocument doc = DxfDocument.Load(filePath);
                if (doc == null)
                {
                    MessageBox.Show("Không thể tải file DXF.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Tính toán bounding box
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                bool hasEntities = false;
                foreach (var entity in doc.Entities.All)
                {
                    hasEntities = true;
                    GetEntityBounds(entity, ref minX, ref minY, ref maxX, ref maxY);
                }

                if (!hasEntities || minX == double.MaxValue)
                {
                    MessageBox.Show("File DXF không có đối tượng để hiển thị.", "Cảnh báo",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                double dxfWidth = maxX - minX;
                double dxfHeight = maxY - minY;

                if (dxfWidth == 0 || dxfHeight == 0)
                {
                    MessageBox.Show("Không thể xác định kích thước bản vẽ.", "Cảnh báo",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Thiết lập canvas size
                double canvasWidth = 1000;
                double canvasHeight = 800;
                DrawingCanvas.Width = canvasWidth;
                DrawingCanvas.Height = canvasHeight;

                // Tính toán scale để fit vào canvas (với padding 10%)
                double scaleX = canvasWidth * 0.9 / dxfWidth;
                double scaleY = canvasHeight * 0.9 / dxfHeight;
                double scale = Math.Min(scaleX, scaleY);

                // Tính toán offset để center drawing
                double offsetX = (canvasWidth - dxfWidth * scale) / 2 - minX * scale;
                double offsetY = (canvasHeight - dxfHeight * scale) / 2 + maxY * scale;

                TransformGroup transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform(scale, -scale)); // Flip Y axis
                transformGroup.Children.Add(new TranslateTransform(offsetX, offsetY));

                // Vẽ các entity
                foreach (var entity in doc.Entities.All)
                {
                    WpfShapes.Shape wpfShape = ConvertToWpfShape(entity, doc);
                    if (wpfShape != null)
                    {
                        var color = GetEntityColor(entity, doc);
                        wpfShape.Stroke = new SolidColorBrush(color);
                        wpfShape.StrokeThickness = 1.0 / scale; // Scale-independent thickness
                        wpfShape.RenderTransform = transformGroup;
                        DrawingCanvas.Children.Add(wpfShape);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đọc file DXF: {ex.Message}\n\nStack trace: {ex.StackTrace}",
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GetEntityBounds(DxfEntities.EntityObject entity, ref double minX, ref double minY,
            ref double maxX, ref double maxY)
        {
            switch (entity.Type)
            {
                case DxfEntities.EntityType.Line:
                    var line = (DxfEntities.Line)entity;
                    UpdateBounds(line.StartPoint.X, line.StartPoint.Y, ref minX, ref minY, ref maxX, ref maxY);
                    UpdateBounds(line.EndPoint.X, line.EndPoint.Y, ref minX, ref minY, ref maxX, ref maxY);
                    break;

                case DxfEntities.EntityType.Circle:
                    var circle = (DxfEntities.Circle)entity;
                    UpdateBounds(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius,
                        ref minX, ref minY, ref maxX, ref maxY);
                    UpdateBounds(circle.Center.X + circle.Radius, circle.Center.Y + circle.Radius,
                        ref minX, ref minY, ref maxX, ref maxY);
                    break;

                case DxfEntities.EntityType.Arc:
                    var arc = (DxfEntities.Arc)entity;
                    UpdateBounds(arc.Center.X - arc.Radius, arc.Center.Y - arc.Radius,
                        ref minX, ref minY, ref maxX, ref maxY);
                    UpdateBounds(arc.Center.X + arc.Radius, arc.Center.Y + arc.Radius,
                        ref minX, ref minY, ref maxX, ref maxY);
                    break;

                case DxfEntities.EntityType.Polyline2D:
                    var polyline = (DxfEntities.Polyline2D)entity;
                    foreach (var vertex in polyline.Vertexes)
                    {
                        UpdateBounds(vertex.Position.X, vertex.Position.Y, ref minX, ref minY, ref maxX, ref maxY);
                    }
                    break;

                    //case DxfEntities.EntityType.LwPolyline:
                    //    var lwPolyline = (DxfEntities.LwPolyline)entity;
                    //    foreach (var vertex in lwPolyline.Vertexes)
                    //    {
                    //        UpdateBounds(vertex.Position.X, vertex.Position.Y, ref minX, ref minY, ref maxX, ref maxY);
                    //    }
                    //    break;
            }
        }

        private void UpdateBounds(double x, double y, ref double minX, ref double minY,
            ref double maxX, ref double maxY)
        {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        private WpfShapes.Shape ConvertToWpfShape(DxfEntities.EntityObject entity, DxfDocument doc)
        {
            switch (entity.Type)
            {
                case DxfEntities.EntityType.Line:
                    var line = (DxfEntities.Line)entity;
                    return new WpfShapes.Line
                    {
                        X1 = line.StartPoint.X,
                        Y1 = line.StartPoint.Y,
                        X2 = line.EndPoint.X,
                        Y2 = line.EndPoint.Y
                    };

                case DxfEntities.EntityType.Circle:
                    var circle = (DxfEntities.Circle)entity;
                    return new WpfShapes.Ellipse
                    {
                        Width = circle.Radius * 2,
                        Height = circle.Radius * 2,
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        RenderTransform = new TranslateTransform(
                            circle.Center.X - circle.Radius,
                            circle.Center.Y - circle.Radius
                        )
                    };

                case DxfEntities.EntityType.Arc:
                    var arc = (DxfEntities.Arc)entity;
                    return CreateArcPath(arc);

                case DxfEntities.EntityType.Polyline2D:
                    var polyline = (DxfEntities.Polyline2D)entity;
                    var points = new PointCollection();
                    foreach (var vertex in polyline.Vertexes)
                    {
                        points.Add(new Point(vertex.Position.X, vertex.Position.Y));
                    }
                    return new WpfShapes.Polyline { Points = points };

                //case DxfEntities.EntityType.LwPolyline:
                //    var lwPolyline = (DxfEntities.LwPolyline)entity;
                //    var lwPoints = new PointCollection();
                //    foreach (var vertex in lwPolyline.Vertexes)
                //    {
                //        lwPoints.Add(new Point(vertex.Position.X, vertex.Position.Y));
                //    }
                //    return new WpfShapes.Polyline { Points = lwPoints };

                default:
                    return null;
            }
        }

        private WpfShapes.Path CreateArcPath(DxfEntities.Arc arc)
        {
            double startAngleRad = arc.StartAngle * Math.PI / 180.0;
            double endAngleRad = arc.EndAngle * Math.PI / 180.0;

            Point startPoint = new Point(
                arc.Center.X + arc.Radius * Math.Cos(startAngleRad),
                arc.Center.Y + arc.Radius * Math.Sin(startAngleRad)
            );
            Point endPoint = new Point(
                arc.Center.X + arc.Radius * Math.Cos(endAngleRad),
                arc.Center.Y + arc.Radius * Math.Sin(endAngleRad)
            );

            double sweepAngle = arc.EndAngle - arc.StartAngle;
            if (sweepAngle < 0) sweepAngle += 360;
            bool isLargeArc = sweepAngle > 180;

            PathFigure pathFigure = new PathFigure { StartPoint = startPoint, IsClosed = false };
            ArcSegment arcSegment = new ArcSegment
            {
                Point = endPoint,
                Size = new Size(arc.Radius, arc.Radius),
                IsLargeArc = isLargeArc,
                SweepDirection = SweepDirection.Counterclockwise,
                RotationAngle = 0
            };

            pathFigure.Segments.Add(arcSegment);
            PathGeometry pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(pathFigure);

            return new WpfShapes.Path { Data = pathGeometry };
        }

        private Color GetEntityColor(DxfEntities.EntityObject entity, DxfDocument doc)
        {
            AciColor aciColor = entity.Color;

            // Nếu màu là ByLayer, lấy màu từ layer
            if (aciColor.IsByLayer)
            {
                Layer layer = doc.Layers[entity.Layer.Name];
                if (layer != null)
                {
                    aciColor = layer.Color;
                }
                else
                {
                    return Colors.White; // Màu mặc định
                }
            }

            // Nếu dùng TrueColor
            if (aciColor.UseTrueColor)
            {
                return Color.FromRgb(aciColor.R, aciColor.G, aciColor.B);
            }

            // Chuyển đổi ACI color sang RGB
            int rgb = AciColor.ToTrueColor(aciColor);
            return Color.FromRgb((byte)(rgb >> 16), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        }
    }
}