using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace UltralightNet.Test;

public unsafe class ULBufferTests
{
	[Fact]
	public void TestCopy()
	{
		Span<byte> stack = stackalloc byte[128];
		var buffer = ULBuffer.CreateFrom(stack);

		Assert.NotEqual((nuint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(stack)), (nuint)buffer->Data);
		Assert.Equal(stack.Length, (int)buffer->Size);
		Assert.Equal((nuint)0, (nuint)buffer->UserData);
		Assert.True(buffer->OwnsData);

		buffer->Destroy();
	}

	[Fact]
	public void TestCallback()
	{
		bool called = false;
		void* allocated = NativeMemory.Alloc(128);

		void Callback(void* userData, void* data)
		{
			Assert.Equal((nuint)228, (nuint)userData);
			Assert.Equal((nuint)allocated, (nuint)data);
			NativeMemory.Free(data);
			called = true;
		}

		var buffer = ULBuffer.Create(allocated, 128, Callback, (void*)228);

		Assert.Equal((nuint)allocated, (nuint)buffer->Data);
		Assert.Equal((nuint)128, (nuint)buffer->Size);
		Assert.Equal((nuint)228, (nuint)buffer->UserData);
		Assert.False(buffer->OwnsData);

		buffer->Destroy();

		Assert.True(called);
	}
	[Fact]
	public void ZeroLength()
	{
		byte something = 228;

		var buffer = ULBuffer.Create(&something, 0, userData: (void*)228);

		Assert.Equal((nuint)(byte*)&something, (nuint)buffer->Data);
		Assert.Equal((nuint)0, (nuint)buffer->Size);
		Assert.Equal((nuint)228, (nuint)buffer->UserData);
		Assert.False(buffer->OwnsData);

		buffer->Destroy();
	}
	[Fact]
	public void ZeroLengthCallback()
	{
		bool called = false;
		byte something = 228;

		byte* somethingPtr = &something;

		void Callback(void* userData, void* data)
		{
			Assert.Equal((nuint)228, (nuint)userData);
			Assert.Equal((nuint)somethingPtr, (nuint)data);
			called = true;
		}

		var buffer = ULBuffer.Create(somethingPtr, 0, Callback, (void*)228);

		Assert.Equal((nuint)somethingPtr, (nuint)buffer->Data);
		Assert.Equal((nuint)0, (nuint)buffer->Size);
		Assert.Equal((nuint)228, (nuint)buffer->UserData);
		Assert.False(buffer->OwnsData);

		buffer->Destroy();

		Assert.True(called);
	}
	[Fact]
	public void ZeroLengthCopy()
	{
		byte something = 228;

		var buffer = ULBuffer.CreateFrom(&something, 0);

		Assert.Equal((nuint)0, (nuint)buffer->Data);
		Assert.Equal((nuint)0, (nuint)buffer->Size);
		Assert.Equal((nuint)0, (nuint)buffer->UserData);
		Assert.False(buffer->OwnsData); // ok.

		buffer->Destroy();
	}
	[Fact]
	public void Throws()
	{
		Assert.Throws<ArgumentException>(() => ULBuffer.Create(null, 0, destroyCallback: (userData, data) => { }));
		Assert.Throws<ArgumentException>(() => ULBuffer.Create(null, 0, destroyCallback: (delegate*<void*, void*, void>)1));
	}
}