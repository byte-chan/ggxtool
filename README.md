# ggxtool: a simple GGX archive unpacker and repacker

⚠ **This project is still in a very early stage of development. Use at your own risk!** ⚠

This tool supports:
-	very long file names
-	file names with non-ASCII characters (Windows-31J)
-	uncompressed (but "encrypted") files
-	<abbr title="byte pair encoding">BPE</abbr>-compressed files

This tool does **NOT** support:
-	non-uppercase file names (converted to uppercase for consistency with existing archives)
-	addition and deletion of files without recompressing other files (again, very early stage of development)
-	<abbr title="Lempel–Ziv–Storer–Szymanski">LZSS</abbr>-compressed files (mentioned in debug messages, but never encountered in any archives)

So far, this tool is known to be compatible with archives for the following games:
-	Mamorukun Curse! (PS3) (NPUB30934, version 1.01)
-	Strike Witches: Hakugin no Tsubasa (Xbox 360) (CF-2003)

## Acknowledgements

This tool supports the GGX archive format, created by Osamu Chadani of Gulti Co., Ltd. (now Kayac Akiba Studio Co., Ltd.) in 2007.

For compression and decompression of archived files, this tool uses a C# adaptation of the byte pair encoding (BPE) algorithm described and implemented by Philip Gage in ["A New Algorithm for Data Compression"][1].

## Using ggxtool

The following operations are supported by ggxtool:
-	adding files to archives: `ggxtool a <archive> [files...]`
	-	with no files specified, ggxtool repacks the archive, sometimes achieving a slightly better compression ratio
-	deleting files from archives: `ggxtool d <archive> [files...]`
-	listing files in archives: `ggxtool l <archive> [files...]`
	-	with no files specified, all files are listed
	-	with some files specified, only those files (or files in those directories) are listed
		-	`ggxtool l archive.bin SOUND VOICE` lists files in the archive's `SOUND` and `VOICE` directories (if they exist)
-	extracting files from archives: `ggxtool x <archive> [files...]`
	-	with no files specified, all files are extracted
	-	with some files specified, only those files (or files in those directories) are extracted

All operations write the names of affected files to standard output.

## The archive format

All fields are stored in little-endian byte order. This tool uses .NET's [BinaryReader][2] and should work on big-endian systems.

Because the reference implementation did it first, file names are encoded using the [Windows code page 932][3], also known as Windows-31J, which is a variant of the Shift JIS encoding.

### Header

Supported archives are identified by the 16-byte string `"GGXArchiver1.00\x00"`.
This string is followed by the number of name entries (4 bytes) and files (also 4 bytes).
The full header is padded to 32 bytes.

The following `hexdump -C` output shows the first 256 bytes of an archive containing this project at an earlier stage of development.

```
00000000  47 47 58 41 72 63 68 69  76 65 72 31 2e 30 30 00  |GGXArchiver1.00.|
00000010  28 04 00 00 e2 01 00 00  00 00 00 00 00 00 00 00  |(...............|
```

This archive contains 1064 name entries (`28 04 00 00`) and 482 files (`e2 01 00 00`).

### Names

The GGX format stores 32-byte-aligned null-terminated file names.
For unknown reasons (likely alignment), file names are actually terminated by **four** null bytes, and these null bytes have to be accounted for when padding file names to the next 32-byte boundary.
The order of file names may not line up with the order of file entries.
Archives written by this tool *do* store file names in the same order as files.

The following continuation of the `hexdump -C` output from the preceding section shows some file names as they are stored in  the archive.

