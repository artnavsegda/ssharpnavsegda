using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes

namespace hsvtorgb
{
    public class ColourSpaceHSV
    {
        public ushort Red { get; set; }

        public ushort Green { get; set; }

        public ushort Blue { get; set; }

        public ushort Hue { get; set; }

        public ushort Saturation { get; set; }

        public ushort Value { get; set; }

        public void SetRGB(ushort red, ushort green, ushort blue)
        {
            this.Red = red;
            this.Green = green;
            this.Blue = blue;
            this.CalculateHSV();
        }

        public void CalculateHSV()
        {
            double red = (double) this.Red;
            double green = (double) this.Green;
            double blue = (double) this.Blue;
            double val1 = this.ScaleRBG(red);
            double val2_1 = this.ScaleRBG(green);
            double val2_2 = this.ScaleRBG(blue);
            double num1 = Math.Min(Math.Min(val1, val2_1), val2_2);
            double num2 = Math.Max(Math.Max(val1, val2_1), val2_2);
            double n1 = num2;
            double num3 = num2 - num1;
            if (num2 != 0.0)
            {
                double n2 = num3/num2;
                double h = (val1 != num2
                    ? (val2_1 != num2 ? 4.0 + (val1 - val2_1)/num3 : 2.0 + (val2_2 - val1)/num3)
                    : (val2_1 - val2_2)/num3)*60.0;
                if (h < 0.0)
                    h += 360.0;
                this.Hue = this.ScaleHue(h);
                this.Saturation = this.Scale(n2);
                this.Value = this.Scale(n1);
            }
        }

        private double ScaleRBG(double value)
        {
            value /= 256.0;
            value /= (double) byte.MaxValue;
            return value;
        }

        private ushort ScaleHue(double h)
        {
            double num = 0.28;
            return (ushort) (655.0*(h*num));
        }

        private ushort Scale(double n)
        {
            n = Math.Round(n, 2);
            if (n > 1.0)
                n = 1.0;
            return (ushort) (n*(double) ushort.MaxValue);
        }
    }
}
