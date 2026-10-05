using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiscUtils;
using DiscUtils.ExFat;
using DiscUtils.Ntfs;

namespace SEGADownloadTool_Sinmai;

public class Untie
{
	private readonly struct P1
	{
		private readonly byte[] a;

		internal byte e0 => a[13];

		internal byte e1 => a[14];

		internal bool e2 => a[15] != 0;

		internal string e3 => BV(a, 16, 4);

		internal string e4 => BV(a, 64, 3);

		internal ulong e5 => BinaryPrimitives.ReadUInt64LittleEndian(a.AsSpan(32));

		internal ulong e6 => BinaryPrimitives.ReadUInt64LittleEndian(a.AsSpan(40));

		internal ulong e7 => BinaryPrimitives.ReadUInt64LittleEndian(a.AsSpan(48));

		internal P1(byte[] raw)
		{
			a = raw;
		}
	}

	private sealed class P2 : Stream
	{
		private readonly Stream a;

		private readonly bool b;

		private readonly P1 c;

		private readonly byte[] d;

		private readonly byte[] e;

		private readonly Aes f;

		private readonly byte[] g = new byte[4096];

		private int h;

		private long i;

		private byte[]? j;

		internal P1 Z => c;

		public override long Length => checked((long)(c.e6 * (c.e5 - c.e7)));

		public override long Position
		{
			get
			{
				return i;
			}
			set
			{
				Seek(value, SeekOrigin.Begin);
			}
		}

		public override bool CanRead => true;

		public override bool CanSeek => true;

		public override bool CanWrite => false;

		internal P2(Stream input, string dir, bool leaveOpen = false, (byte[] key, byte[] iv)? manual = null)
		{
			a = input;
			b = leaveOpen;
			c = P0(input);
			byte e = c.e0;
			long position = checked((long)(c.e7 * c.e6));
			byte[] key;
			byte[] array;
			if (e == 2)
			{
				key = S4;
				array = S5;
			}
			else if (manual.HasValue)
			{
				key = (byte[])manual.Value.key.Clone();
				array = (byte[])manual.Value.iv.Clone();
			}
			else
			{
				(byte[], byte[])? tuple = B3((e == 0) ? c.e4 : c.e3);
				if (!tuple.HasValue)
				{
					throw new InvalidDataException("There were no decryption keys for this container.");
				}
				key = tuple.Value.Item1;
				array = tuple.Value.Item2;
			}
			if (c.e2 || array == null)
			{
				input.Position = position;
				this.e = B2(key, (e == 2) ? S1 : S0, BR(input, 4096L));
			}
			else
			{
				this.e = array;
			}
			d = key;
			input.Position = position;
			f = Aes.Create();
			f.Mode = CipherMode.CBC;
			f.Padding = PaddingMode.None;
			f.Key = d;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			BP(buffer, offset, count);
			int i;
			int num;
			for (i = 0; i < count; i += num)
			{
				num = Pull(buffer, offset + i, count - i);
				if (num <= 0)
				{
					break;
				}
			}
			return i;
		}

		private int Pull(byte[] buffer, int offset, int count)
		{
			if (j == null)
			{
				while ((long)h < 4096L)
				{
					int num = a.Read(g, h, (int)(4096L - (long)h));
					if (num <= 0)
					{
						break;
					}
					h += num;
				}
				long at = a.Position - checked((long)(c.e7 * c.e6)) - 4096;
				Span<byte> span = stackalloc byte[16];
				B1(at, e, span);
				f.IV = span.ToArray();
				using ICryptoTransform cryptoTransform = f.CreateDecryptor();
				byte[] array = new byte[h];
				Array.Copy(g, array, h);
				j = cryptoTransform.TransformFinalBlock(array, 0, h);
				h = 0;
			}
			if (j == null || j.Length == 0)
			{
				return 0;
			}
			int num2 = (int)(i % 4096);
			int num3 = Math.Min(j.Length - num2, count);
			if (num3 <= 0)
			{
				return 0;
			}
			Array.Copy(j, num2, buffer, offset, num3);
			i += num3;
			if (i % 4096 == 0L)
			{
				j = null;
			}
			return num3;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			long num;
			long num2;
			checked
			{
				num = (long)(c.e7 * c.e6);
				num2 = origin switch
				{
					SeekOrigin.Begin => offset, 
					SeekOrigin.Current => this.i + offset, 
					SeekOrigin.End => Length + offset, 
					_ => throw new ArgumentOutOfRangeException("origin"), 
				};
				if (num2 < 0)
				{
					throw new IOException("cannot seek before the start");
				}
			}
			if (this.i / 4096 == num2 / 4096)
			{
				this.i = num2;
				return num2;
			}
			j = null;
			a.Position = num + num2 / 4096 * 4096;
			this.i = num2 / 4096 * 4096;
			long num3 = num2 % 4096;
			if (num3 > 0)
			{
				byte[] array = new byte[num3];
				int num4;
				for (int i = 0; i < array.Length; i += num4)
				{
					num4 = Read(array, i, array.Length - i);
					if (num4 <= 0)
					{
						throw new EndOfStreamException();
					}
				}
			}
			return num2;
		}

