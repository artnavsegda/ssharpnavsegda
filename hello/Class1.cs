using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes

namespace hello
{
    public class HelloClass
    {

        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public HelloClass()
        {
        }
        public static void Hello()
        {
            CrestronConsole.PrintLine("Hello world !");
        }
    }
}
