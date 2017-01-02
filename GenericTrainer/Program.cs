using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Diagnostics;

namespace GenericTrainer
{
    class Program
    {
        private static bool Verbose = false;

        private enum EXITCODE : int
        {
            /// <summary>
            /// All operations completed successfully
            /// </summary>
            SUCCESS = 0,
            /// <summary>
            /// No arguments supplied
            /// </summary>
            NO_ARGS = 1,
            /// <summary>
            /// Invalid arguments supplied
            /// </summary>
            INVALID_ARGS = 2,
            /// <summary>
            /// Can't find/launch process
            /// </summary>
            NO_PROCESS = 3,
            /// <summary>
            /// Help requested
            /// </summary>
            HELP = 4,
            /// <summary>
            /// Can't access a property of the process.
            /// </summary>
            ACCESS_ERROR = 5
        }

        private struct Args
        {
            public string ProcessName;
            public int ProcessId;
            public bool Launch;
            public bool HelpRequest;
            public bool Valid;
            public bool Once;
            public Address[] Addresses;

            public void Init()
            {
                ProcessName = string.Empty;
                ProcessId = 0;
                Launch = HelpRequest = Once = false;
                Valid = true;
                Addresses = new Address[0];
            }
        }

        private struct Address
        {
            public bool IsPointer;
            public bool IsOffset;
            public bool MonitorOnly;
            public bool IsReference;
            public int PointerLevels;
            public long MemoryAddress;
            public long Value;
            public AddressType Type;

            public bool ParseString(string Line)
            {
                DebugLog("Processing Address argument: {0}", Line);
                Init();

                //The offset
                if (Line.StartsWith("+"))
                {
                    IsOffset = true;
                    Line = Line.Substring(1);
                    DebugLog("Address is a base address offset");
                }

                //Pointer levels
                //We could make a cheap while loop for this but let's be fancy instead
                if (Line.StartsWith("#"))
                {
                    IsPointer = true;
                    PointerLevels = Line.Length - Line.TrimStart('#').Length;
                    Line = Line.Substring(PointerLevels);
                    DebugLog("Address is a pointer with {0} levels", PointerLevels);
                }

                //If a type has been specified, then check it.
                if (Line.IndexOf(':') == 1)
                {
                    DebugLog("Address has type specifier: {0}", Line[0]);
                    switch (Line.ToUpper()[0])
                    {
                        case 'B':
                            Type = AddressType.Int8;
                            break;
                        case 'S':
                            Type = AddressType.Int16;
                            break;
                        case 'I':
                            Type = AddressType.Int32;
                            break;
                        case 'L':
                            Type = AddressType.Int64;
                            break;
                        default:
                            DebugLog("Invalid value type: {0}", Line[0]);
                            Init();
                            return false;
                    }
                    Line = Line.Substring(2);
                }
                //If '=' is present, this is set and not only monitored
                if (Line.Contains('='))
                {
                    DebugLog("This address is written to: {0}", Line.Split('=')[0]);
                    string Expression = Line.Substring(Line.IndexOf('=') + 1);
                    bool neg = (Expression.IndexOf('-') == 0);
                    if (neg)
                    {
                        DebugLog("Expression is negative");
                        Expression = Expression.TrimStart('-');
                    }
                    if (long.TryParse(
                        //The value. We need to cut off the hex start if present
                        Expression.ToUpper().StartsWith("0X") ? Expression.Substring(2) : Expression,
                        //If hexadecimal, we need to tell this function
                        Expression.ToUpper().StartsWith("0X") ? NumberStyles.HexNumber : NumberStyles.Integer,
                        CultureInfo.CurrentCulture,
                        out Value))
                    {
                        if (neg)
                        {
                            Value *= -1;
                        }
                    }
                    else
                    {
                        DebugLog("Processing Value: {0}", Expression);
                        //Expression is a reference
                        if (Expression.ToUpper().StartsWith("R"))
                        {
                            IsReference = true;
                            if (!long.TryParse(Expression.Substring(1), out Value) || Value < 1)
                            {
                                DebugLog("Invalid reference: {0}", Expression);
                                Init();
                                return false;
                            }
                        }
                        else
                        {
                            DebugLog("Invalid value: {0}", Expression);
                            Init();
                            return false;
                        }
                    }

                    //Keep only the address at this point
                    Line = Line.Split('=')[0];
                }
                else
                {
                    DebugLog("This is only monitored");
                    MonitorOnly = true;
                }

                if (!long.TryParse(Line, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out MemoryAddress))
                {
                    DebugLog("Invalid Address {0}", Line);
                    Init();
                    return false;
                }

                return true;
            }

