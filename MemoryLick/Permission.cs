using System;

namespace MemoryLick
{
    public class Permission
    {
        public const int Tamper = (0x000F0000) | (0x00100000) | (0xFFFF);
        public const int ReadOnly = 0x20;
        public const int ReadWrite = 0x40;
        public const int ExecuteWriteCopy = 0x80;

        public int Value { get; set; }

        public Permission(int value)
        {
            if (value != Tamper && value != ReadOnly && value != ReadWrite && value != ExecuteWriteCopy)
            {
                throw new Exception("Unsupported permission value");
            }
            Value = value;
        }
    }
}