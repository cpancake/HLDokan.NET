using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;

namespace HLDokan.Net
{
	public class PackageSystem
	{
		public PackageNode RootNode => _rootNode;

		private PackageNode _rootNode;

		public PackageSystem(List<string> packages)
		{
			_rootNode = new PackageNode() { Name = "root", Directory = true };

			HLLib.hlInitialize();

			foreach(var package in packages)
			{
				if(Directory.Exists(package))
				{
					FindDirFiles(package, _rootNode);
					continue;
				}

				HLLib.HLPackageType type = HLLib.hlGetPackageTypeFromName(package);

				uint packagePointer;
				if(!HLLib.hlCreatePackage(type, out packagePointer))
				{
					throw new Exception("Can't load package: " + HLLib.hlGetString(HLLib.HLOption.HL_ERROR_SHORT_FORMATED));
				}

				HLLib.hlBindPackage(packagePointer);

				if(!HLLib.hlPackageOpenFile(package, (uint)HLLib.HLFileMode.HL_MODE_READ))
				{
					throw new Exception("Can't load package: " + HLLib.hlGetString(HLLib.HLOption.HL_ERROR_SHORT_FORMATED));
				}

				_rootNode.FileSize = 0;
				var rootItems = HLLib.hlFolderGetCount(HLLib.hlPackageGetRoot());
				for(uint i = 0; i < rootItems; i++)
				{
					var item = HLLib.hlFolderGetItem(HLLib.hlPackageGetRoot(), i);
					uint size;
					HLLib.hlItemGetSize(item, out size);
					_rootNode.FileSize += size;
				}
				FindFiles(HLLib.hlPackageGetRoot(), _rootNode, packagePointer);

				//HLLib.hlPackageClose();
			}
		}

		public PackageNode GetNode(string path)
		{
			var parts = path.Split('\\');
			if(parts.Length <= 1 || string.IsNullOrEmpty(parts[0]) && string.IsNullOrEmpty(parts[1]))
			{
				return _rootNode;
			}

			var currentNode = _rootNode;
			for(var i = 1; i < parts.Length; i++)
			{
				if(currentNode.Directories.ContainsKey(parts[i]))
				{
					currentNode = currentNode.Directories[parts[i]];
				}
				else if(currentNode.Files.ContainsKey(parts[i]))
				{
					currentNode = currentNode.Files[parts[i]];
				}
				else
				{
					return null;
				}
			}

			return currentNode;
		}

		private void FindFiles(IntPtr dir, PackageNode node, uint package)
		{
			uint count = HLLib.hlFolderGetCount(dir);
			for(uint i = 0; i < count; i++)
			{
				var item = HLLib.hlFolderGetItem(dir, i);
				var itemNode = new PackageNode();
				itemNode.Name = HLLib.hlItemGetName(item).ToLower();
				itemNode.Directory = HLLib.hlItemGetType(item) == HLLib.HLDirectoryItemType.HL_ITEM_FOLDER;
				
				var pathPtr = Marshal.AllocHGlobal(255);
				HLLib.hlItemGetPath(item, pathPtr, 255);
				var pathBuffer = new byte[255];
				Marshal.Copy(pathPtr, pathBuffer, 0, 255);
				itemNode.Path = Encoding.ASCII.GetString(pathBuffer).TrimEnd('\0').ToLower();

				uint filesize;
				HLLib.hlItemGetSize(item, out filesize);
				itemNode.FileSize = filesize;

				if(itemNode.Directory)
				{
					if(node.Directories.ContainsKey(itemNode.Name))
					{
						FindFiles(item, node.Directories[itemNode.Name], package);
					}
					else
					{
						FindFiles(item, itemNode, package);
						node.Directories[itemNode.Name] = itemNode;
					}
				}
				else
				{
					itemNode.FilePtr = item;
					itemNode.FilePackage = package;
					node.Files[itemNode.Name] = itemNode;
				}
			}
		}

		private void FindDirFiles(string dir, PackageNode node)
		{
			foreach(var dirName in Directory.EnumerateDirectories(dir))
			{
				var dirNode = new PackageNode();
				dirNode.Name = Path.GetFileName(dirName);
				dirNode.Path = Path.Combine(dir, dirNode.Name);
				dirNode.Directory = true;
				dirNode.InPackage = false;

				FindDirFiles(dirNode.Path, dirNode);
				node.FileSize += dirNode.FileSize;
				node.Directories[dirNode.Name] = dirNode;
			}

			foreach(var fileName in Directory.EnumerateFiles(dir))
			{
				var fileNode = new PackageNode();
				fileNode.Name = Path.GetFileName(fileName);
				fileNode.Path = Path.Combine(dir, fileNode.Name);
				fileNode.Directory = false;
				fileNode.InPackage = false;
				fileNode.FileSize = new FileInfo(fileNode.Path).Length;

				node.FileSize += fileNode.FileSize;
				node.Files[fileNode.Name] = fileNode;
			}
		}
	}

	public class PackageNode
	{
		public string Name;
		public string Path;
		public bool Directory;
		public Dictionary<string, PackageNode> Files = new Dictionary<string, PackageNode>();
		public Dictionary<string, PackageNode> Directories = new Dictionary<string, PackageNode>();
		public long FileSize = 0;
		public bool InPackage = true;

		public IntPtr FilePtr;
		public uint FilePackage;

		public Stream CreateStream()
		{
			if(InPackage)
			{
				return new PackageFileStream(FilePtr);
			}
			else
			{
				try
				{
					return File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				}catch(Exception e)
				{
					Console.WriteLine(e.Message);
					return null;
				}
			}
		}
	}
}