            public void Init()
            {
                MonitorOnly = IsPointer = IsOffset = IsReference = false;
                MemoryAddress = Value = PointerLevels = 0;
                Type = AddressType.Int32;
            }
        }

        public enum AddressType : int
        {
            Int8 = 1,
            Int16 = 2,
            Int32 = 3,
            Int64 = 4
        }

        static int Main(string[] args)
        {
#if DEBUG
            Verbose = true;
            args = new string[] { "brogue-debug.exe" };
#endif
            Args A = new Args();
            if (args.Length == 0)
            {
                DebugLog("No arguments supplied. Show help and exit");
                Help(EXITCODE.NO_ARGS);
            }
            else
            {
                A = ParseArgs(args);
            }
            if (A.HelpRequest)
            {
                DebugLog("Show help and exit");
                Help(EXITCODE.HELP);
            }
            if (!A.Valid)
            {
                DebugLog("Invalid arguments specified. Exit");
                return (int)EXITCODE.INVALID_ARGS;
            }

            Process P = null;
            if (A.Launch)
            {
                DebugLog("Attempting to launch a new Process");
                try
                {
                    P = Process.Start(A.ProcessName);
                }
                catch (Exception ex)
                {
                    DebugLog("There was an error launching the process.");
                    Console.Error.WriteLine("Can't launch process. Error: {0}", ex.Message);
                    return (int)EXITCODE.NO_PROCESS;
                }
            }
            if (A.ProcessId != 0)
            {
                DebugLog("Attempting to find the process by ID");
                try
                {
                    P = Process.GetProcessById(A.ProcessId);
                }
                catch (Exception ex)
                {
                    DebugLog("Can't find Process with ID {0}", A.ProcessId);
                    Console.Error.WriteLine("Can't find a process with the supplied ID. Error: {0}", ex.Message);
                    return (int)EXITCODE.NO_PROCESS;
                }
            }
            else
            {
                DebugLog("Attempting to find a process by name");
                var Procs = Process.GetProcessesByName(A.ProcessName.Substring(0, A.ProcessName.Length - 4));
                if (Procs == null || Procs.Length == 0)
                {
                    DebugLog("Can't find a process with the name {0}", A.ProcessName);
                    Console.Error.WriteLine("Can't find a running process '{0}'", A.ProcessName);
                    return (int)EXITCODE.NO_PROCESS;
                }
                try
                {
                    P = Process.GetProcessById(Procs.Max(m => m.Id));
                }
                catch
                {
                    //You are very (un-)lucky if you run into this.
                    //It means the process has exited within the last few milliseconds.
                    //We could be fancy and take the next best result.
                    DebugLog("Process has exited while being processed from the list.");
                    Console.Error.WriteLine("The found process has just exited.");
                }
            }

            if (A.Addresses == null || A.Addresses.Length == 0)
            {
                //Just show some Information.
                if (!ShowDetails(P))
                {
                    Console.Error.WriteLine("There were problems accessing some properties. This process might not be usable for memory accesses.");
                    return (int)EXITCODE.ACCESS_ERROR;
                }
            }
            //TODO: Address read/write
#if DEBUG
            Console.Error.WriteLine("#END");
            Console.ReadKey(true);
#endif

            return (int)EXITCODE.SUCCESS;
        }

