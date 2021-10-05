using System;
using System.Runtime.InteropServices;

namespace UltralightNet
{
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct ULLogger : IDisposable
	{
		public ULLoggerLogMessageCallback LogMessage { set { unsafe { _LogMessage = (level, msg) => value(level, ULString.NativeToManaged(msg)); } } }

		public ULLoggerLogMessageCallback__PInvoke__ _LogMessage
		{
			set
			{
				unsafe
				{
					ULLoggerLogMessageCallback__PInvoke__ logMessage = value;
					ULPlatform.loggerHandles.Add(this, GCHandle.Alloc(logMessage, GCHandleType.Normal));
					__LogMessage = (delegate* unmanaged[Cdecl]<ULLogLevel, ULString*, void>)Marshal.GetFunctionPointerForDelegate(logMessage);
				}
			}
		}

		public delegate* unmanaged[Cdecl]<ULLogLevel, ULString*, void> __LogMessage;

		public void Dispose()
		{
			if (ULPlatform.loggerHandles.ContainsKey(this))
			{
				GCHandle handle = ULPlatform.loggerHandles[this];
				if (handle.IsAllocated)
				{
					handle.Free();
				}
			}
		}
	}
}
