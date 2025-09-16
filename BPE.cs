namespace GGXTool;

using System;
using System.IO;

/* C# adaptation of compress.c and expand.c */
/* Original implementation copyright 1994 by Philip Gage */

#pragma warning disable IDE1006
/// <param name="blockSize">Maximum block size</param>
/// <param name="hashSize">Size of hash table</param>
/// <param name="maxChars">Char set per block</param>
/// <param name="threshold">Minimum pair count</param>
public class BPE(int blockSize = 10000, int hashSize = 8192, int maxChars = 220, int threshold = 3) {
	/// <summary>Data block</summary>
	readonly byte[] buffer = new byte[blockSize];
	/// <summary>Pair table</summary>
	readonly byte[] leftcode = new byte[256];
	/// <summary>Pair table</summary>
	readonly byte[] rightcode = new byte[256];
	/// <summary>Hash table</summary>
	readonly byte[] left = new byte[hashSize];
	/// <summary>Hash table</summary>
	readonly byte[] right = new byte[hashSize];
	/// <summary>Pair count</summary>
	readonly byte[] count = new byte[hashSize];
	/// <summary>Size of current data block</summary>
	int size;

	/// <summary>Return index of character pair in hash table<br/>
	/// Deleted nodes have count of 1 for hashing</summary>
	int lookup(byte a, byte b) {
		/* Compute hash key from both characters */
		int index = (a ^ (b << 5)) & (hashSize - 1);
		while((left[index] != a || right[index] != b) && count[index] != 0)
			index = (index + 1) & (hashSize - 1);

		left[index] = a;
		right[index] = b;
		return index;
	}

	/// <summary>Read next block from input file into buffer</summary>
	/// <returns>Whether EOF has been reached</returns>
	bool fileread(Stream input) {
		int c, index, used = 0;

		/* Reset hash table and pair table */
		for(c = 0; c < hashSize; c++)
			count[c] = 0;
		for(c = 0; c < 256; c++) {
			leftcode[c] = (byte)c;
			rightcode[c] = 0;
		}
		size = 0;

		/* Read data until full or few unused chars */
		while(size < blockSize && used < maxChars &&
				(c = input.ReadByte()) != -1) {
			if(size > 0) {
				index = lookup(buffer[size - 1], (byte)c);
				if(count[index] < 255) ++count[index];
			}
			buffer[size++] = (byte)c;

			/* Use rightcode to flag data chars found */
			if(rightcode[c] == 0) {
				rightcode[c] = 1;
				used++;
			}
		}
		return c == -1;
	}

	/// <summary>Write each pair table and data block to output</summary>
	int filewrite(Stream output) {
		int i, len, c = 0;
		int written = 0;

		/* For each character 0..255 */
		while(c < 256) {

			/* If not a pair code, count run of literals */
			if(c == leftcode[c]) {
				len = 1; c++;
				while(len < 127 && c < 256 && c == leftcode[c]) {
					len++; c++;
				}
				output.WriteByte((byte)(len + 127));
				written++;
				len = 0;
				if(c == 256) break;
			} else { /* Else count run of pair codes */
				len = 0; c++;
				while((len < 127 && c < 256 && c != leftcode[c]) ||
						(len < 125 && c < 254 && (c + 1) != leftcode[c + 1])) {
					len++; c++;
				}
				output.WriteByte((byte)len);
				written++;
				c -= len + 1;
			}

			/* Write range of pairs to output */
			for(i = 0; i <= len; i++) {
				output.WriteByte(leftcode[c]);
				written++;
				if(c != leftcode[c]) {
					output.WriteByte(rightcode[c]);
					written++;
				}
				c++;
			}
		}

		/* Write size bytes and compressed data block */
		output.WriteByte((byte)(size / 256));
		output.WriteByte((byte)(size % 256));
		output.Write(buffer, 0, size);
		return written + 2 + size;
	}

	/// <summary>Compress from input file to output file</summary>
	int compress(Stream infile, Stream outfile) {
		int leftch = 0, rightch = 0, code, oldsize;
		int index, r, w, best;
		bool done = false;
		int written = 0;

		/* Compress each data block until end of file */
		while(!done) {
			done = fileread(infile);
			code = 256;

			/* Compress this block */
			for(; ; ) {

				/* Get next unused char for pair code */
				for(code--; code >= 0; code--)
					if(code == leftcode[code] && rightcode[code] == 0) break;

				/* Must quit if no unused chars left */
				if(code < 0) break;

				/* Find most frequent pair of chars */
				for(best = 2, index = 0; index < hashSize; index++) {
					if(count[index] > best) {
						best = count[index];
						leftch = left[index];
						rightch = right[index];
					}
				}

				/* Done if no more compression possible */
				if(best < threshold) break;

				/* Replace pairs in data, adjust pair counts */
				oldsize = size - 1;
				for(w = 0, r = 0; r < oldsize; r++) {
					if(buffer[r] == leftch &&
						buffer[r + 1] == rightch) {

						if(r > 0) {
							index = lookup(buffer[w - 1], (byte)leftch);
							if(count[index] > 1) --count[index];
							index = lookup(buffer[w - 1], (byte)code);
							if(count[index] < 255) ++count[index];
						}
						if(r < oldsize - 1) {
							index = lookup((byte)rightch, buffer[r + 2]);
							if(count[index] > 1) --count[index];
							index = lookup((byte)code, buffer[r + 2]);
							if(count[index] < 255) ++count[index];
						}
						buffer[w++] = (byte)code;
						r++; size--;
					} else buffer[w++] = buffer[r];
				}
				buffer[w] = r < blockSize ? buffer[r] : (byte)0;

				/* Add to pair substitution table */
				leftcode[code] = (byte)leftch;
				rightcode[code] = (byte)rightch;

				/* Delete pair from hash table */
				index = lookup((byte)leftch, (byte)rightch);
				count[index] = 1;
			}
			written += filewrite(outfile);
		}
		return written;
	}

	/// <summary>Compress data from input to output</summary>
	public int Compress(Stream input, Stream output) {
		lock(this) return compress(input, output);
	}

	/// <summary>Decompress data from input to output</summary>
	public static int Expand(Stream input, Stream output) {
		Span<byte> left = stackalloc byte[256];
		Span<byte> right = stackalloc byte[256];
		Span<byte> stack = stackalloc byte[256];
		int c, count, i, size;
		int written = 0;

		/* Unpack each block until end of file */
		while((count = input.ReadByte()) != -1) {
			for(i = 0; i < 256; i++)
				left[i] = (byte)i;

			/* Read pair table */
			for(c = 0; ;) {

				/* Skip range of literal bytes */
				if(count > 127) {
					c += count - 127;
					count = 0;
				}
				if(c == 256) break;

				/* Read pairs, skip right if literal */
				for(i = 0; i <= count; i++, c++) {
					left[c] = (byte)input.ReadByte();
					if(c != left[c])
						right[c] = (byte)input.ReadByte();
				}
				if(c == 256) break;
				count = input.ReadByte();
			}

			/* Calculate packed data block size */
			size = 256 * input.ReadByte() + input.ReadByte();

			/* Unpack data block */
			for(i = 0; ;) {

				/* Pop byte from stack or read byte */
				if(i != 0) {
					c = stack[--i];
				} else {
					if(size-- == 0) break;
					c = input.ReadByte();
				}

				/* Output byte or push pair on stack */
				if(c == left[c]) {
					output.WriteByte((byte)c);
					written++;
				} else {
					stack[i++] = right[c];
					stack[i++] = left[c];
				}
			}
		}
		return written;
	}
}
