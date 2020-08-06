﻿using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes

namespace hello
{
    public class Class1
    {

        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public Class1()
        {
        }
        public static void hello()
        {
            CrestronConsole.PrintLine("Hello world !");
        }
    }
}
