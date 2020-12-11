using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace MemoryLick
{
    public class MemoryLick
    {
        private const int TamperAccess = (0x000F0000) | (0x00100000) | (0xFFFF);
        private int _defaultReadSize = 128;
        private Process _process;
        private IntPtr _processHandle;
        private uint _oldProtectionValue;
        private int _oldProtectionSize;
        private IntPtr _oldProtectionAddress;
        
        /// <summary>
        /// Creates a new MemoryLick.
        /// </summary>
        /// <param name="process">The process to use.</param>
        public MemoryLick(Process process)
        {
            _process = process;
            _processHandle = Imports.OpenProcess(TamperAccess, 0, (uint) process.Id);
        }

        /// <summary>
        /// Creates a new MemoryLick with the specified process and default read size.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="defaultReadSize">The default number of bytes to read.</param>
        public MemoryLick(Process process, int defaultReadSize) : this(process)
        {
            this._defaultReadSize = defaultReadSize;
        }

        /// <summary>
        /// Gets the base address of the process.
        /// </summary>
        /// <returns>An int32.</returns>
        public int BaseAddress()
        {
            return _process.MainModule.BaseAddress.ToInt32();
        }

        /// <summary>
        /// Closes the process handle.
        /// </summary>
        public void Discard()
        {
            Imports.CloseHandle(_processHandle);
        }

        #region Write
        
        /// <summary>
        /// Writes the provided bytes starting from the provided address.
        /// </summary>
        /// <param name="address">The starting address.</param>
        /// <param name="data">The bytes.</param>
        public void WriteBytes(int address, byte[] data)
        {
            Write(address, data);
        }
        
        /// <summary>
        /// Writes the provided int to the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="value">The value.</param>
        public void WriteInt(int address, int value)
        {
            Write(address, BitConverter.GetBytes(value));
        }
        
        /// <summary>
        /// Writes the provided int16 to the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="value">The value.</param>
        public void WriteInt16(int address, Int16 value)
        {
            Write(address, BitConverter.GetBytes(value));
        }
        
        /// <summary>
        /// Writes the provided bool to the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="value">The value.</param>
        public void WriteBool(int address, bool value)
        {
            Write(address, BitConverter.GetBytes(value));
        }
        
        /// <summary>
        /// Writes the provided float to the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="value">The value.</param>
        public void WriteFloat(int address, float value)
        {
            Write(address, BitConverter.GetBytes(value));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Write(int address, byte[] data)
        {
            AllowPageTableTampering(address, data.Length);
            var success = Imports.WriteProcessMemory(_processHandle, new IntPtr(address), data, data.Length, out var _);
            RestorePageTablePermissions();
            if (!success)
            {
                throw new Exception($"Failed to write memory ({address})");
            }
        }
        #endregion

        #region Read
        
        /// <summary>
        /// Reads a string starting from the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>A string.</returns>
        public string ReadString(int address)
        {
            bool terminated = false;
            var builder = new StringBuilder(_defaultReadSize);
            while (!terminated)
            {
                var bytes = Read(address, _defaultReadSize);
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] != '\0')
                    {
                        builder.Append((char) bytes[i]);
                    }
                    else
                    {
                        terminated = true;
                        // This handles UTF-16 strings, because I'm too lazy to figure out how to handle this right now.
                        if (i < bytes.Length - 1 && bytes[i + 1] == (byte) '\0')
                        {
                            break;
                        }
                    }
                }
            }

            return builder.ToString();
        }
        
        /// <summary>
        /// Reads an int from the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>An int.</returns>
        public int ReadInt(int address)
        {
            return BitConverter.ToInt32(Read(address, sizeof(int)), 0);
        }
        
        /// <summary>
        /// Reads an int16 from the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>An int16.</returns>
        public float ReadInt16(int address)
        {
            return BitConverter.ToInt16(Read(address, sizeof(Int16)), 0);
        }
        
        /// <summary>
        /// Reads a float from the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>A float.</returns>
        public float ReadFloat(int address)
        {
            return BitConverter.ToSingle(Read(address, sizeof(float)), 0);
        }
        
        /// <summary>
        /// Reads a bool from the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>A bool.</returns>
        public bool ReadBool(int address)
        {
            return BitConverter.ToBoolean(Read(address, sizeof(bool)), 0);
        }
        
        /// <summary>
        /// Reads a byte from the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>A byte.</returns>
        public byte ReadByte(int address)
        {
            return Read(address, 1)[0];
        }
        
        /// <summary>
        /// Reads a byte array starting from the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>A byte array.</returns>
        public byte[] ReadBytes(int address, int count)
        {
            return Read(address, count);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private byte[] Read(int address, int size)
        {
            AllowPageTableTampering(address, size);
            var bytes = new byte[size];
            Imports.ReadProcessMemory(_processHandle, new IntPtr(address), bytes, (uint) size, out var _);
            RestorePageTablePermissions();
            return bytes;
        }

        #endregion

        #region Permissions
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void AllowPageTableTampering(int address, int size)
        {
            _oldProtectionAddress = new IntPtr(address);
            _oldProtectionSize = size;
            Imports.VirtualProtectEx(_processHandle, new IntPtr(address), (UIntPtr) size, TamperAccess,
                out _oldProtectionValue);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void RestorePageTablePermissions()
        {
            Imports.VirtualProtectEx(_processHandle, _oldProtectionAddress, (UIntPtr) _oldProtectionSize,
                _oldProtectionValue, out var _);
        }
        #endregion
        
    }
}