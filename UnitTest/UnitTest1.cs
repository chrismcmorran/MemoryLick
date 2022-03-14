using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MemoryLick;
using NUnit.Framework;

namespace UnitTest;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var licker = new Licker(Process.GetCurrentProcess());
        var read = licker.Read<string>(Process.GetCurrentProcess().MainModule.BaseAddress);
        Assert.IsNotNull(read);
    }
    
    [Test]
    public unsafe void TestReadBool()
    {
        var licker = new Licker(Process.GetCurrentProcess());
        var data = false;
        var read = licker.Read<bool>(&data);
        Assert.AreEqual(data, read);
        data = true;
        read = licker.Read<bool>(&data);
        Assert.AreEqual(data, read);
    }
    
    [Test]
    public unsafe void TestReadLong()
    {
        var licker = new Licker(Process.GetCurrentProcess());
        var data = long.MaxValue;
        var read = licker.Read<long>(&data);
        Assert.AreEqual(data, read);
        data = long.MinValue;
        read = licker.Read<long>(&data);
        Assert.AreEqual(data, read);
    }
    
    [Test]
    public unsafe void TestReadChar()
    {
        var licker = new Licker(Process.GetCurrentProcess());
        var data = 'a';
        var read = licker.Read<char>(&data);
        Assert.AreEqual(data, read);
        data = 'b';
        read = licker.Read<char>(&data);
        Assert.AreEqual(data, read);
    }
}