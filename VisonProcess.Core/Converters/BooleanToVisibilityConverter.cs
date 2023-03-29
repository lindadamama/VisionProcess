﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VisonProcess.Core.Converters
{
    public class BooleanToVisibilityConverter : BaseValueConverter
    {
        public bool UseHidden { get; set; }
        public bool Reversed { get; set; }



        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                if (Reversed) b = !b;
                if(b)
                    return Visibility.Visible;
                else
                    return UseHidden ? Visibility.Hidden : Visibility.Collapsed;
            }
            else
                throw new ArgumentException(nameof(value));
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
