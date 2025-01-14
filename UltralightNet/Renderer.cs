using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace UltralightNet
{
	public static partial class Methods
	{
		[DllImport("Ultralight")]
		public static extern IntPtr ulCreateRenderer(IntPtr config);

		/// <summary>Destroy the renderer.</summary>
		[DllImport("Ultralight")]
		public static extern void ulDestroyRenderer(IntPtr renderer);

		/// <summary>Update timers and dispatch internal callbacks (JavaScript and network).</summary>
		[DllImport("Ultralight")]
		public static extern void ulUpdate(IntPtr renderer);

		/// <summary>Render all active Views.</summary>
		[DllImport("Ultralight")]
		public static extern void ulRender(IntPtr renderer);

		/// <summary>Attempt to release as much memory as possible. Don't call this from any callbacks or driver code.</summary>
		[DllImport("Ultralight")]
		public static extern void ulPurgeMemory(IntPtr renderer);

		[DllImport("Ultralight")]
		public static extern void ulLogMemoryUsage(IntPtr renderer);
	}

	public class Renderer : IDisposable
	{
		public readonly IntPtr Ptr;

		private Renderer(IntPtr ptr, bool dispose)
		{
			Ptr = ptr;
			IsDisposed = !dispose;
		}
		public static Renderer FromIntPtr(IntPtr ptr, bool dispose = false) => new(ptr, dispose);

		internal Renderer(ULConfig config, bool dispose)
		{
			Ptr = Methods.ulCreateRenderer(config.Ptr);
			IsDisposed = !dispose;
		}

		public View CreateView(uint width, uint height) => CreateView(width, height, new ULViewConfig());
		public View CreateView(uint width, uint height, ULViewConfig viewConfig) => CreateView(width, height, viewConfig, DefaultSession);
		public View CreateView(uint width, uint height, ULViewConfig viewConfig, Session session) => CreateView(width, height, viewConfig, session, true);

		public View CreateView(uint width, uint height, ULViewConfig viewConfig, Session session, bool dispose)
		{
			if (ULPlatform.ErrorGPUDriverNotSet && viewConfig.IsAccelerated && !ULPlatform.gpudriverSet)
			{
				throw new Exception("No ULPlatform.GPUDriver set, but ULViewConfig.IsAccelerated==true. (Disable error by setting ULPlatform.ErrorGPUDriverNotSet to false.)");
			}
			View view = new(Methods.ulCreateView(Ptr, width, height, viewConfig.Ptr, session.Ptr), dispose);
			view.Renderer = this;
			return view;
		}
		public Session CreateSession(bool isPersistent, string name) => new(this, isPersistent, name);
		public Session DefaultSession => new(Methods.ulDefaultSession(Ptr));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Update() => Methods.ulUpdate(Ptr);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Render() => Methods.ulRender(Ptr);
		public void PurgeMemory() => Methods.ulPurgeMemory(Ptr);
		public void LogMemoryUsage() => Methods.ulLogMemoryUsage(Ptr);

		public bool IsDisposed
		{
			get;
			private set;
		}
		~Renderer() => Dispose();
		public void Dispose()
		{
			if (IsDisposed) return;
			Methods.ulDestroyRenderer(Ptr);

			IsDisposed = true;
			GC.SuppressFinalize(this);
		}
	}
}
