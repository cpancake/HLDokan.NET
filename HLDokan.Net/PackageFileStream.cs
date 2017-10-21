using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

namespace HLDokan.Net
{
	public class PackageFileStream : Stream
	{
		private IntPtr _filePtr;
		private IntPtr _streamPtr;
		private bool _disposed = false;
		private long _position = 0;
		private long _size = 0;

		public override long Position
		{
			get
			{
				return _position;
			}
			set
			{
				Seek(value, SeekOrigin.Begin);
			}
		}

		public override long Length => _size;

		public override bool CanRead => true;

		public override bool CanSeek => true;

		public override bool CanTimeout => false;

		public override bool CanWrite => false;

		public PackageFileStream(IntPtr filePtr)
		{
			_filePtr = filePtr;
			if(!HLLib.hlFileCreateStream(filePtr, out _streamPtr))
			{
				throw new Exception("Unable to create stream.");
			}

			if(!HLLib.hlStreamOpen(_streamPtr, (uint)HLLib.HLFileMode.HL_MODE_READ))
			{
				throw new Exception("Unable to open stream.");
			}
			_size = (long)HLLib.hlStreamGetStreamSizeEx(_streamPtr);
		}

		~PackageFileStream()
		{
			Dispose(false);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			IntPtr dataPtr = Marshal.AllocHGlobal((int)(_size > count ? count : _size));

			HLLib.hlStreamRead(_streamPtr, dataPtr, (uint)(_size > count ? count : _size));

			unsafe
			{
				var source = (byte*)dataPtr;
				fixed (byte* dest = buffer)
				{
					for(int i = offset; i < (_size - offset > count ? count : _size - offset); i++)
					{
						dest[i] = source[i];
					}
				}
			}

			Marshal.FreeHGlobal(dataPtr);
			return (int)(_size - offset > count ? count : _size - offset);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{

		}

		public override void Flush()
		{

		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			var seekMode = HLLib.HLSeekMode.HL_SEEK_BEGINNING;
			if(origin == SeekOrigin.Current)
			{
				seekMode = HLLib.HLSeekMode.HL_SEEK_CURRENT;
				_position = _position + offset;
			}
			else if(origin == SeekOrigin.End)
			{
				seekMode = HLLib.HLSeekMode.HL_SEEK_END;
				_position = _position - offset;
			}
			else
			{
				_position = offset;
			}

			return (long)HLLib.hlStreamSeekEx(_streamPtr, offset, seekMode);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if(!_disposed)
			{
				HLLib.hlStreamClose(_streamPtr);
			}
		} 
	}
}
