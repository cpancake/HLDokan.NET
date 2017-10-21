using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using NDesk.Options;
using DokanNet;
using System.IO;
using System.Runtime.InteropServices;

namespace HLDokan.Net
{
	class Program
	{
		[DllImport("kernel32.dll")]
		static extern bool CreateSymbolicLink(
		   string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

		enum SymbolicLink
		{
			File = 0,
			Directory = 1
		}

		static void Main(string[] args)
		{
			var showHelp = false;
			var packages = new List<string>();
			var mountpoint = "";
			var sdkLocation = "";

			var p = new OptionSet() {
				{ "h|help", "show this message", v => showHelp = v != null },
				{ "p|package=", "add a package to mount", v => packages.Add(v) },
				{ "m|mountpoint=", "set the mount point", v => mountpoint = v },
				{ "s|symlink=", "directory to symlink folders to (optional)", v => sdkLocation = v }
			};

			p.Parse(args);

			if(mountpoint == null)
			{
				Console.WriteLine("You must provide a mountpoint.");
				return;
			}

			if(mountpoint.Length == 1)
			{
				mountpoint = mountpoint.ToLower() + ":\\";
			}

			if(packages.Count == 0)
			{
				Console.WriteLine("You must specify at least one package to mount.");
				return;
			}

			Console.WriteLine("Mounting " + packages.Count + " packages to " + mountpoint);

			var gamePackages = new Dictionary<string, List<string>>();
			foreach(var package in packages)
			{
				var parts = package.Split(':');
				var game = parts[0].ToLower();
				var packageFile = string.Join(":", parts.Skip(1));
				if(!gamePackages.ContainsKey(game))
				{
					gamePackages[game] = new List<string>();
				}
				gamePackages[game].Add(packageFile);
			}

			var dokanFS = new DokanFS();
			foreach(var game in gamePackages.Keys)
			{
				dokanFS.Filesystems[game] = new PackageSystem(gamePackages[game]);
			}

			/*if(sdkLocation != "")
			{
				foreach(var game in dokanFS.Filesystems.Keys)
				{
					var path = Path.Combine(sdkLocation, game);
					if(Directory.Exists(path) && IsSymbolic(path))
					{
						Directory.Delete(path);
					}
					else if(Directory.Exists(path))
					{
						continue;
					}

					new Thread(() => {
						Thread.Sleep(10000);
						var created = CreateSymbolicLink(path, Path.Combine(mountpoint, game), SymbolicLink.Directory);
						if(created)
						{
							Console.WriteLine("created symlink to " + game);
						}
						else
						{
							Console.WriteLine("failed to create symlink to " + game);
						}
					}).Start();
				}
			}*/

			Console.WriteLine("Packages loaded");

			dokanFS.Mount(mountpoint, DokanOptions.DebugMode | DokanOptions.RemovableDrive | DokanOptions.StderrOutput);
		}

		static bool IsSymbolic(string path)
		{
			return new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint);
		}
	}
}
