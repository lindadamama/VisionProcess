﻿using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VisonProcess.Core.Attributes;
using VisonProcess.Core.Strings;
using VisonProcess.Core.ToolBase;
using VisonProcess.Tools.Models;

namespace VisonProcess.Tools.ViewModels
{
    [DefaultToolConnector(false, "Image", "Outputs.Image")]
    public partial class AcquireImageViewModel : OperationBase<InputsBase, AcquireImageOutput, GraphicsBase>
    {
        public AcquireImageViewModel() : base()
        {
            Init();
        }

        private int currentIndex = 0;
        private List<string>? imagePaths;
        protected override bool InternalExecute(out string message)
        {
            message = "";

            if (imagePaths == null || imagePaths.Count < 1)
            {
                message = "";
                return false;
            }

            if (currentIndex > imagePaths.Count - 1)
            {
                currentIndex = 0;
            }
            Outputs.Image = new Mat(imagePaths[currentIndex]);
            Records[0].DisplayImage = Outputs.Image.ToBitmapSource();

            currentIndex++;
            ////延时
            //Thread.Sleep(1000);

            return true;
        }

        [RelayCommand]
        private void AcquireLocalImages()
        {
            var dialog = new OpenFileDialog();
            //dialog.FileName = "Document"; // Default file name
            dialog.Multiselect = true;
            //dialog.DefaultExt = ".txt"; // Default file extension
            dialog.Filter = $"{Strings.ImageFiles}  (*.jpg*.bmp*.png)|*.jpg;*.bmp;*.png"; // Filter files by extension
            dialog.Title = $"{Strings.PleaseSelectFiles}";
            // Show open file dialog box
            bool? result = dialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                //string filename = dialog.FileName;
                imagePaths = dialog.FileNames.ToList();
                currentIndex = 0;
            }


            Execute();
        }

        private void Init()
        {
            Records.Add(new() { Title = Strings.OutputImage });
        }
    }
}