```
00000020  41 52 43 48 49 56 45 2e  43 53 00 00 00 00 00 00  |ARCHIVE.CS......|
00000030  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  |................|
00000040  42 49 4e 5c 44 45 42 55  47 5c 4e 45 54 38 2e 30  |BIN\DEBUG\NET8.0|
00000050  5c 47 47 58 2e 44 4c 4c  00 00 00 00 00 00 00 00  |\GGX.DLL........|
00000060  42 49 4e 5c 44 45 42 55  47 5c 4e 45 54 38 2e 30  |BIN\DEBUG\NET8.0|
00000070  5c 47 47 58 2e 52 55 4e  54 49 4d 45 43 4f 4e 46  |\GGX.RUNTIMECONF|
00000080  49 47 2e 4a 53 4f 4e 00  00 00 00 00 00 00 00 00  |IG.JSON.........|
00000090  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  |................|
000000a0  42 49 4e 5c 44 45 42 55  47 5c 4e 45 54 38 2e 30  |BIN\DEBUG\NET8.0|
000000b0  5c 4c 49 4e 55 58 2d 58  36 34 5c 53 59 53 54 45  |\LINUX-X64\SYSTE|
000000c0  4d 2e 52 55 4e 54 49 4d  45 2e 49 4e 54 45 52 4f  |M.RUNTIME.INTERO|
000000d0  50 53 45 52 56 49 43 45  53 2e 4a 41 56 41 53 43  |PSERVICES.JAVASC|
000000e0  52 49 50 54 2e 44 4c 4c  00 00 00 00 00 00 00 00  |RIPT.DLL........|
000000f0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  |................|
```

-	`ARCHIVE.CS`: 14 bytes (including null terminators), 1 entry
-	`BIN\DEBUG\NET8.0\GGX.DLL`: 28 bytes, 1 entry
-	`BIN\DEBUG\NET8.0\GGX.RUNTIMECONFIG.JSON`: 43 bytes, 2 entries
-	`BIN\DEBUG\NET8.0\LINUX-X64\SYSTEM.RUNTIME.INTEROPSERVICES.JAVASCRIPT.DLL`: 76 bytes, 3 entries

### Files

Each file entry is 24 bytes, or six 32-bit integer fields. These fields are:

-	name index: the index into the preceding list of name entries
-	name length: the number of entries occupied by the file name
-	raw/uncompressed size: the uncompressed file size in bytes
-	stored/compressed size: the size of the stored file data in bytes
-	compression method: the compression method used for the file
	-	0: uncompressed (but "encrypted")
	-	1: BPE
	-	*unknown value*: LZSS? (nothing is known about this except for the fact that the reference implementation seems to support it; if you encounter any unreadable archives, please open an issue!)
-	data offset: the offset of the file's stored data relative to the end of the file list; should be 0 for the first file

The following output shows the file entries for the first 4 files.

```
00008520  00 00 00 00 01 00 00 00  23 1b 00 00 91 0b 00 00  |........#.......|
00008530  01 00 00 00 00 00 00 00  01 00 00 00 01 00 00 00  |................|
00008540  00 3e 00 00 91 28 00 00  01 00 00 00 91 0b 00 00  |.>...(..........|
00008550  02 00 00 00 02 00 00 00  01 01 00 00 c7 00 00 00  |................|
00008560  01 00 00 00 22 34 00 00  04 00 00 00 03 00 00 00  |...."4..........|
00008570  a0 98 00 00 97 64 00 00  01 00 00 00 e9 34 00 00  |.....d.......4..|
```

-	`00 00 00 00` `01 00 00 00` `23 1b 00 00` `91 0b 00 00` `01 00 00 00` `00 00 00 00`
	-	name at index 0, 1 entry
	-	raw size 6947 bytes, stored size 2961 bytes
	-	compression method 1 (BPE)
	-	data at offset 0
-	`01 00 00 00` `01 00 00 00` `00 3e 00 00` `91 28 00 00` `01 00 00 00` `91 0b 00 00`
	-	name at index 1, 1 entry
	-	raw size 15872 bytes, stored size 10385 bytes
	-	compression method 1 (BPE)
	-	data at offset 2961
-	`02 00 00 00` `02 00 00 00` `01 01 00 00` `c7 00 00 00` `01 00 00 00` `22 34 00 00`
	-	name at index 2, 2 entries
	-	raw size 257 bytes, stored size 199 bytes
	-	compression method 1 (BPE)
	-	data at offset 13346