        private static bool ShowDetails(Process P)
        {
            DebugLog("Showing process details");
            bool success = true;
            Console.WriteLine("Process information:");

            try
            {
                Console.WriteLine("Name:           {0}", P.ProcessName);
            }
            catch
            {
                DebugLog("Can't access property 'ProcessName'");
                success = false;
            }
            try
            {
                Console.WriteLine("FileName:       {0}", P.MainModule.FileName);
            }
            catch
            {
                DebugLog("Can't access property 'MainModule.FileName'");
                success = false;
            }
            try
            {
                Console.WriteLine("BaseAddress:    0x{0}", P.MainModule.BaseAddress.ToString("X"));
            }
            catch
            {
                DebugLog("Can't access property 'MainModule.BaseAddress'");
                success = false;
            }
            try
            {
                Console.WriteLine("Handle:         {0}", P.Handle);
            }
            catch
            {
                DebugLog("Can't access property 'Handle'");
                success = false;
            }
            try
            {
                Console.WriteLine("Priority:       {0}", P.PriorityClass);
            }
            catch
            {
                DebugLog("Can't access property 'PriorityClass'");
                success = false;
            }
            try
            {
                Console.WriteLine("Window Handle:  {0}", P.MainWindowHandle);
            }
            catch
            {
                DebugLog("Can't access property 'MainWindowHandle'");
                success = false;
            }
            try
            {
                Console.WriteLine("Window Title:   {0}", P.MainWindowTitle);
            }
            catch
            {
                DebugLog("Can't access property 'MainWindowTitle'");
                success = false;
            }
            try
            {
                Console.WriteLine("Private Memory: {0}", P.PrivateMemorySize64);
            }
            catch
            {
                DebugLog("Can't access property 'PrivateMemorySize64'");
                success = false;
            }
            try
            {
                Console.WriteLine("Paged Memory:   {0}", P.PagedMemorySize64);
            }
            catch
            {
                DebugLog("Can't access property 'PagedMemorySize64'");
                success = false;
            }
            try
            {
                Console.WriteLine("StartTime:      {0} (Runtime: {1})", P.StartTime, DateTime.Now.Subtract(P.StartTime));
            }
            catch
            {
                DebugLog("Can't access property 'StartTime'");
                success = false;
            }
            try
            {
                Console.WriteLine("Processor Time: {0}", P.TotalProcessorTime);
            }
            catch
            {
                DebugLog("Can't access property 'TotalProcessorTime'");
                success = false;
            }
            return success;
        }

        static Args ParseArgs(string[] Args)
        {
            DebugLog("Processing Command line: {0}", string.Join(" :: ", Args));
            Args A = new Args();
            A.Init();
            List<Address> Addresses = new List<Address>();
            foreach (string AA in Args)
            {
                DebugLog("processing Argument: {0}", AA);
                //This allows modification of AA without C# bitching.
                string Arg = AA;

                switch (Arg.ToUpper())
                {
                    case "/V":
                    case "-V":
                    case "--verbose":
                        Verbose = true;
                        DebugLog("Switchig to verbose mode.");
                        break;
                    case "/?":
                    case "--help":
                    case "-?":
                    case "-h":
                        A.HelpRequest = true;
                        DebugLog("Help request");
                        break;
                    case "/O":
                    case "-O":
                        A.Once = true;
                        DebugLog("Only run once, then exit");
                        break;
                    default:
                        if (A.ProcessId == 0 && string.IsNullOrEmpty(A.ProcessName))
                        {
                            DebugLog("No process has been specified yet. Assuming this is the process argument.");
                            if (Arg.ToUpper().StartsWith("/L:"))
                            {
                                DebugLog("User wants to launch a process");
                                A.Launch = true;
                                Arg = Arg.Substring(3);
                            }
                            if (Arg.ToLower().EndsWith(".exe"))
                            {
                                DebugLog("Process name/path is {0}", Arg);
                                A.ProcessName = Arg;
                            }
                            else if (!int.TryParse(Arg, out A.ProcessId))
                            {
                                DebugLog("The supplied process ID is not a valid integer.");
                                Console.Error.WriteLine("Invalid Process ID: {0}", Arg);
                                A.Valid = false;
                            }
                            else if (A.ProcessId == 0)
                            {
                                DebugLog("The supplied process ID must not be 0.");
                                Console.Error.WriteLine("Invalid Process ID: {0}", Arg);
                                A.Valid = false;
                            }
                            else if (A.Launch)
                            {
                                DebugLog("User attempts to launch a process ID. Let's see if this works (hint: it does not)");
                                Console.Error.WriteLine("Attempted to launch a process ID");
                                A.Valid = false;
                            }
                        }
                        else
                        {
                            Address Addr = new Address();
                            if (Addr.ParseString(Arg))
                            {
                                DebugLog("Address argument is valid");
                                Addresses.Add(Addr);
                            }
                            else
                            {
                                DebugLog("Invalid address argument");
                                Console.Error.WriteLine("Invalid Address region argument: {0}", Arg);
                                A.Valid = false;
                            }
                        }
                        break;
                }
            }
            A.Addresses = Addresses.ToArray();
            DebugLog("Processing Done. Found {0} Addresses", A.Addresses.Length);
            return A;
        }

