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

        public void SetHSV(ushort hue, ushort saturation, ushort value)
        {
            this.Hue = hue;
            this.Saturation = saturation;
            this.Value = value;
            this.CalculateRGB();
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

        public void CalculateRGB()
        {
            double h = (double)this.Hue;
            double S = (double)this.Saturation / 100;
            double V = (double)this.Value / 100;

            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R, G, B;
            if (V <= 0)
            { R = G = B = 0; }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                double hf = H / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i)
                {

                    // Red is the dominant color

                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;

                    // Green is the dominant color

                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;

                    // Blue is the dominant color

                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;

                    // Red is the dominant color

                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // The color is not defined, we should throw an error.

                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }
            this.Red = (ushort)(R * 65535);
            this.Green = (ushort)(G * 65535);
            this.Blue = (ushort)(B * 65535);
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

        public ColourSpaceHSV()
        {
            //default constructor
        }
    }
}
