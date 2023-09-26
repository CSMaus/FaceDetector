using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Numerics.Tensors;
using System.Collections.Generic;
using OnnxTensor = Microsoft.ML.OnnxRuntime.Tensors.Tensor<float>;

namespace ObjectDetector.ViewModels
{
    public class FaceDetectorViewModel : INotifyPropertyChanged
    {
        private VideoCaptureDevice _videoSource;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private BitmapSource _currentFrame;
        public BitmapSource CurrentFrame
        {
            get => _currentFrame;
            set
            {
                _currentFrame = value;
                OnPropertyChanged(nameof(CurrentFrame));  // Notify the View that the property changed
            }
        }

        // https://stackoverflow.com/questions/44406587/explanation-of-haarcascade-xml-files-in-opencv
        private InferenceSession _faceModel;  // ONNX model session
        
        public FaceDetectorViewModel()
        {
            _faceModel = new InferenceSession("Resources/version-RFB-640.onnx");

            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            if (videoDevices.Count == 0)
                throw new ApplicationException("No camera found!");

            Console.WriteLine(videoDevices.Count);
            for (int i = 0; i < videoDevices.Count; i++)
            {
                Console.WriteLine($"{i}: {videoDevices[i].Name}");
            }
            _videoSource = new VideoCaptureDevice(videoDevices[1].MonikerString);
            if (_videoSource != null)
            {
                _videoSource.NewFrame += VideoSource_NewFrame;
            }
            try
            {
                _videoSource.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting video source: {ex.Message}");
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IntPtr hBitmap = eventArgs.Frame.GetHbitmap();
                    try
                    {
                        using (Bitmap srcBitmap = Bitmap.FromHbitmap(hBitmap))
                        {
                            Image<Bgr, byte> imageCV = srcBitmap.ToImage<Bgr, byte>();

                            byte[,,] imageData = ConvertImageToByteArray(srcBitmap);
                            var tensor = new DenseTensor<float>(imageData, new[] { 1, srcBitmap.Height, srcBitmap.Width, 3 }); // Assuming model takes NHWC format
                            var input = NamedOnnxValue.CreateFromTensor("input_name", tensor); // Replace "input_name" with your ONNX model's input node name

                            // Run inference on GPU
                            var results = _faceModel.Run(new[] { input });

                            var resultTensor = results.First().AsTensor<float>();
                            // Process the resultTensor to get face detections, you will need to write logic for that depending on your ONNX model's output.

                            foreach (var face in ExtractFacesFromResult(resultTensor))
                            {
                                imageCV.Draw(face, new Bgr(System.Drawing.Color.Red), 2);
                            }

                            CurrentFrame = ConvertToBitmapSource(imageCV.ToBitmap());
                        }


                        // CurrentFrame = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        //    hBitmap,
                        //    IntPtr.Zero,
                        //    Int32Rect.Empty,
                        //    BitmapSizeOptions.FromEmptyOptions());
                    }
                    finally
                    {
                        DeleteObject(hBitmap); // Release the handle
                    }
                });
            }
            else
            {
                Environment.Exit(0);
            }
        }
        private IEnumerable<Rectangle> ExtractFacesFromResult(OnnxTensor resultTensor)
        {
            // Process the result tensor to extract detected faces.
            // This method is just a placeholder, you'll need to implement the logic depending on your ONNX model's output.
            throw new NotImplementedException();
        }

        public byte[,,] ConvertImageToByteArray(Bitmap image)
        {
            int width = image.Width;
            int height = image.Height;
            byte[,,] imageData = new byte[height, width, 3]; // Assuming 3 channels for BGR

            BitmapData bmpData = image.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, image.PixelFormat);

            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
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
                _videoSource.SignalToStop();  // Signal the device to stop capturing
                _videoSource.WaitForStop();   // Wait for the device to finish
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
