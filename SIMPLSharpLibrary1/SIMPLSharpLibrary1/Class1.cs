using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes

namespace SIMPLSharpLibrary1
{
    public class Class1
    {
        int ID = 12;
        public delegate void CallbackHandler(SimplSharpString data);
        public CallbackHandler CallbackEvent { set; get; }
        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public Class1()
        {
        }
        public int GetID()
        {
            CrestronConsole.PrintLine("Sample simplsharp callback");
            return ID;
        }
    }
}
