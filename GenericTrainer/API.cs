using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace WinAPI
{
    public class ProcessAPI : IDisposable
    {
        #region WinAPI

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccess dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr Handle);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);

        [Flags]
        private enum ProcessAccess : int
        {
            PROCESS_WM_READ = 0x0010,
            PROCESS_VM_WRITE = 0x0020,
            PROCESS_VM_OPERATION = 0x0008,
            PROCESS_VM_READWRITE = PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_WM_READ
        }

        public struct ERROR
        {
            public const int PROCESS_INVALID = -1;
            public const int READ_ERROR = -2;
        }

        public struct ENERGY
        {
            public const int MIN = 0;
            public const int MAX = 30;
        }

        private const int ADDR = 0x00573886;

        #endregion

        private Process P;
        private IntPtr Handle;

        #region Reads

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

        public byte Read8(int Address)
        {
            return Read(Address,1)[0];
        }

        public short Read16(int Address)
        {
            return BitConverter.ToInt16(Read(Address, 2), 0);
        }

        public int Read32(int Address)
        {
            return BitConverter.ToInt32(Read(Address, 4), 0);
        }

        public long Read64(int Address)
        {
            return BitConverter.ToInt64(Read(Address, 8), 0);
        }

        #endregion

        #region Writes

        private bool Write(int Address, byte[] Data)
        {
            int written = 0;
            return WriteProcessMemory(Handle, Address, Data, Data.Length, ref written) && written == Data.Length;
        }

        public void Write(int Address, byte Value)
        {
            Write(Address, new byte[] { Value });
        }

        public void Write(int Address, short Value)
        {
            Write(Address, BitConverter.GetBytes(Value));
        }

        public void Write(int Address, int Value)
        {
            Write(Address, BitConverter.GetBytes(Value));
        }

        public void Write(int Address, long Value)
        {
            Write(Address, BitConverter.GetBytes(Value));
        }

        #endregion

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
            this.P = P;
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
