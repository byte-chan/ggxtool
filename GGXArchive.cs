using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

enum GGXCompression {
	None = 0,
	BPE = 1
}

struct ArchiveFile(GGXCompression compression, byte[] data) {
	public GGXCompression compression = compression;
	public byte[] data = data;
}

class GGXArchive {
	public static readonly byte[] magicData = [
		0x28, 0x43, 0x29, 0x32, 0x30, 0x30, 0x37, 0x47, 0x55, 0x4C, 0x54, 0x49, 0x43, 0x4F, 0x20, 0x20, // "(C)2007GULTICO  "
		0x47, 0x47, 0x58, 0x20, 0x41, 0x72, 0x63, 0x68, 0x69, 0x76, 0x65, 0x20, 0x20, 0x20, 0x20, 0x20, // "GGX Archive     "
		0x53, 0x79, 0x73, 0x74, 0x65, 0x6D, 0x2E, 0x20, 0x43, 0x72, 0x65, 0x61, 0x74, 0x65, 0x64, 0x20, // "System. Created "
		0x42, 0x79, 0x20, 0x4F, 0x73, 0x61, 0x6D, 0x75, 0x20, 0x43, 0x68, 0x61, 0x64, 0x61, 0x6E, 0x69, // "By Osamu Chadani"
		0x39, 0x46, 0x34, 0x43, 0x33, 0x41, 0x30, 0x42, 0x36, 0x35, 0x37, 0x45, 0x38, 0x31, 0x32, 0x44, // "9F4C3A0B657E812D"
		0x23, 0x7E, 0x25, 0x3D, 0x5D, 0x28, 0x5B, 0x5E, 0x26, 0x27, 0x7C, 0x29, 0x24, 0x21, 0x2D, 0x40, // "#~%=]([^&'|)$!-@"
		0x35, 0x39, 0x44, 0x38, 0x43, 0x32, 0x45, 0x41, 0x33, 0x46, 0x34, 0x37, 0x31, 0x30, 0x36, 0x42, // "59D8C2EA3F47106B"
		0x24, 0x7E, 0x40, 0x25, 0x5B, 0x28, 0x2D, 0x5E, 0x26, 0x21, 0x23, 0x3D, 0x7C, 0x5D, 0x29, 0x27, // "$~@%[(-^&!#=|])'"
		0x33, 0x34, 0x44, 0x36, 0x32, 0x30, 0x35, 0x37, 0x46, 0x39, 0x38, 0x41, 0x43, 0x42, 0x31, 0x45, // "34D62057F98ACB1E"
		0x24, 0x40, 0x23, 0x3D, 0x5D, 0x2D, 0x5E, 0x26, 0x27, 0x7C, 0x25, 0x28, 0x5B, 0x7E, 0x21, 0x29, // "$@#=]-^&'|%([~!)"
		0x34, 0x36, 0x44, 0x31, 0x42, 0x37, 0x30, 0x33, 0x45, 0x32, 0x46, 0x38, 0x41, 0x39, 0x43, 0x35, // "46D1B703E2F8A9C5"
		0x40, 0x25, 0x3D, 0x2D, 0x7E, 0x5B, 0x26, 0x24, 0x27, 0x5D, 0x23, 0x5E, 0x28, 0x29, 0x7C, 0x21, // "@%=-~[&$']#^()|!"
		0x64, 0x6C, 0x6F, 0x6A, 0x66, 0x63, 0x6E, 0x6D, 0x68, 0x69, 0x61, 0x65, 0x6B, 0x70, 0x62, 0x67, // "dlojfcnmhiaekpbg"
		0x36, 0x42, 0x32, 0x38, 0x43, 0x35, 0x31, 0x37, 0x44, 0x41, 0x30, 0x33, 0x46, 0x45, 0x39, 0x34, // "6B28C517DA03FE94"
		0x6F, 0x69, 0x6C, 0x6A, 0x6B, 0x6E, 0x61, 0x70, 0x68, 0x62, 0x6D, 0x67, 0x64, 0x65, 0x66, 0x63, // "oiljknaphbmgdefc"
		0x5B, 0x3D, 0x7E, 0x25, 0x28, 0x26, 0x21, 0x2D, 0x23, 0x5E, 0x40, 0x29, 0x5D, 0x27, 0x7C, 0x24, // "[=~%(&!-#^@)]'|$"
	];
	static readonly byte[] magic = "GGXArchiver1.00\0"u8.ToArray();
	static readonly Encoding shiftjis = Encoding.GetEncoding(932);
	readonly Dictionary<string, ArchiveFile> files = [];
	public IEnumerable<string> Files => files.Keys;
	public void Add(string path, byte[] data, GGXCompression? compression = null) {
		path = path.ToUpperInvariant().Replace('\\', Path.DirectorySeparatorChar);
		if(compression is null && path.EndsWith(".AT3")) {
			compression = GGXCompression.None;
		}
		files[path] = new(compression ?? GGXCompression.BPE, data);
	}
	public bool Remove(string path) => files.Remove(path.ToUpperInvariant());
	public void Clear() => files.Clear();
	public int Count => files.Count;
	public int Load(Stream input) {
		if(!input.CanSeek) throw new NotSupportedException("Stream is not seekable");
		input.Position = 0;
		using var reader = new BinaryReader(input, shiftjis, true);
		var header = reader.ReadBytes(16);
		if(!header.SequenceEqual(magic)) {
			throw new InvalidDataException("Invalid archive header");
		}
		var nameCount = reader.ReadUInt32();
		var fileCount = reader.ReadUInt32();
		var filesLoaded = 0;
		byte cryptOffset = 0;
		for(var i = 0; i < fileCount; i++) {
			input.Position = 32 + nameCount * 32 + i * 24;
			var nameIndex = reader.ReadInt32();
			var nameLength = reader.ReadInt32();
			var rawSize = reader.ReadInt32();
			var storedSize = reader.ReadInt32();
			var compression = (GGXCompression)reader.ReadInt32();
			var offset = 32 + nameCount * 32 + fileCount * 24 + reader.ReadInt32();
			input.Position = 32 + nameIndex * 32;
			var rawName = reader.ReadBytes(nameLength * 32);
			var name = shiftjis.GetString(rawName[..rawName.TakeWhile(c => c != 0).Count()]);
			input.Position = offset;
			var storedData = reader.ReadBytes(storedSize);
			byte[] rawData;
			switch(compression) {
				case GGXCompression.None: {
						var key = (byte)(storedSize / 256 + 5);
						rawData = storedData;
						for(var j = 0; j < storedSize; j++) {
							rawData[j] ^= (byte)(magicData[cryptOffset++] | key);
						}
						break;
					}
				case GGXCompression.BPE: {
						rawData = new byte[rawSize];
						using var ms1 = new MemoryStream(storedData);
						using var ms2 = new MemoryStream(rawData);
						BPE.Expand(ms1, ms2);
						if(ms2.Position != rawSize) {
							throw new InvalidDataException($"Size mismatch");
						}
						break;
					}
				default: throw new NotSupportedException($"Unsupported compression method {compression}");
			}
			Add(name, rawData, compression);
			filesLoaded++;
		}
		return filesLoaded;
	}
	public static GGXArchive LoadFrom(Stream input) {
		var arch = new GGXArchive();
		arch.Load(input);
		return arch;
	}
	public static GGXArchive LoadFrom(string path) {
		using var f = File.OpenRead(path);
		return LoadFrom(f);
	}
	record struct ArchiveSave(byte[] Name, byte[] Data, int RawSize, GGXCompression Compression);
	public void Save(Stream output) {
		var bpe = new BPE();
		using var writer = new BinaryWriter(output, shiftjis, true);
		writer.Write(magic);
		var zfiles = new List<ArchiveSave>(files.Count);
		var nameCount = 0;
		byte cryptOffset = 0;
		foreach(var (path, file) in files) {
			var p = path.Replace(Path.DirectorySeparatorChar, '\\');
			var name = new byte[(shiftjis.GetByteCount(p) + 35) & ~31];
			shiftjis.GetBytes(p, name);
			nameCount += name.Length / 32;
			byte[] zdata;
			var usize = file.data.Length;
			switch(file.compression) {
				case GGXCompression.None: {
						var key = (byte)(usize / 256 + 5);
						zdata = new byte[usize];
						for(var i = 0; i < usize; i++) {
							zdata[i] = (byte)(file.data[i] ^ (byte)(magicData[cryptOffset++] | key));
						}
						break;
					}
				case GGXCompression.BPE: {
						using var ms1 = new MemoryStream(file.data);
						using var ms2 = new MemoryStream();
						bpe.Compress(ms1, ms2);
						var zsize = ms2.Position;
						zdata = new byte[zsize];
						ms2.Position = 0;
						ms2.Read(zdata);
						break;
					}
				default: throw new NotSupportedException($"Unsupported compression method {file.compression}");
			}
			zfiles.Add(new(name, zdata, file.data.Length, file.compression));
		}
		writer.Write(nameCount);
		writer.Write(zfiles.Count);
		writer.Write(0);
		writer.Write(0);
		foreach(var f in zfiles) {
			writer.Write(f.Name);
		}
		nameCount = 0;
		int offset = 0;
		foreach(var f in zfiles) {
			writer.Write(nameCount);
			writer.Write(f.Name.Length / 32);
			writer.Write(f.RawSize);
			writer.Write(f.Data.Length);
			writer.Write((int)f.Compression);
			writer.Write(offset);
			nameCount += f.Name.Length / 32;
			offset += f.Data.Length;
		}
		foreach(var f in zfiles) {
			writer.Write(f.Data);
		}
	}
	public void SaveTo(string path) {
		using var f = File.Create(path);
		Save(f);
	}
	public ArchiveFile this[string path] {
		get => files[path];
		set => Add(path, value.data, value.compression);
	}
}
