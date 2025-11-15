using RoboDk.API;
using RoboDk.API.Model;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace RoboDk
{
    public partial class MainWindow : Window
    {
        private RoboDK _rdk;
        private IItem _robot;

        private bool _draggingTcp;
        private Point _mouseStart;
        private Point3D _sphereStart;

        private const double MaxStepMm = 10.0;    
        private const double PixelsToMm = 2.0;    
        private readonly DispatcherTimer _pollTimer;

        public MainWindow()
        {
            InitializeComponent();

            InitRoboDK();
            InitTcpSphereFromRobot();

            InitZSliderFromRobot();

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _pollTimer.Tick += PollTimerOnTick;
            _pollTimer.Start();
        }

        private void InitRoboDK()
        {
            try
            {
                _rdk = new RoboDK();
                _rdk.SetRunMode(RunMode.Simulate);

                _robot = _rdk.GetItemByName("Fanuc ARC Mate 100iD", ItemType.Robot);

                if (!_robot.Valid())
                {
                    StatusText.Text = "Robot not found in RoboDK station.";
                    StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                }
                else
                {
                    StatusText.Text = "Connected to RoboDK.";
                    StatusText.Foreground = System.Windows.Media.Brushes.LawnGreen;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error connecting to RoboDK: " + ex.Message;
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private void InitTcpSphereFromRobot()
        {
            if (_robot == null || !_robot.Valid())
                return;

            Mat pose = _robot.Pose();
            double[] p = pose.ToXYZRPW();

            TcpSphere.Center = new Point3D(p[0], p[1], p[2]);
        }

        private void InitZSliderFromRobot()
        {
            if (_robot == null || !_robot.Valid())
                return;

            Mat pose = _robot.Pose();
            double[] p = pose.ToXYZRPW();
            double z = p[2];

            if (z < ZSlider.Minimum) z = ZSlider.Minimum;
            if (z > ZSlider.Maximum) z = ZSlider.Maximum;

            ZSlider.Value = z;
            ZSliderLabel.Text = $"Z: {z:F1} mm";
        }

        private void PollTimerOnTick(object sender, EventArgs e)
        {
            if (_robot == null || !_robot.Valid())
                return;

            try
            {
                Mat pose = _robot.Pose();
                double[] p = pose.ToXYZRPW();

                TcpXText.Text = $"X: {p[0]:F1} mm";
                TcpYText.Text = $"Y: {p[1]:F1} mm";
                TcpZText.Text = $"Z: {p[2]:F1} mm";
            }
            catch
            {
            }
        }

        private void View3D_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            _mouseStart = e.GetPosition(View3D);
            _sphereStart = TcpSphere.Center;
            _draggingTcp = true;
            View3D.CaptureMouse();
        }

        private void View3D_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_draggingTcp)
                return;

            Point pos = e.GetPosition(View3D);
            MoveSphereAndRobotOnDrag(pos);
        }

        private void View3D_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingTcp)
            {
                _draggingTcp = false;
                View3D.ReleaseMouseCapture();
            }
        }

        private void MoveSphereAndRobotOnDrag(Point mousePos)
        {
            if (_robot == null || !_robot.Valid())
                return;

            double dxPixels = mousePos.X - _mouseStart.X;
            double dyPixels = mousePos.Y - _mouseStart.Y;

            double dxMm = dxPixels * PixelsToMm;
            double dyMm = -dyPixels * PixelsToMm; 

            double zMm = ZSlider.Value;

            Point3D target = new Point3D(
                _sphereStart.X + dxMm,
                _sphereStart.Y + dyMm,
                zMm);

            Point3D currentCenter = TcpSphere.Center;
            double sx = target.X - currentCenter.X;
            double sy = target.Y - currentCenter.Y;
            double sz = target.Z - currentCenter.Z;
            double dist = Math.Sqrt(sx * sx + sy * sy + sz * sz);

            if (dist < 1.0)
                return;

            if (dist > MaxStepMm)
            {
                double scale = MaxStepMm / dist;
                sx *= scale;
                sy *= scale;
                sz *= scale;
            }

            Point3D stepped = new Point3D(
                currentCenter.X + sx,
                currentCenter.Y + sy,
                currentCenter.Z + sz);

            TcpSphere.Center = stepped;

            MoveRobotToPoint(stepped);
        }

        private void MoveRobotToPoint(Point3D p3d)
        {
            if (_robot == null || !_robot.Valid())
                return;

            Mat currentPose = _robot.Pose();
            double[] cur = currentPose.ToXYZRPW();

            double curX = cur[0];
            double curY = cur[1];
            double curZ = cur[2];

            double dx = p3d.X - curX;
            double dy = p3d.Y - curY;
            double dz = p3d.Z - curZ;

            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            //if (dist < 1.0)
            //    return;

            if (dist > MaxStepMm)
            {
                double scale = MaxStepMm / dist;
                dx *= scale;
                dy *= scale;
                dz *= scale;
            }

            double newX = curX + dx;
            double newY = curY + dy;
            double newZ = curZ + dz;

            double[] xyzrxyz =
            {
                newX, newY, newZ,
                cur[3], cur[4], cur[5] 
            };

            Mat targetPose = Mat.FromXYZRPW(xyzrxyz);

            try
            {
                _robot.MoveL(targetPose);
            }
            catch(Exception d)
            {
            }

        }

        private void ZSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Point3D c = TcpSphere.Center;
            TcpSphere.Center = new Point3D(c.X, c.Y, ZSlider.Value);
        }

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            InitTcpSphereFromRobot();
            InitZSliderFromRobot();
        }
    }
}