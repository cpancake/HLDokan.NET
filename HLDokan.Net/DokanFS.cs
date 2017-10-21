using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DokanNet;
using Microsoft.Win32;
using FileAccess = DokanNet.FileAccess;
using System.IO;
using System.Security.AccessControl;

namespace HLDokan.Net
{
	public class DokanFS : IDokanOperations
	{
		public const string DESKTOP_INI =
@"[.ShellClassInfo]
ConfirmFileOp=0
NoSharing=1";

		public Dictionary<string, PackageSystem> Filesystems = new Dictionary<string, PackageSystem>();

		private PackageNode GetNode(string path)
		{
			var parts = path.ToLower().Split('\\');
			if(!Filesystems.ContainsKey(parts[1]))
			{
				return null;
			}

			if(parts.Last() == "*")
			{
				parts = parts.Take(parts.Count() - 1).ToArray();
			}

			return Filesystems[parts[1]].GetNode("\\" + string.Join("\\", parts.Skip(2)));
		}

		public void Cleanup(string filename, DokanFileInfo info)
		{
			(info.Context as Stream)?.Close();
			info.Context = null;
		}

		public void CloseFile(string filename, DokanFileInfo info)
		{
			(info.Context as Stream)?.Close();
			info.Context = null;
		}

		public NtStatus CreateFile(
			string filename,
			FileAccess access,
			FileShare share,
			FileMode mode,
			FileOptions options,
			FileAttributes attributes,
			DokanFileInfo info
		)
		{
			Console.WriteLine("CreateFile: " + filename);
			if(info.IsDirectory && mode == FileMode.CreateNew)
			{
				return DokanResult.AccessDenied;
			}

			if(filename == "\\" || Filesystems.ContainsKey(filename.TrimStart('\\')))
			{
				return DokanResult.Success;
			}

			var node = GetNode(filename);
			if(node == null)
			{
				return DokanResult.FileNotFound;
			}

			if(!node.Directory)
			{
				info.Context = node.CreateStream();
			}
			else
			{
				info.Context = new object();
			}

			return DokanResult.Success;
		}

		public NtStatus DeleteDirectory(string filename, DokanFileInfo info)
		{
			return DokanResult.Error;
		}

		public NtStatus DeleteFile(string filename, DokanFileInfo info)
		{
			return DokanResult.Error;
		}

		public NtStatus FlushFileBuffers(string filename, DokanFileInfo info)
		{
			return DokanResult.Error;
		}

		public NtStatus FindFiles(string filename, out IList<FileInformation> files, DokanFileInfo info)
		{
			Console.WriteLine("FindFiles: " + filename);
			files = new List<FileInformation>();
			if(filename == "\\")
			{
				foreach(var game in Filesystems.Keys)
				{
					files.Add(new FileInformation()
					{
						FileName = game,
						Attributes = FileAttributes.Directory,
						LastAccessTime = DateTime.Now,
						LastWriteTime = null,
						CreationTime = null,
						Length = Filesystems[game].RootNode.FileSize
					});
				}

				return DokanResult.Success;
			}

			var parent = GetNode(filename);
			if(parent == null)
			{
				return DokanResult.FileNotFound;
			}
			foreach(var node in parent.Directories.Values)
			{
				var fileInfo = new FileInformation()
				{
					FileName = node.Name,
					Attributes = FileAttributes.Directory,
					LastAccessTime = DateTime.Now,
					LastWriteTime = null,
					CreationTime = null,
					Length = node.FileSize
				};
				files.Add(fileInfo);
			}

			foreach(var node in parent.Files.Values)
			{
				var fileInfo = new FileInformation()
				{
					FileName = node.Name,
					Attributes = FileAttributes.Normal,
					LastAccessTime = DateTime.Now,
					LastWriteTime = null,
					CreationTime = null,
					Length = node.FileSize
				};
				files.Add(fileInfo);
			}

			return DokanResult.Success;
		}

		public NtStatus GetFileInformation(string filename, out FileInformation fileInfo, DokanFileInfo info)
		{
			Console.WriteLine("GetFileInformation: " + filename);
			fileInfo = new FileInformation { FileName = filename };
			if(filename.Split('\\').Count() < 2)
			{
				fileInfo.Attributes = FileAttributes.Directory;
				fileInfo.LastAccessTime = DateTime.Now;
				fileInfo.LastWriteTime = null;
				fileInfo.CreationTime = null;
				return DokanResult.Success;
			}

			var node = GetNode(filename);
			if(node == null)
			{
				return DokanResult.FileNotFound;
			}

			fileInfo.Attributes = node.Directory ? FileAttributes.Directory : FileAttributes.Normal;
			fileInfo.LastAccessTime = DateTime.Now;
			fileInfo.LastWriteTime = null;
			fileInfo.CreationTime = null;
			fileInfo.Length = node.FileSize;

			return DokanResult.Success;
		}