-	`04 00 00 00` `03 00 00 00` `a0 98 00 00` `97 64 00 00` `01 00 00 00` `e9 34 00 00`
	-	name at index 4, 3 entries
	-	raw size 39072 bytes, stored size 13545 bytes
	-	compression method 1 (BPE)
	-	data at offset 13545

### Compressed data (BPE)

The format for BPE-compressed data is exactly the same as [Gage's description of the format][1].
Any implementation of the original BPE algorithm should be able to decompress data from GGX archives.
This includes Gage's reference implementation, which may crash if decompression overflows the 30-byte stack.
The reference implementation also features a potentially useless out-of-bounds memory access.

### Uncompressed data

Some file formats, particularly lossy audio formats, do not benefit from BPE compression.
For those formats, GGX archives can contain **uncompressed** files.

Because storing data in an easily accessible format is no good, these uncompressed files are ~~encrypted~~ masked with a 256-byte block of data.

```
00000000  28 43 29 32 30 30 37 47  55 4c 54 49 43 4f 20 20  |(C)2007GULTICO  |
00000010  47 47 58 20 41 72 63 68  69 76 65 20 20 20 20 20  |GGX Archive     |
00000020  53 79 73 74 65 6d 2e 20  43 72 65 61 74 65 64 20  |System. Created |
00000030  42 79 20 4f 73 61 6d 75  20 43 68 61 64 61 6e 69  |By Osamu Chadani|
00000040  39 46 34 43 33 41 30 42  36 35 37 45 38 31 32 44  |9F4C3A0B657E812D|
00000050  23 7e 25 3d 5d 28 5b 5e  26 27 7c 29 24 21 2d 40  |#~%=]([^&'|)$!-@|
00000060  35 39 44 38 43 32 45 41  33 46 34 37 31 30 36 42  |59D8C2EA3F47106B|
00000070  24 7e 40 25 5b 28 2d 5e  26 21 23 3d 7c 5d 29 27  |$~@%[(-^&!#=|])'|
00000080  33 34 44 36 32 30 35 37  46 39 38 41 43 42 31 45  |34D62057F98ACB1E|
00000090  24 40 23 3d 5d 2d 5e 26  27 7c 25 28 5b 7e 21 29  |$@#=]-^&'|%([~!)|
000000a0  34 36 44 31 42 37 30 33  45 32 46 38 41 39 43 35  |46D1B703E2F8A9C5|
000000b0  40 25 3d 2d 7e 5b 26 24  27 5d 23 5e 28 29 7c 21  |@%=-~[&$']#^()|!|
000000c0  64 6c 6f 6a 66 63 6e 6d  68 69 61 65 6b 70 62 67  |dlojfcnmhiaekpbg|
000000d0  36 42 32 38 43 35 31 37  44 41 30 33 46 45 39 34  |6B28C517DA03FE94|
000000e0  6f 69 6c 6a 6b 6e 61 70  68 62 6d 67 64 65 66 63  |oiljknaphbmgdefc|
000000f0  5b 3d 7e 25 28 26 21 2d  23 5e 40 29 5d 27 7c 24  |[=~%(&!-#^@)]'|$|
```

Additionally, each byte of that block is ORed with a fixed byte derived from a file's size (`size / 256 + 5`). For a hypothetical 64000-byte file, each byte of that block would be ORed with `0xFF`.

The offset into that block does **not** start over for each uncompressed file.
For several hypothetical uncompressed 64-byte files, the first file would be XORed with the first 64 bytes (OR `0x05`), the second file with the next 64 bytes, and so on. This offset only advances for uncompressed files.

[1]: http://www.pennelynn.com/Documents/CUJ/HTML/94HTML/19940045.HTM
[2]: https://learn.microsoft.com/dotnet/api/system.io.binaryreader
[3]: https://en.wikipedia.org/wiki/Code_page_932_(Microsoft_Windows)
