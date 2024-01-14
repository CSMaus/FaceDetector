﻿using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using Emgu.CV;
using Emgu.CV.Structure;
using System.IO;
using Emgu.CV.CvEnum;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ObjectDetector.ViewModels
{
    public class CameraViewModel : INotifyPropertyChanged
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        Stopwatch stopwatch = new Stopwatch();

        private VideoCaptureDevice _videoSource;
        private BitmapSource _currentFrame;
        public BitmapSource CurrentFrame
        {
            get => _currentFrame;
            set
            {
                _currentFrame = value;
                OnPropertyChanged(nameof(CurrentFrame));
            }
        }

        public bool CameraOn { get; set; } = true;
        public ICommand TurnOnOff { get; set; }
        private DateTime _lastProcessedTime = DateTime.MinValue;


        public ObservableCollection<string> VideoDevices { get; set; } = new ObservableCollection<string>();
        public int SelectedDeviceIndex
        {
            get => _selectedDeviceIndex;
            set
            {
                _selectedDeviceIndex = value;
                OnPropertyChanged(nameof(SelectedDeviceIndex));
                SetVideoDeviceByIndex(value);
            }
        }
        private int _selectedDeviceIndex;

        // https://stackoverflow.com/questions/44406587/explanation-of-haarcascade-xml-files-in-opencv
        // check the link. there are also some links there for face recognotion tasks
        // https://itecnote.com/tecnote/c-emgucv-3-1-face-detection/
        readonly CascadeClassifier faceCascade = new CascadeClassifier("Resources/haarcascade_frontalface_default.xml");

        private double _doubleUpdateFreq;
        public double DoubleUpdateFreq
        {
            get { return _doubleUpdateFreq; }
            set
            {
                if (_doubleUpdateFreq != value)
                {
                    _doubleUpdateFreq = value;
                    OnPropertyChanged(nameof(DoubleUpdateFreq));
                }
            }
        }

        private string _detectionUpdateFreqText;
        public string DetectionUpdateFreqText
        {
            get { return _detectionUpdateFreqText; }
            set
            {
                if (_detectionUpdateFreqText != value)
                {
                    _detectionUpdateFreqText = value;
                    OnPropertyChanged(nameof(DetectionUpdateFreqText));

                    var normalizedValue = _detectionUpdateFreqText.Replace('.', ',');

                    if (double.TryParse(_detectionUpdateFreqText, out double parsedValue))
                    {
                        DoubleUpdateFreq = parsedValue;
                    }
                    else if(double.TryParse(normalizedValue, out parsedValue))
                    {
                        DoubleUpdateFreq = parsedValue;
                    }

                }
            }
        }

        public CameraViewModel()
        {
            TurnOnOff = new RelayCommand(TurnCameraOnOff);

            var videoDevicesCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in videoDevicesCollection)
            {
                VideoDevices.Add(device.Name);
            }

            if (VideoDevices.Count == 0)
                throw new ApplicationException("No camera found!");

        }
        private void TurnCameraOnOff()
        {
            if (_videoSource != null)
            {
                CameraOn = !CameraOn;
                if (CameraOn)
                {
                    try
                    {
                        _videoSource.NewFrame += VideoSource_NewFrame;
                        _videoSource.Start();
                    }
                    catch 
                    {
                        MessageBox.Show($"Error starting video source: No camera selected");
                    }
                }
                else
                {
                    try
                    {
                        _videoSource.NewFrame -= VideoSource_NewFrame;
                        _videoSource.Stop();
                        CurrentFrame = null;
                    }
                    catch 
                    {
                        MessageBox.Show($"Error in video source: No camera selected");
                    }
                }
            }
            
        }
        private Rectangle[] Faces { get; set; }
        private bool printThicks = true;

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    

                    if (CameraOn)
                    {
                        IntPtr hBitmap = eventArgs.Frame.GetHbitmap();
                        try
                        {
                            using (Bitmap srcBitmap = Bitmap.FromHbitmap(hBitmap))
                            {
                                if (printThicks) stopwatch.Start();
                                Image<Bgr, byte> imageCV = srcBitmap.ToImage<Bgr, byte>();

                                // Console.WriteLine($"Update face detection value: {DoubleUpdateFreq}");
                                if ((DateTime.Now - _lastProcessedTime).TotalSeconds < DoubleUpdateFreq)
                                {

                                    if (Faces != null)
                                    {
                                        foreach (Rectangle face in Faces)
                                        {
                                            imageCV.Draw(face, new Bgr(System.Drawing.Color.Red), 2);
                                        }
                                    }
                                    CurrentFrame = ConvertToBitmapSource(imageCV.ToBitmap());
                                    return;
                                }

                                UMat imageCV_UMat = imageCV.ToUMat();
                                UMat grayImage = new UMat();
                                CvInvoke.CvtColor(imageCV_UMat, grayImage, ColorConversion.Bgr2Gray);
                                

                                Faces = faceCascade.DetectMultiScale(grayImage, 1.1, 3,
                                    new System.Drawing.Size(imageCV.Width / 13, imageCV.Height / 13),
                                    new System.Drawing.Size((int)((double)imageCV.Width / 1.05), (int)((double)imageCV.Width / 1.05))); // 223437 ticks

                                foreach (Rectangle face in Faces)
                                {
                                    imageCV.Draw(face, new Bgr(System.Drawing.Color.Red), 2);
                                }

                                CurrentFrame = ConvertToBitmapSource(imageCV.ToBitmap());
                                if (printThicks) stopwatch.Stop();
                                if (printThicks) Console.WriteLine("RunTime: " + stopwatch.ElapsedTicks + " ticks"); printThicks = false;

                                _lastProcessedTime = DateTime.Now;
                            }
                        }
                        finally
                        {
                            DeleteObject(hBitmap);
                        }
                    }

                    
                });
            }
            else
            {
                Environment.Exit(0);
            }
        }


        private void SetVideoDeviceByIndex(int index)
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.NewFrame -= VideoSource_NewFrame;
                _videoSource.Stop(); 
                CurrentFrame = null;
            }

            var videoDevicesCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            _videoSource = new VideoCaptureDevice(videoDevicesCollection[index].MonikerString);
            
            _videoSource.NewFrame += VideoSource_NewFrame;
            _videoSource.Start();
        }

        public byte[,,] ConvertImageToByteArray(Bitmap image)
        {
            int width = image.Width;
            int height = image.Height;
            byte[,,] imageData = new byte[height, width, 3];

            BitmapData bmpData = image.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, image.PixelFormat);

            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * height;
            byte[] rgbValues = new byte[bytes];


            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            int stride = bmpData.Stride;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int idx = (y * stride) + (x * 3) + channel;
                        imageData[y, x, channel] = rgbValues[idx];
                    }
                }
            }

            image.UnlockBits(bmpData);

            return imageData;
        }
        public static BitmapSource ConvertToBitmapSource(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            var bitmapSource = BitmapSource.Create(bitmapData.Width, bitmapData.Height, bitmap.HorizontalResolution, bitmap.VerticalResolution, PixelFormats.Bgr24, null, bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        // let this shit be, but it doesn't work
        public void Cleanup()
        {
            if (_videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.WaitForStop();
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute)
        {
            _execute = execute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}
