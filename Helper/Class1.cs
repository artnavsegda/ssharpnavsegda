using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes

namespace Helper
{
    public class HelperClass
    {
        public HelperClass()
        {
            // default constructor
        }
        public int ComputeRamp(int rampValue, int rampBound, int transitionTime)
        {
            return (rampValue / rampBound) * transitionTime;
        }
    }
}