		public override void Flush()
		{
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				f.Dispose();
				if (!b)
				{
					a.Dispose();
				}
				if (d != S4)
				{
					Array.Clear(d);
				}
				if (e != S5)
				{
					Array.Clear(e);
				}
				if (j != null)
				{
					Array.Clear(j);
				}
				j = null;
			}
			base.Dispose(disposing);
		}
	}

	private readonly struct P3
	{
		internal readonly Stream a;

		internal readonly ulong[]? b;

		internal readonly ulong c;

		internal bool d => b != null;

		internal P3(Stream stream, ulong[]? map, ulong unit)
		{
			a = stream;
			b = map;
			c = unit;
		}
	}

	private sealed class P4 : Stream
	{
		private readonly P3[] pool;

		private readonly long low;

		private readonly long full;

		private long at;

		private long size => full - low;

		public override long Length => size;

		public override long Position
		{
			get
			{
				return at;
			}
			set
			{
				Seek(value, SeekOrigin.Begin);
			}
		}

		public override bool CanRead => true;

		public override bool CanSeek => true;

		public override bool CanWrite => false;

		internal P4(params Stream[] parts)
		{
			if (parts.Length == 0)
			{
				throw new InvalidDataException("Not a valid VHD file");
			}
			pool = new P3[parts.Length];
			ulong num = 0uL;
			for (int i = 0; i < parts.Length; i++)
			{
				pool[i] = B8(parts[i], out var total);
				if (i == 0)
				{
					num = total;
				}
			}
			full = checked((long)num);
			low = (long)BC(pool, num);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			BP(buffer, offset, count);
			long num = size - at;
			if (num <= 0)
			{
				return 0;
			}
			int num2 = (int)Math.Min(count, num);
			int i;
			int num3;
			for (i = 0; i < num2; i += num3)
			{
				num3 = BB(pool, (ulong)full, (ulong)(low + at), buffer, offset + i, num2 - i);
				if (num3 <= 0)
				{
					break;
				}
				at += num3;
			}
			return i;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			long num = checked(origin switch
			{
				SeekOrigin.Begin => offset, 
				SeekOrigin.Current => at + offset, 
				SeekOrigin.End => size + offset, 
				_ => throw new ArgumentOutOfRangeException("origin"), 
			});
			if (num < 0)
			{
				throw new IOException("seek before start");
			}
			at = num;
			return at;
		}

		public override void Flush()
		{
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}
	}

	private const byte Q0 = 0;

	private const byte Q1 = 1;

	private const byte Q2 = 2;

	private const long Q3 = 4096L;

	private static readonly byte[] S2 = BW("09ca5efd30c9aaef3804d0a7e3fa7120");

	private static readonly byte[] S3 = BW("b155c22c2e7f0491fa7f0fdc217aff90");

	private static readonly Dictionary<string, (string a, string b)> S8 = new Dictionary<string, (string, string)>
	{
		["SDGB"] = ("7ca4e6b6f3d6e8b26472973887d7fa3a", "53fe7135762de3f97e7fe76b0fef3f27"),
		["SDGA"] = ("0a6610a62ef670c65b7e7b1750ffb7a1", "17a2a22915f81c5896edbba4c412585e"),
		["SDEZ"] = ("d136eba05d40e82682e6aad8d9e8688c", "c484deeaa0249ef46695f63694b7372f")
	};

	private const int QD = 446;

	private const byte QE = 7;

	private static readonly byte[] S6 = new byte[4] { 235, 82, 144, 78 };

	private static readonly ulong[] S7 = new ulong[4] { 0uL, 32256uL, 1048576uL, 512uL };

	private const int Q4 = 512;

	private const uint Q5 = uint.MaxValue;

	private const int Q6 = 60;

	private const int Q7 = 16;

	private const int Q8 = 68;

	private const int Q9 = 40;

	private const uint QA = 2u;

	private const uint QB = 3u;

	private const uint QC = 4u;

	private static readonly byte[] S0 = new byte[16]
	{
		235, 82, 144, 78, 84, 70, 83, 32, 32, 32,
		32, 0, 16, 1, 0, 0
	};

	private static readonly byte[] S1 = new byte[16]
	{
		235, 118, 144, 69, 88, 70, 65, 84, 32, 32,
		32, 0, 0, 0, 0, 0
	};

	private static readonly byte[] S4 = BW("5c84a9e726eaa5dd351f2b0750c23697");

	private static readonly byte[] S5 = BW("c063bf6f562d084d7963c987f5281761");

	public static void Run(string containerPath, Action<string>? log = null, string? baseHint = null, (byte[] key, byte[] iv)? keyHint = null)
	{
		if (log == null)
		{
			log = delegate
			{
			};
		}
		string fullPath = Path.GetFullPath(containerPath);
		string dir = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
		using FileStream input = File.OpenRead(fullPath);
		using P2 p = new P2(input, dir, leaveOpen: false, keyHint);
		P1 z = p.Z;
		byte e = z.e0;
		switch (e)
		{
		case 0:
		case 1:
			try
			{
				string text = B4(fullPath, z.e1, dir, keyHint);
				if (z.e1 == 0)
				{
					BD(text, BN(fullPath), log);
					BO(text, log);
					break;
				}
				string basePath;
				if (baseHint != null)
				{
					if (baseHint.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
					{
						using FileStream input2 = File.OpenRead(baseHint);
						if (P0(input2).e1 != 0)
						{
							throw new InvalidDataException("base container is not a seq0 app");
						}
						basePath = B4(baseHint, 0, dir, keyHint);
					}
					else
					{
						basePath = baseHint;
					}
				}
				else
				{
					basePath = B6(z.e3, dir, log, keyHint);
				}
				B7(basePath, text, fullPath, log);
				break;
			}
			catch (Exception ex2)
			{
				log("w0: " + ex2.Message);
				break;
			}
		case 2:
			try
			{
				BH(fullPath, dir, log);
				break;
			}
			catch (Exception ex)
			{
				log("w1: " + ex.Message);
				break;
			}
		default:
			log($"w2: {e}");
			break;
		}
	}

	internal static byte? BX(string path)
	{
		try
		{
			using FileStream input = File.OpenRead(path);
			P1 p = P0(input);
			byte e = p.e0;
			if (e != 0 && e != 1)
			{
				return null;
			}
			return p.e1;
		}
		catch
		{
			return null;
		}
	}

	internal static string? BZ(string path)
	{
		try
		{
			using FileStream input = File.OpenRead(path);
			P1 p = P0(input);
			byte e = p.e0;
			if (e != 0 && e != 1)
			{
				return null;
			}
			return (e == 0) ? p.e4 : p.e3;
		}
		catch
		{
			return null;
		}
	}

	internal static string? BY(string deltaPath, string? searchDir = null, (byte[] key, byte[] iv)? manual = null)
	{
		try
		{
			string fullPath = Path.GetFullPath(deltaPath);
			string text = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
			string dir = searchDir ?? text;
			using FileStream input = File.OpenRead(fullPath);
			return B6(P0(input).e3, dir, delegate
			{
			}, manual);
		}
		catch
		{
			return null;
		}
	}

	internal static bool ZK(string id)
	{
		return S8.ContainsKey(id);
	}

	private static P1 P0(Stream input)
	{
		byte[] array = BQ(input, 96);
		B0(S2, S3, array);
		P1 result = new P1(array);
		byte e = result.e0;
		if (e != 0 && e != 1 && e != 2)
		{
			throw new InvalidDataException($"Unknown container type {e}");
		}
		return result;
	}

	private static (byte[] p, byte[]? q)? B3(string gameId)
	{
		if (S8.TryGetValue(gameId, out (string, string) value))
		{
			return (BW(value.Item1), BW(value.Item2));
		}
		return null;
	}

	private static void B0(byte[] key, byte[] iv, byte[] data)
	{
		using Aes aes = Aes.Create();
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.None;
		aes.Key = key;
		aes.IV = iv;
		using ICryptoTransform cryptoTransform = aes.CreateDecryptor();
		byte[] array = cryptoTransform.TransformFinalBlock(data, 0, data.Length);
		if (array.Length != data.Length)
		{
			throw new InvalidDataException("failed to decrypt");
		}
		Array.Copy(array, data, array.Length);
	}

	private static void B1(long at, byte[] from, Span<byte> into)
	{
		for (int i = 0; i < 16; i++)
		{
			into[i] = (byte)(from[i] ^ (at >>> 8 * (i & 7)));
		}
	}

	private static byte[] B2(byte[] key, byte[] expected, byte[] source)
	{
		if (source.Length < 16)
		{
			throw new EndOfStreamException("container too small");
		}
		byte[] array = new byte[16];
		Array.Copy(source, array, 16);
		B0(key, expected, array);
		return array;
	}

	private static void BP(byte[] buffer, int offset, int count)
	{
		ArgumentNullException.ThrowIfNull(buffer, "buffer");
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("offset");
		}
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		if (buffer.Length - offset < count)
		{
			throw new ArgumentException("offset and count exceed buffer bounds");
		}
	}

	private static byte[] BQ(Stream s, int count)
	{
		byte[] array = new byte[count];
		int num;
		for (int i = 0; i < count; i += num)
		{
			num = s.Read(array, i, count - i);
			if (num <= 0)
			{
				throw new EndOfStreamException();
			}
		}
		return array;
	}

	private static byte[] BR(Stream s, long max)
	{
		byte[] array = new byte[max];
		int i;
		int num;
		for (i = 0; i < max; i += num)
		{
			num = s.Read(array, i, (int)(max - i));
			if (num <= 0)
			{
				break;
			}
		}
		if (i == array.Length)
		{
			return array;
		}
		byte[] array2 = new byte[i];
		Array.Copy(array, array2, i);
		return array2;
	}

	private static uint BS(byte[] b, int off)
	{
		return BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(off));
	}

	private static ulong BT(byte[] b, int off)
	{
		return ((ulong)BS(b, off) << 32) | BS(b, off + 4);
	}

	private static uint BU(byte[] b, int off)
	{
		return BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(off));
	}

	private static string BV(byte[] b, int off, int len)
	{
		return Encoding.ASCII.GetString(b.AsSpan(off, len)).TrimEnd('\0').TrimEnd();
	}

	private static byte[] BW(string s)
	{
		return Convert.FromHexString(s);
	}

	private static bool BJ(string name)
	{
		if (!name.StartsWith('$') && !(name == ".") && !(name == ".."))
		{
			return name == "System Volume Information";
		}
		return true;
	}

	private static string BM(string path)
	{
		string text = path.TrimEnd('\\');
		int num = text.LastIndexOf('\\');
		if (num < 0)
		{
			return text;
		}
		return text.Substring(num + 1);
	}

	private static string BN(string path)
	{
		return Path.ChangeExtension(path, null);
	}

	private static void BO(string path, Action<string> log)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception ex)
		{
			log("w9: " + ex.Message);
		}
	}

	private static string? BL(string dir, string prefix, string suffix)
	{
		foreach (string item in Directory.EnumerateFiles(dir))
		{
			string fileName = Path.GetFileName(item);
			if (fileName.StartsWith(prefix, StringComparison.Ordinal) && fileName.EndsWith(suffix, StringComparison.Ordinal))
			{
				return item;
			}
		}
		return null;
	}

	private static void BK(string path, DiscFileInfo info, bool isDirectory)
	{
		try
		{
			DateTime lastWriteTimeUtc = info.LastWriteTimeUtc;
			if (lastWriteTimeUtc.Year >= 1980)
			{
				DateTime lastAccessTimeUtc = info.LastAccessTimeUtc;
				if (lastAccessTimeUtc.Year < 1980)
				{
					lastAccessTimeUtc = lastWriteTimeUtc;
				}
				if (isDirectory)
				{
					Directory.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
					Directory.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
				}
				else
				{
					File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
					File.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
				}
			}
		}
		catch
		{
		}
	}

	private static void BI(DiscFileSystem fs, string fsDir, string outDir, bool stamp)
	{
		BI0(fs, fsDir, outDir, stamp);
		BI1(fs, fsDir, outDir);
	}

	private static void BI0(DiscFileSystem fs, string fsDir, string outDir, bool stamp)
	{
		foreach (string directory in fs.GetDirectories(fsDir, "*", SearchOption.TopDirectoryOnly))
		{
			string text = BM(directory);
			if (!BJ(text))
			{
				string text2 = Path.Combine(outDir, text);
				Directory.CreateDirectory(text2);
				BI(fs, directory, text2, stamp);
				if (stamp)
				{
					BK(text2, fs.GetFileInfo(directory), isDirectory: true);
				}
			}
		}
	}

	private static void BI1(DiscFileSystem fs, string fsDir, string outDir)
	{
		foreach (string file in fs.GetFiles(fsDir, "*", SearchOption.TopDirectoryOnly))
		{
			string text = BM(file);
			if (BJ(text))
			{
				continue;
			}
			string path = Path.Combine(outDir, text);
			using (Stream stream = fs.OpenFile(file, FileMode.Open, FileAccess.Read))
			{
				using FileStream destination = File.Create(path);
				stream.CopyTo(destination);
			}
			BK(path, fs.GetFileInfo(file), isDirectory: false);
		}
	}

	private static string B4(string imagePath, byte seq, string dir, (byte[] key, byte[] iv)? manual = null)
	{
		string text = $"internal_{seq}.vhd";
		string text2 = Path.ChangeExtension(imagePath, ".vhd");
		using FileStream input = File.OpenRead(imagePath);
		using P2 stream = new P2(input, dir, leaveOpen: false, manual);
		using NtfsFileSystem ntfsFileSystem = BG(stream);
		List<string> list = ntfsFileSystem.GetFiles("\\", "*", SearchOption.TopDirectoryOnly).Select(BM).ToList();
		string text3 = null;
		foreach (string item in list)
		{
			if (item == text)
			{
				text3 = item;
				break;
			}
		}
		if (text3 == null)
		{
			foreach (string item2 in list)
			{
				if (string.Equals(item2, text, StringComparison.OrdinalIgnoreCase))
				{
					text3 = item2;
					break;
				}
			}
		}
		if (text3 == null)
		{
			throw new FileNotFoundException("could not find VHD " + text);
		}
		using (Stream stream2 = ntfsFileSystem.OpenFile("\\" + text3, FileMode.Open, FileAccess.Read))
		{
			using FileStream destination = File.Create(text2);
			stream2.CopyTo(destination);
		}
		BK(text2, ntfsFileSystem.GetFileInfo("\\" + text3), isDirectory: false);
		return text2;
	}

	private static string? B6(string gameId, string dir, Action<string> log, (byte[] key, byte[] iv)? manual = null)
	{
		string prefix = gameId + "_";
		string text = BL(dir, prefix, "_0.vhd");
		if (text != null)
		{
			log("i0: " + text);
			return text;
		}
		string text2 = BL(dir, prefix, "_0.app");
		if (text2 != null)
		{
			log("i1: " + text2);
			try
			{
				byte e;
				using (FileStream input = File.OpenRead(text2))
				{
					e = P0(input).e1;
				}
				return B4(text2, e, dir, manual);
			}
			catch (Exception ex)
			{
				log("w3: " + ex.Message);
			}
		}
		log("w4: " + gameId);
		return null;
	}

	private static (byte[] a, byte[]? b, uint c) B5(string path)
	{
		using FileStream fileStream = File.OpenRead(path);
		long length = fileStream.Length;
		if (length < 512)
		{
			throw new InvalidDataException("Not a valid VHD file");
		}
		fileStream.Position = length - 512;
		byte[] array = BQ(fileStream, 512);
		if (!((ReadOnlySpan<byte>)array.AsSpan(0, 8)).SequenceEqual("conectix"u8))
		{
			throw new InvalidDataException("Not a valid VHD file");
		}
		byte[] item = array[68..84].ToArray();
		uint num = BS(array, 60);
		byte[] item2 = null;
		if (num == 4)
		{
			fileStream.Position = (long)BT(array, 16);
			byte[] array2 = BQ(fileStream, 1024);
			if (!((ReadOnlySpan<byte>)array2.AsSpan(0, 8)).SequenceEqual("cxsparse"u8))
			{
				throw new InvalidDataException("Invalid dynamic VHD header");
			}
			item2 = array2[40..56].ToArray();
		}
		return (a: item, b: item2, c: num);
	}

	private static void B7(string? basePath, string deltaPath, string deltaApp, Action<string> log)
	{
		if (basePath == null)
		{
			return;
		}
		byte[] item;
		try
		{
			item = B5(basePath).a;
		}
		catch (Exception ex)
		{
			log("w5: " + ex.Message);
			return;
		}
		List<(string, string)> list = new List<(string, string)> { (basePath, basePath) };
		bool? flag = null;
		try
		{
			(byte[], byte[], uint) tuple = B5(deltaPath);
			flag = tuple.Item2 != null && ((ReadOnlySpan<byte>)tuple.Item2.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)item);
		}
		catch (Exception ex2)
		{
			log("w6: " + ex2.Message);
		}
		if (flag == true)
		{
			list.Add((deltaPath, deltaApp));
		}
		else if (flag == false)
		{
			log("w7");
			log("i3: " + deltaApp);
		}
		for (int i = 1; i < list.Count; i++)
		{
			string[] array = new string[i + 1];
			for (int j = 0; j <= i; j++)
			{
				array[j] = list[j].Item1;
			}
			try
			{
				BE(array, BN(list[i].Item2), log);
			}
			catch (Exception ex3)
			{
				log("w8: " + ex3.Message);
			}
		}
		foreach (var item2 in list)
		{
			BO(item2.Item1, log);
		}
	}

	private static void BD(string vhdPath, string outputDir, Action<string> log)
	{
		log("i4: " + vhdPath);
		using FileStream fileStream = File.OpenRead(vhdPath);
		using P4 stream = new P4(fileStream);
		BF(stream, outputDir);
		log("i5: " + outputDir);
	}

	private static void BE(string[] chainPaths, string outputDir, Action<string> log)
	{
		log("i6: " + string.Join(" + ", chainPaths));
		List<Stream> list = new List<Stream>();
		try
		{
			for (int i = 0; i < chainPaths.Length; i++)
			{
				list.Add(File.OpenRead(chainPaths[i]));
			}
			using P4 stream = new P4(list.ToArray());
			BF(stream, outputDir);
			log("i5: " + outputDir);
		}
		finally
		{
			for (int j = 0; j < list.Count; j++)
			{
				list[j].Dispose();
			}
		}
	}

	private static void BF(Stream stream, string outputDir)
	{
		using NtfsFileSystem fs = BG(stream);
		Directory.CreateDirectory(outputDir);
		BI(fs, "\\", outputDir, stamp: true);
	}

	private static NtfsFileSystem BG(Stream stream)
	{
		NtfsFileSystem ntfsFileSystem = new NtfsFileSystem(stream);
		ntfsFileSystem.NtfsOptions.HideHiddenFiles = false;
		ntfsFileSystem.NtfsOptions.HideSystemFiles = false;
		return ntfsFileSystem;
	}

	private static void BH(string imagePath, string dir, Action<string> log)
	{
		string text = BN(imagePath);
		Directory.CreateDirectory(text);
		using FileStream input = File.OpenRead(imagePath);
		using P2 partitionStream = new P2(input, dir);
		using ExFatFileSystem fs = new ExFatFileSystem(partitionStream);
		BI(fs, "\\", text, stamp: false);
	}

	private static P3 B8(Stream s, out ulong total)
	{
		long length = s.Length;
		if (length < 512)
		{
			throw new InvalidDataException("Not a valid VHD file");
		}
		s.Position = length - 512;
		byte[] array = BQ(s, 512);
		if (!((ReadOnlySpan<byte>)array.AsSpan(0, 8)).SequenceEqual("conectix"u8))
		{
			throw new InvalidDataException("Not a valid VHD file");
		}
		uint num = BS(array, 60);
		switch (num)
		{
		case 2u:
			total = (ulong)(length - 512);
			return new P3(s, null, 0uL);
		default:
			throw new InvalidDataException($"Unsupported VHD type {num}");
		case 3u:
		case 4u:
		{
			s.Position = (long)BT(array, 16);
			byte[] array2 = BQ(s, 1024);
			if (!((ReadOnlySpan<byte>)array2.AsSpan(0, 8)).SequenceEqual("cxsparse"u8))
			{
				throw new InvalidDataException("Invalid dynamic VHD header");
			}
			ulong position = BT(array2, 16);
			uint num2 = BS(array2, 24);
			ulong num3 = BS(array2, 32);
			s.Position = (long)position;
			byte[] b = BQ(s, checked((int)(num2 * 4)));
			ulong[] array3 = new ulong[num2];
			for (int i = 0; i < num2; i++)
			{
				array3[i] = BS(b, i * 4);
			}
			total = num2 * num3;
			return new P3(s, array3, num3);
		}
		}
	}

	private static int B9(P3 layer, ulong off, ulong vsize, byte[] buf, int bufOff, int count)
	{
		if (off >= vsize)
		{
			return 0;
		}
		int num = (int)Math.Min((ulong)count, vsize - off);
		if (!layer.d)
		{
			layer.a.Position = (long)off;
			return layer.a.Read(buf, bufOff, num);
		}
		ulong[] b = layer.b;
		ulong num2 = off / layer.c;
		ulong num3 = off % layer.c;
		int num4 = (int)Math.Min((ulong)num, layer.c - num3);
		if (num2 >= (ulong)b.Length || b[num2] == uint.MaxValue)
		{
			Array.Clear(buf, bufOff, num4);
			return num4;
		}
		layer.a.Position = (long)(b[num2] * 512 + 512 + num3);
		return layer.a.Read(buf, bufOff, num4);
	}

	private static int? BA(P3 layer, ulong off, byte[] buf, int bufOff, int count)
	{
		if (!layer.d)
		{
			return null;
		}
		ulong[] b = layer.b;
		ulong num = off / layer.c;
		ulong num2 = off % layer.c;
		if (num >= (ulong)b.Length || b[num] == uint.MaxValue)
		{
			return null;
		}
		int count2 = Math.Min(count, (int)(512 - off % 512));
		long num3 = (long)(b[num] * 512);
		layer.a.Position = num3;
		byte[] array = BQ(layer.a, 512);
		int num4 = (int)(num2 / 512);
		if (((array[num4 / 8] >> 7 - num4 % 8) & 1) == 0)
		{
			return null;
		}
		layer.a.Position = num3 + 512 + (long)num2;
		return layer.a.Read(buf, bufOff, count2);
	}

	private static int BB(P3[] layers, ulong vsize, ulong off, byte[] buf, int bufOff, int count)
	{
		if (off >= vsize)
		{
			return 0;
		}
		int count2 = (int)Math.Min((ulong)count, vsize - off);
		for (int num = layers.Length - 1; num > 0; num--)
		{
			int? num2 = BA(layers[num], off, buf, bufOff, count2);
			if (num2.HasValue)
			{
				return num2.Value;
			}
		}
		return B9(layers[0], off, vsize, buf, bufOff, count2);
	}

	private static ulong BC(P3[] layers, ulong vsize)
	{
		byte[] array = new byte[4];
		if (vsize >= 512)
		{
			byte[] array2 = new byte[512];
			BB(layers, vsize, 0uL, array2, 0, 512);
			if (array2[510] == 85 && array2[511] == 170)
			{
				for (int i = 0; i < 4; i++)
				{
					int num = 446 + i * 16;
					if (array2[num + 4] != 7)
					{
						continue;
					}
					ulong num2 = (ulong)BU(array2, num + 8) * 512uL;
					if (num2 + 4 <= vsize)
					{
						BB(layers, vsize, num2, array, 0, 4);
						if (((ReadOnlySpan<byte>)array.AsSpan(0, 4)).SequenceEqual((ReadOnlySpan<byte>)S6))
						{
							return num2;
						}
					}
				}
			}
		}
		for (int j = 0; j < S7.Length; j++)
		{
			ulong num3 = S7[j];
			if (num3 + 4 <= vsize)
			{
				BB(layers, vsize, num3, array, 0, 4);
				if (((ReadOnlySpan<byte>)array.AsSpan(0, 4)).SequenceEqual((ReadOnlySpan<byte>)S6))
				{
					return num3;
				}
			}
		}
		throw new InvalidDataException("No NTFS partition found in VHD");
	}
}
