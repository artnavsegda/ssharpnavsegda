using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes

namespace events
{
    /// <summary>
    /// TestBoundary has 2 members : myBoundary and currentValue.
    /// An event will be  triggered whenever currentValue goes over myBoundary.
    /// </summary>
    public class TestBoundary
    {
        private short myBoundary = 75;
        private short currentValue = 70;
        /// <summary>
        /// Definition for Event onOutsideBoundary
        /// </summary>
        public event BoundaryHandler onOutsideBoundary;
        /// <summary>
        /// Function Signature of the event handler for onOutsideBoundary
        /// </summary>
        /// <param name="o">reference to Object that raised the event</param>
        /// <param name="e">object of class EventArgs</param>
        public delegate void BoundaryHandler(object o, EventArgs e);
        /// <summary>
        /// Constructor
        /// </summary>
        public TestBoundary()
        {
            currentValue = 72;
        }
        /// <summary>
        /// Property for private member upperLimit
        /// </summary>
        public int MyBoundary
        {
            get { return myBoundary; }
            set { myBoundary = (short)value; }
        }
        /// <summary>
        /// Checks the new value against myBoundary
        /// and executes the event if necessary.
        /// </summary>
        public short CurrentValue
        {
            get { return currentValue; }
            set
            {
                if (value > myBoundary)
                {
                    if (onOutsideBoundary != null)
                    {
                        onOutsideBoundary(null, null);
                    }
                }
                currentValue = value;
            }
        }
    }
}
