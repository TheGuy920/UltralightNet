using Octokit;
using PeNet;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace UltralightNet.Generator
{
	public static class Program
	{
		public static async Task Main(string[] args)
		{
			GitHubClient client = new(new ProductHeaderValue("my-cool-app"));
			var latestRelease = await client.Repository.Release.GetLatest("ultralight-ux", "Ultralight");

			ReleaseAsset asset = null;
			foreach (var _asset in latestRelease.Assets)
			{
				if (_asset.Name.EndsWith("win-x64.7z"))
				{
					asset = _asset;
				}
			}
			if (asset is null) throw new Exception();

			Console.WriteLine(asset.Url);
			Console.WriteLine(asset.BrowserDownloadUrl);

			WebRequest request = WebRequest.CreateHttp(asset.BrowserDownloadUrl);
			WebResponse response = await request.GetResponseAsync();
			Stream responseStream = response.GetResponseStream();
			MemoryStream stream = new();
			await responseStream.CopyToAsync(stream);

			var archive = SevenZipArchive.Open(stream);

			var entries = archive.Entries;

			SevenZipArchiveEntry seven_entry = null;

			foreach(var entry in entries)
			{
				Console.WriteLine(entry.Key);
				if (entry.Key == "bin/Ultralight.dll")
				{
					seven_entry = entry;
					break;
				}
			}

			if (seven_entry is null) throw new Exception();

			Stream ultralightDLLReadOnly = seven_entry.OpenEntryStream();

			MemoryStream ultralightDll = new();

			await ultralightDLLReadOnly.CopyToAsync(ultralightDll);

			PeFile peFile = new(ultralightDll);

			foreach(var exported in peFile.ExportedFunctions)
			{
				Console.WriteLine(exported.Name);
			}
		}
	}
}