		public NtStatus LockFile(string filename, long offset, long length, DokanFileInfo info)
		{
			return DokanResult.Success;
		}

		public NtStatus UnlockFile(string filename, long offset, long length, DokanFileInfo info)
		{
			return DokanResult.Success;
		}

		public NtStatus MoveFile(string filename, string newName, bool replace, DokanFileInfo info)
		{
			return DokanResult.Error;
		}

		public NtStatus ReadFile(string filename, byte[] buffer, out int readBytes, long offset, DokanFileInfo info)
		{
			Console.WriteLine("ReadFile: " + filename);
			var node = GetNode(filename);
			if(node == null)
			{
				readBytes = 0;
				return DokanResult.FileNotFound;
			}

			if(offset > node.FileSize)
			{
				readBytes = 0;
				return DokanResult.Error;
			}

			Stream stream;
			if(info.Context != null)
			{
				stream = info.Context as Stream;
			}
			else
			{
				stream = node.CreateStream();
			}

			stream.Seek(offset, SeekOrigin.Begin);
			readBytes = stream.Read(buffer, 0, buffer.Length);

			return DokanResult.Success;
		}

		public NtStatus SetEndOfFile(string filename, long length, DokanFileInfo info)
		{
			return DokanResult.Error;
		}

		public NtStatus SetAllocationSize(string filename, long length, DokanFileInfo info)
		{
			return DokanResult.Error;
		}

		public NtStatus SetFileAttributes(string filename, FileAttributes attr, DokanFileInfo info)
		{
			return DokanResult.Error;
		}

		public NtStatus SetFileTime(
			string filename,
			DateTime? ctime,
			DateTime? atime,
			DateTime? mtime,
			DokanFileInfo info)
		{
			return DokanResult.Error;
		}

		public NtStatus Mounted(DokanFileInfo info)
		{
			return DokanResult.Success;
		}

		public NtStatus Unmounted(DokanFileInfo info)
		{
			return DokanResult.Success;
		}

		public NtStatus GetDiskFreeSpace(
			out long freeBytesAvailable,
			out long totalBytes,
			out long totalFreeBytes,
			DokanFileInfo info)
		{
			freeBytesAvailable = 0;
			totalBytes = Filesystems.Values.Select(a => a.RootNode.FileSize).Sum();
			totalFreeBytes = 0;
			return DokanResult.Success;
		}

		public NtStatus WriteFile(
			string filename,
			byte[] buffer,
			out int writtenBytes,
			long offset,
			DokanFileInfo info)
		{
			writtenBytes = 0;
			return DokanResult.Error;
		}

		public NtStatus GetVolumeInformation(
			out string volumeLabel, 
			out FileSystemFeatures features,
			out string fileSystemName, 
			DokanFileInfo info)
		{
			volumeLabel = "HLDokan";
			features = FileSystemFeatures.ReadOnlyVolume;
			fileSystemName = string.Empty;
			return DokanResult.Success;
		}

		public NtStatus GetFileSecurity(
			string fileName, 
			out FileSystemSecurity security, 
			AccessControlSections sections,
			DokanFileInfo info)
		{
			security = null;
			return DokanResult.Error;
		}

		public NtStatus SetFileSecurity(
			string fileName, 
			FileSystemSecurity security, 
			AccessControlSections sections,
			DokanFileInfo info)
		{
			return DokanResult.Error;
		}

		public NtStatus EnumerateNamedStreams(
			string fileName, 
			IntPtr enumContext, 
			out string streamName,
			out long streamSize, 
			DokanFileInfo info)
		{
			streamName = string.Empty;
			streamSize = 0;
			return DokanResult.NotImplemented;
		}

		public NtStatus FindStreams(
			string fileName, 
			out IList<FileInformation> streams, 
			DokanFileInfo info)
		{
			streams = new FileInformation[0];
			return DokanResult.NotImplemented;
		}

		public NtStatus FindFilesWithPattern(
			string fileName, 
			string searchPattern, 
			out IList<FileInformation> files,
			DokanFileInfo info)
		{
			files = new FileInformation[0];
			return DokanResult.NotImplemented;
		}
	}
}
