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
using AForge.Video.DirectShow;
using AForge.Controls;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Threading;
using AForge.Video;
using System.Resources;
using System.Windows.Interop;

namespace CamShow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private VideoCaptureDevice webcam;
        private FilterInfoCollection videoDevices;
        private NewFrameEventHandler newFrameHandler;
        public bool invertVideo { get; set; } = false;

        public MainWindow()
        {
            InitializeComponent();
            newFrameHandler = new NewFrameEventHandler(web_NewFrame);
            invert.IsChecked  = Properties.Settings.Default.invert;
            webcam = new VideoCaptureDevice();
            this.DataContext = this;
            Loaded += MainWindow_Loaded;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Enumerate the available capture devices, and initialise the
            // webcam ComboBox.
            // This will trigger the selectionChanged handler, which will
            // query and fill out the resolutions ComboBox
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo f in videoDevices)
            {
                captureDevices.Items.Add(f.Name);

                // Select this cam if it was the cam we had selected last session
                if (f.MonikerString == Properties.Settings.Default.lastUsedWebcam)
                {
                    captureDevices.SelectedItem = f.Name;
                }
            }
            if (captureDevices.Items.Count > 0)
            {
                if (captureDevices.SelectedItem == null) {
                    // We're got at least one webcam, but no history of
                    // selected cam from a previous session, so select zeroth cam.
                    captureDevices.SelectedIndex = 0;
                }
            }
            else
            {
                // If no capture devices, make controls disabled, use messagebox to alert and suggest 
                // checking connections or plugging in webcam
                MessageWindow mw = new MessageWindow("Tsk.");
                mw.label.Content = "No camera found";
                mw.textBlock.Text = "No connected cameras were detected. Without a camera, this application can't do anything.\n\nPlease check your camera is plugged in, and has the appropriate drivers installed. Then restart this application.\n\nSorry about that.";
                mw.ShowDialog();
                Close();
            }
        }

        private void captureDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // re-query selected capture device to discover resolutions
            // TODO - sort resolutions according to width (first order) height (second order)
            if (captureDevices.SelectedItem != null) {
                stopWebcam(webcam);
                String moniker = videoDevices[captureDevices.SelectedIndex].MonikerString;
                Properties.Settings.Default.lastUsedWebcam = moniker;
                webcam = new VideoCaptureDevice(moniker);
                webcamResolutions.Items.Clear();
                VideoCapabilities[] videoCapabilities = webcam.VideoCapabilities;
                foreach (VideoCapabilities capability in videoCapabilities) {
                    VideoCapabilitiesDisplayable capabilityDisplayable = new VideoCapabilitiesDisplayable(capability);
                    if (!webcamResolutions.Items.Contains(capabilityDisplayable))
                    {
                        webcamResolutions.Items.Add(capabilityDisplayable);
                    }
                }
                if (webcamResolutions.Items.Count > 0) { webcamResolutions.SelectedIndex = 0; }

                startWebcam(webcam);
            }
        }

        private void webcamResolutions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: If we change cameras, and the same resolution exists, we should retain it?
            if (webcamResolutions.Items.Count > 0)
            {
                stopWebcam(webcam);
                webcam.VideoResolution = ((VideoCapabilitiesDisplayable)webcamResolutions.SelectedItem).Capability;
                startWebcam(webcam);
                // Do not resize to larger than the available screen
                // Need to account for window decorations
                // Strategy - a) allow for requested size, then resize if too big, or;
                //          - b) set maximum of 90% of screen resolution?

                // Let's try (a) for now.
                //Height = webcam.VideoResolution.FrameSize.Height;
                //Width = webcam.VideoResolution.FrameSize.Width;

                // Free up Window to be resized by the videoImage element
                SizeToContent = SizeToContent.WidthAndHeight;
                Height = Double.NaN;
                Width = Double.NaN;

                // Set size according to what pixels we ought to be receiving from the cam
                videoImage.Height = webcam.VideoResolution.FrameSize.Height;
                videoImage.Width = webcam.VideoResolution.FrameSize.Width;

                // Let's force a redraw so we know the Window will have been
                // resized by the videoImage element.
                UpdateLayout();

                // Having resized, set Window to be that size (which user can then
                // override simply by resizing with pointer). This ensures the new
                // Height and Width take account of window decorations.
                Height = ActualHeight;
                Width = ActualWidth;

                // Prevent videoImage from asserting its size
                SizeToContent = SizeToContent.Manual;

                // Remove Height and Width restrictions on videoImage, so it will
                // follow the Stretch rule to fill the Window (and retain aspect ratio)
                videoImage.Height = Double.NaN;
                videoImage.Width = Double.NaN;

                if (ActualHeight > SystemParameters.WorkArea.Height ||
                    ActualWidth > SystemParameters.WorkArea.Width)
                {
                    Top = 0;
                    Left = 0;
                    Height = SystemParameters.WorkArea.Height;
                    Width = SystemParameters.WorkArea.Width;
                }
            }
        }

        private void web_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                BitmapImage bi;
                using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    bi = new BitmapImage();
                    bi.BeginInit();
                    bi.Rotation = invertVideo == true ? Rotation.Rotate180 : Rotation.Rotate0;
                    MemoryStream ms = new MemoryStream();
                    bitmap.Save(ms, ImageFormat.Bmp);
                    ms.Seek(0, SeekOrigin.Begin);
                    bi.StreamSource = ms;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                }
                bi.Freeze();
                Dispatcher.BeginInvoke(new ThreadStart(delegate { videoImage.Source = bi; }));


            }
            catch (Exception ex)
            {
                Console.WriteLine("Error updating frame: {0}", ex.Message);
            }
        }

        private void stopWebcam(VideoCaptureDevice webcam)
        {
            if (webcam != null) {
                webcam.NewFrame -= newFrameHandler;
                webcam.Stop();
             }
        }

        private void startWebcam(VideoCaptureDevice webcam)
        {
            if (webcam != null)
            {
                webcam.NewFrame += newFrameHandler;
                webcam.Start();
            }
        }

        private void enterFullscreen()
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.NoResize;
            fullscreenImage.Source = (ImageSource)FindResource("exitFullscreen");
        }

        private void exitFullscreen()
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            fullscreenImage.Source = (ImageSource)FindResource("enterFullscreen");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.invert = invert.IsChecked == true;
            Properties.Settings.Default.Save();
            stopWebcam(webcam);
        }

        private void fullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (WindowStyle == WindowStyle.None)
            {
                exitFullscreen();
            }
            else
            {
                enterFullscreen();
            }
        }

        private void togglePlay()
        {
            if (webcam.IsRunning)
            {
                playPauseImage.Source = (ImageSource)FindResource("play");
                stopWebcam(webcam);
            }
            else
            {
                playPauseImage.Source = (ImageSource)FindResource("pause");
                startWebcam(webcam);
            }
        }

        // Keyboard shortcuts

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && WindowStyle == WindowStyle.None)
            {
                exitFullscreen();
            }

            else if (e.Key == Key.S) // Webcam Settings - adjust contrast, brightness etc.
            {
                showWebcamProperties();
                
            }

            else if (e.Key == Key.Space) // Play/Pause
            {
                togglePlay();
            }
        }

        private void playPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            togglePlay();
        }

        private void showWebcamProperties()
        {
            //CameraPropertiesWindow cpw = new CameraPropertiesWindow(webcam);
            //cpw.Title = (String)captureDevices.SelectedItem;
            //cpw.ShowDialog();
            //Close();
            webcam.DisplayPropertyPage(new WindowInteropHelper(this).Handle);
        }

        private void optionsBtn_Click(object sender, RoutedEventArgs e)
        {
            showWebcamProperties();
        }
    }
}
