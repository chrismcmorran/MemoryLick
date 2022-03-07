using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MemoryLick
{
    public class Licker
    {
        private int _defaultReadSize = 128;
        private const int maxReadSize = 4096 * 4;
        private Process _process;
        private IntPtr _processHandle;
        private uint _oldProtectionValue;
        private int _oldProtectionSize;
        private IntPtr _oldProtectionAddress;

        /// <summary>
        /// Creates a new MemoryLick.
        /// </summary>
        /// <param name="process">The process to use.</param>
        public Licker(Process process)
        {
            _process = process;
#if OS_WINDOWS
            _processHandle = Imports.OpenProcess(Permission.Tamper, 0, (uint) process.Id);
#endif
        }

        /// <summary>
        /// Creates a new MemoryLick with the specified process and default read size.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="defaultReadSize">The default number of bytes to read.</param>
        public Licker(Process process, int defaultReadSize) : this(process)
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
#if OS_WINDOWS
            Imports.CloseHandle(_processHandle);
#endif
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
        /// Writes the provided byte to the address.
        /// </summary>
        /// <param name="address">The starting address.</param>
        /// <param name="data">The byte.</param>
        public void WriteByte(int address, byte data)
        {
            Write(address, new[] {data});
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
#if OS_WINDOWS
            AllowPageTableTampering(address, data.Length);
            Imports.WriteProcessMemory(_processHandle, new IntPtr(address), data, data.Length, out var _);
            RestorePageTablePermissions();
#endif
#if OS_LINUX
            this.Write<byte[]>(data, new IntPtr(address));
#endif
        }

        public unsafe bool Write<T>(T value, IntPtr address) where T : unmanaged
        {
            var ptr = &value;
            var size = Util.SizeOf<T>();
            var localIo = new iovec
            {
                iov_base = ptr,
                iov_len = size
            };
            var remoteIo = new iovec
            {
                iov_base = address.ToPointer(),
                iov_len = size
            };
            var res = Imports.process_vm_writev(_process.Id, &localIo, 1, &remoteIo, 1, 0);
            return res != -1;
        }

        #endregion

        #region Read

        /// <summary>
        /// Reads a UInt64 from the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>A UInt64.</returns>
        public UInt64 ReadUInt64(int address)
        {
            return BitConverter.ToUInt64(Read(address, sizeof(UInt64)), 0);
        }

        /// <summary>
        /// Reads a UInt32 from the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>A UInt32.</returns>
        public UInt32 ReadUInt32(int address)
        {
            return BitConverter.ToUInt32(Read(address, sizeof(UInt32)), 0);
        }

        /// <summary>
        /// Reads a UInt16 from the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>A UInt16.</returns>
        public UInt16 ReadUInt16(int address)
        {
            return BitConverter.ToUInt16(Read(address, sizeof(UInt16)), 0);
        }

        /// <summary>
        /// Follows a chain of pointers and returns the last address in the chain.
        /// </summary>
        /// <param name="address">The base address.</param>
        /// <param name="offsets">The offsets to follow.</param>
        /// <returns>An int.</returns>
        public int FollowPointer(int address, params int[] offsets)
        {
            var addr = address;
            foreach (var offset in offsets)
            {
                addr = ReadInt(addr);
                if (addr == 0)
                {
                    return 0;
                }

                addr += offset;
            }

            return addr;
        }

        /// <summary>
        /// Reads a string starting from the provided address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>A string.</returns>
        public string ReadString(int address)
        {
            bool terminated = false;
            int allocated = 0;
            var builder = new StringBuilder(_defaultReadSize);
            while (!terminated && allocated < maxReadSize)
            {
                var bytes = Read(address, _defaultReadSize);
                allocated += _defaultReadSize;
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
        public Int16 ReadInt16(int address)
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
            var bytes = new byte[size];
#if OS_WINDOWS
            AllowPageTableTampering(address, size);
            Imports.ReadProcessMemory(_processHandle, new IntPtr(address), bytes, (uint) size, out var _);
            RestorePageTablePermissions();
            return bytes;
#endif
            for (int i = 0; i < size; i++)
            {
                this.Read<byte>(new IntPtr(address + i), out bytes[i]);
            }

            return bytes;
        }

        public unsafe bool Read<T>(IntPtr address, out T value) where T : unmanaged
        {
            var size = Util.SizeOf<T>();
            var ptr = stackalloc byte[size];
            var localIo = new iovec
            {
                iov_base = ptr,
                iov_len = size
            };
            var remoteIo = new iovec
            {
                iov_base = address.ToPointer(),
                iov_len = size
            };

            var res = Imports.process_vm_readv(_process.Id, &localIo, 1, &remoteIo, 1, 0);
            value = *(T*) ptr;
            return res != -1;
        }

        #endregion

        #region Permissions

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AllowPageTableTampering(int address, int size)
        {
            _oldProtectionAddress = new IntPtr(address);
            _oldProtectionSize = size;
#if OS_WINDOWS
            Imports.VirtualProtectEx(_processHandle, new IntPtr(address), (UIntPtr) size, Permission.Tamper,
                out _oldProtectionValue);
#endif
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RestorePageTablePermissions()
        {
#if OS_WINDOWS
            Imports.VirtualProtectEx(_processHandle, _oldProtectionAddress, (UIntPtr) _oldProtectionSize,
                _oldProtectionValue, out var _);
#endif
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void MakePageTableReadOnly(int address, int size)
        {
            _oldProtectionAddress = new IntPtr(address);
            _oldProtectionSize = size;
#if OS_WINDOWS
            Imports.VirtualProtectEx(_processHandle, new IntPtr(address), (UIntPtr) size, Permission.Tamper,
                out _oldProtectionValue);
#endif
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetPageTablePermissions(int address, int size, Permission permission)
        {
            _oldProtectionAddress = new IntPtr(address);
            _oldProtectionSize = size;
#if OS_WINDOWS
            Imports.VirtualProtectEx(_processHandle, new IntPtr(address), (UIntPtr) size, (uint) permission.Value,
                out _oldProtectionValue);
#endif
        }

        #endregion
    }
}