        static void Help()
        {
            Console.Error.WriteLine(@"
GenericTrainer.exe <[/L:]ProcessName|ProcessId> [/O]
                   [[+][#][Type:]Region[=Value]]

ProcessName   - Name of the Process. Must end in '.exe'
ProcessId     - ID of the Process
/L            - Launch the process specified. If it is not found in the path
                Environment, you have to specify the full/relative path to it.
                if the name/path contains spaces, enclose the whole argument
                with double quotes (including '/L:').
/O            - Run only once and exit. Default is to loop until the target
                process exits or CTRL+C is hit.
+             - If present, the region specifies an offset from the base
#             - If present, the region is treated as a pointer to the location
                That should be read/written instead. Can be used multiple
                times for 'stacked' pointers.
Region        - Memory address to read/write. Hexadecimal
Type          - Value type. Supported: L=Int64, I=Int32, S=Int16, B=Int8
                This argument defaults to 'I'
Value         - The value to set. Prefix with '0x' for hexadecimal.
                You can use negative hexadecimal numbers with '-0x'.
                You can use a region reference here.

Special cases:
- If only the process name or id is supplied, it will show some information.
  This always exits immediately (/O is not required).
  Warning! If you use /L with this, it will launch the Process and leave it
  running 'as-is'.
- If no value is specified for a region, that region is just shown.
- Multiple regions can be specified to show/update multiple values at once.
- If a process name is specified and multiple names are found, it will take
  the one with the highest process ID.
- The value can be a region reference (see example below) to set it to the
  same value a read/written region has. Regions are assigned incremental IDs,
  starting at 1.

Example:

GenericTrainer.exe /L:test.exe +##I:402F #B:85B3E=0x10 +S:0=R1

- This will launch 'test.exe'
- It will monitor the value of the address of the address of the
  base address+0x402F as integer.
  (This is R1).
- It will write the byte value 16 to the address '85B3E' points to.
  (This is R2)
- It will Write the value from R1 to the base address of the process.
  This also converts the value from int to short. It will cut off excessive
  bits without rounding or converting. This always assumes signed numbers.
  (This is R3)
");
            Help();
        }

        static void Help(EXITCODE ExitCode)
        {
            Help();
            DebugLog("Exiting with code: {0}", ExitCode);
#if DEBUG
            Console.Error.WriteLine("#END");
            Console.ReadKey(true);
#endif
            Environment.Exit((int)ExitCode);
        }

        static void DebugLog(string Text, params object[] Args)
        {
            DebugLog(string.Format(Text, Args));
        }

        static void DebugLog(string Text)
        {
            if (Verbose)
            {
                Console.Error.WriteLine(Text);
            }
        }
    }
}
