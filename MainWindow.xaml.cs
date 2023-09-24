﻿using System;
using System.Windows;
using AForge.Video;
using AForge.Video.DirectShow;
using ObjectDetector.Views;
using ObjectDetector.ViewModels;

namespace ObjectDetector
{
    public partial class MainWindow : Window
    {
        private CameraView _cameraView;
        public MainWindow()
        {
            InitializeComponent();
            _cameraView = new CameraView();
            cameraFrame.Navigate(_cameraView);
        }
        public void UpdateCameraViewDataContext(CameraViewModel cameraViewModel)
        {
            _cameraView.DataContext = null;
            _cameraView.DataContext = cameraViewModel;
        }

    }
}
