using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace WinAPI
{
    /// <summary>
    /// Provides Read/Write Access to a Process
    /// </summary>
    public class ProcessAPI : IDisposable
    {
        #region WinAPI

        /// <summary>
        /// Opens a process to access its memory
        /// </summary>
        /// <param name="dwDesiredAccess">Read/Write request</param>
        /// <param name="bInheritHandle">True to inherit the handle for child processes.</param>
        /// <param name="dwProcessId">Process ID to open</param>
        /// <returns>Handle or IntPtr.Zero on error</returns>
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccess dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        /// <summary>
        /// Closes a Handle
        /// </summary>
        /// <param name="Handle">Handle</param>
        /// <returns>True on success</returns>
        /// <remarks>There is no "CloseProcess" function.</remarks>
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr Handle);

        /// <summary>
        /// Reads a region from a process
        /// </summary>
        /// <param name="hProcess">Process Handle</param>
        /// <param name="lpBaseAddress">Address to read from</param>
        /// <param name="lpBuffer">Buffer to read into</param>
        /// <param name="dwSize">Number of bytes to read</param>
        /// <param name="lpNumberOfBytesRead">Number of bytes actually read</param>
        /// <returns>True on success</returns>
        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        /// <summary>
        /// Writes a region to a process
        /// </summary>
        /// <param name="hProcess">Process Handle</param>
        /// <param name="lpBaseAddress">Address to begin writing</param>
        /// <param name="lpBuffer">Buffer to write from</param>
        /// <param name="dwSize">Number of bytes to write</param>
        /// <param name="lpNumberOfBytesWritten">Number of bytes actually written</param>
        /// <returns>True on success</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);

        /// <summary>
        /// Flags for process memory access.
        /// </summary>
        [Flags]
        private enum ProcessAccess : int
        {
            PROCESS_WM_READ = 0x0010,
            PROCESS_VM_WRITE = 0x0020,
            PROCESS_VM_OPERATION = 0x0008,
            PROCESS_VM_READWRITE = PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_WM_READ
        }

        #endregion

        private Process P;
        private IntPtr Handle;

        #region Reads

        /// <summary>
        /// Reads a Byte array from Memory
        /// </summary>
        /// <param name="Address">Address to read from</param>
        /// <param name="Count">Number of bytes to read</param>
        /// <returns>Bytes read from Memory</returns>
        private byte[] Read(int Address, int Count)
        {
            byte[] Data = new byte[Count];
            int readed = 0;
            ReadProcessMemory(Handle, Address, Data, Count, ref readed);
            if (readed != Data.Length)
            {
                Array.Resize(ref Data, readed);
            }
            return Data;
        }

        /// <summary>
        /// Read an byte from Memory
        /// </summary>
        /// <param name="Address">Location to read from</param>
        /// <returns>Value read from Memory</returns>
        public byte Read8(int Address)
        {
            return Read(Address,1)[0];
        }

        /// <summary>
        /// Read an Int16 from Memory
        /// </summary>
        /// <param name="Address">Location to read from</param>
        /// <returns>Value read from Memory</returns>
        public short Read16(int Address)
        {
            return BitConverter.ToInt16(Read(Address, 2), 0);
        }

        /// <summary>
        /// Read an Int32 from Memory
        /// </summary>
        /// <param name="Address">Location to read from</param>
        /// <returns>Value read from Memory</returns>
        public int Read32(int Address)
        {
            return BitConverter.ToInt32(Read(Address, 4), 0);
        }

        /// <summary>
        /// Read an Int64 from Memory
        /// </summary>
        /// <param name="Address">Location to read from</param>
        /// <returns>Value read from Memory</returns>
        public long Read64(int Address)
        {
            return BitConverter.ToInt64(Read(Address, 8), 0);
        }

        #endregion

        #region Writes

        /// <summary>
        /// Writes a Byte array to Memory
        /// </summary>
        /// <param name="Address">Address to write to</param>
        /// <param name="Data">Data to write</param>
        /// <returns>True on success</returns>
        private bool Write(int Address, byte[] Data)
        {
            int written = 0;
            return WriteProcessMemory(Handle, Address, Data, Data.Length, ref written) && written == Data.Length;
        }

        /// <summary>
        /// Writes an Int8 to Memory
        /// </summary>
        /// <param name="Address">Address to Write to</param>
        /// <param name="Value">Data to Write</param>
        /// <returns>True on success</returns>
        public bool Write(int Address, byte Value)
        {
            return Write(Address, new byte[] { Value });
        }

        /// <summary>
        /// Writes an Int16 to Memory
        /// </summary>
        /// <param name="Address">Address to Write to</param>
        /// <param name="Value">Data to Write</param>
        /// <returns>True on success</returns>
        public bool Write(int Address, short Value)
        {
            return Write(Address, BitConverter.GetBytes(Value));
        }

        /// <summary>
        /// Writes an Int32 to Memory
        /// </summary>
        /// <param name="Address">Address to Write to</param>
        /// <param name="Value">Data to Write</param>
        /// <returns>True on success</returns>
        public bool Write(int Address, int Value)
        {
            return Write(Address, BitConverter.GetBytes(Value));
        }

        /// <summary>
        /// Writes an Int64 to Memory
        /// </summary>
        /// <param name="Address">Address to Write to</param>
        /// <param name="Value">Data to Write</param>
        /// <returns>True on success</returns>
        public bool Write(int Address, long Value)
        {
            return Write(Address, BitConverter.GetBytes(Value));
        }

        #endregion

        /// <summary>
        /// Gets the currently monitored process (or null if terminated)
        /// </summary>
        public Process Process
        {
            get
            {
                //Return only a clone
                try
                {
                    return System.Diagnostics.Process.GetProcessById(P.Id);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Initialize Process memory access
        /// </summary>
        /// <param name="P">Process to access</param>
        public ProcessAPI(Process P)
        {
            if (P == null)
            {
                throw new ArgumentNullException("P");
            }
            if (P.HasExited)
            {
                throw new ArgumentException("The process has exited");
            }
            this.P = System.Diagnostics.Process.GetProcessById(P.Id);
            Handle = OpenProcess(ProcessAccess.PROCESS_VM_READWRITE, false, P.Id);
            if (Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Can't open process memory.");
            }
        }

        ~ProcessAPI()
        {
            Dispose();
        }

        /// <summary>
        /// Closes the Access handle properly and releases the Process object
        /// </summary>
        public void Dispose()
        {
            if (P != null)
            {
                P.Dispose();
                P = null;
            }
            if (Handle != IntPtr.Zero)
            {
                CloseHandle(Handle);
                Handle = IntPtr.Zero;
            }
        }
    }
}
