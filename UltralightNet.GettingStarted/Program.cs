using System;
using System.IO;
using System.Threading;
using UltralightNet;
using UltralightNet.AppCore;

namespace UltralightNet.GettingStarted
{
	class Program
	{
		static void Main()
		{
			// Set Logger
			ULPlatform.Logger = new()
			{
				LogMessage = (level, message) =>
				{
					Console.WriteLine($"({level}): {message}");
				}
			};

			// Set Font Loader
			AppCoreMethods.ulEnablePlatformFontLoader();

			// Set filesystem (Ultralight requires "resources/icudt67l.dat", and probably cacert.pem too)
			AppCoreMethods.ulEnablePlatformFileSystem(Path.GetDirectoryName(typeof(Program).Assembly.Location));

			// Create config, used for specifying resources folder (used for URL loading)
			ULConfig config = new();

			// Create Renderer
			Renderer renderer = ULPlatform.CreateRenderer(config);

			// Create View
			View view = renderer.CreateView(512, 512);

			// Load URL

			bool loaded = false;

			view.OnFinishLoading += (frameId, isMainFrame, url) =>
			{
				loaded = true;
			};

			view.URL = "https://github.com"; // Requires "UltralightNet.Resources"

			// Update Renderer until page is loaded
			while (!loaded)
			{
				renderer.Update();
				// sleep | give ultralight time to process network etc.
				Thread.Sleep(10);
			}

			// Render
			renderer.Render();

			// Get Surface
			ULSurface surface = view.Surface;

			// Get Bitmap
			ULBitmap bitmap = surface.Bitmap;

			// Swap Red and Blue channels
			bitmap.SwapRedBlueChannels();

			// save bitmap to png file
			bitmap.WritePng("./github.png");
		}
	}
}
