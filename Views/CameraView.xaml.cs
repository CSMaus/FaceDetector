using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using ObjectDetector.ViewModels;

namespace ObjectDetector.Views
{
    public partial class CameraView : Page
    {
        public CameraView()
        {
            InitializeComponent();
            DataContext = new CameraViewModel();
        }
    }
}
