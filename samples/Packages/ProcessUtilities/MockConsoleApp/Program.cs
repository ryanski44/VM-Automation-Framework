using System;
using System.Collections.Generic;
using System.Text;

namespace MockConsoleApp
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Usage: MockConsoleApp [exit code] [seconds to run before exiting]");
            int returnCode = 0;
            int secondsToWait = 0;
            if (args.Length == 2)
            {
                Int32.TryParse(args[0], out returnCode);
                Int32.TryParse(args[1], out secondsToWait);
            }
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(secondsToWait));
            Console.WriteLine("Exit Code: " + returnCode + ", Ran For " + secondsToWait + " seconds.");
            //write a bunch of text, so that when the process exits there is still text in a buffer, this is to check a feature in UtilityBackgroundProcess where it waits for all standard output to be written
            for (int i = 0; i < 50; i++)
            {
                Console.WriteLine("Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.");
            }
            return returnCode;
        }
    }
}
