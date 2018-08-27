﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;

namespace WalletWasabi.TorSocks5
{
	public class TorProcessManager
	{
		public IPEndPoint TorSocks5EndPoint { get; }

		/// <param name="torSocks5EndPoint">Opt out Tor with null.</param>
		public TorProcessManager(IPEndPoint torSocks5EndPoint)
		{
			TorSocks5EndPoint = torSocks5EndPoint ?? new IPEndPoint(IPAddress.Loopback, 9050);
		}

		public async Task StartAsync()
		{
			// 1. Is it already running?
			// 2. Can I simply run it from output directory?
			// 3. Can I copy and unzip it from assets?
			// 4. Throw exception.

			try
			{
				if (await IsTorRunningAsync(TorSocks5EndPoint))
				{
					return;
				}

				var torPath = "";
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					torPath = @"tor\Tor\tor.exe";
				}
				else // Linux or OSX
				{
					torPath = @"tor/Tor/tor";
				}

				if (!File.Exists(torPath))
				{
					var torDir = Path.Combine(AppContext.BaseDirectory, "tor");
					var torDaemonsDir = $"TorDaemons";
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						try
						{
							ZipFile.ExtractToDirectory(Path.Combine(torDaemonsDir, "tor-win32.zip"), torDir);
						}
						catch (UnauthorizedAccessException)
						{
							await Task.Delay(100);
							ZipFile.ExtractToDirectory(Path.Combine(torDaemonsDir, "tor-win32.zip"), torDir);
						}
					}
					else
					{
						throw new NotImplementedException();
					}
				}

				var torProcessStartInfo = new ProcessStartInfo(torPath)
				{
					Arguments = $"SOCKSPort {TorSocks5EndPoint.Port}",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true
				};
				Process torProcess = Process.Start(torProcessStartInfo);

				await Task.Delay(1000);
				if (!await IsTorRunningAsync(TorSocks5EndPoint))
				{
					throw new TorException("Could not automatically start Tor. Try running Tor manually.");
				}
			}
			catch (Exception ex)
			{
				throw new TorException("Could not automatically start Tor. Try running Tor manually.", ex);
			}
		}

		/// <param name="torSocks5EndPoint">Opt out Tor with null.</param>
		public static async Task<bool> IsTorRunningAsync(IPEndPoint torSocks5EndPoint)
		{
			using (var client = new TorSocks5Client(torSocks5EndPoint))
			{
				try
				{
					await client.ConnectAsync();
					await client.HandshakeAsync();
				}
				catch (ConnectionException)
				{
					return false;
				}
				return true;
			}
		}
	}
}