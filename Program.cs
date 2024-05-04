using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

if(args.Length < 2) {
	Console.WriteLine("""
		Add specified files to archive:
		    a <archive> [files...]
		Delete specified files in archive:
		    d <archive> [files...]
		List specified files (or all) in archive:
		    l <archive> [files...]
		Extract specified files (or all) from archive:
		    x <archive> [files...]
		""");
	return 1;
}

int addFiles(string archivePath, IEnumerable<string> files) {
	var a = File.Exists(archivePath) ? GGXArchive.LoadFrom(archivePath) : new GGXArchive();
	void add(string path) {
		if(File.GetAttributes(path).HasFlag(FileAttributes.Directory)) {
			foreach(var f in Directory.EnumerateFileSystemEntries(path)) {
				add(f);
			}
		} else {
			Console.WriteLine(path);
			a.Add(path, File.ReadAllBytes(path));
		}
	}
	foreach(var path in files) add(path);
	a.SaveTo(archivePath);
	return 0;
}

int deleteFiles(string archivePath, IEnumerable<string> files) {
	var a = GGXArchive.LoadFrom(archivePath);
	var list = a.Files.Where(f => files.Any(m => f == m || f.StartsWith(m.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar)));
	foreach(var path in list) {
		Console.WriteLine(path);
		a.Remove(path);
	}
	a.SaveTo(archivePath);
	return 0;
}

int listFiles(string archivePath, IEnumerable<string> files) {
	var a = GGXArchive.LoadFrom(archivePath);
	var list = a.Files;
	if(files.Any()) {
		list = list.Where(f => files.Any(m => f == m || f.StartsWith(m.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar)));
	}
	foreach(var path in list) {
		Console.WriteLine(path);
	}
	return 0;
}

int extractFiles(string archivePath, IEnumerable<string> files) {
	var a = GGXArchive.LoadFrom(archivePath);
	var list = a.Files;
	if(files.Any()) {
		list = list.Where(f => files.Any(m => f == m || f.StartsWith(m.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar)));
	}
	foreach(var path in list) {
		Console.WriteLine(path);
		var dir = Path.GetDirectoryName(path);
		if(dir is not null and not "") Directory.CreateDirectory(dir);
		using var f = File.Create(path);
		f.Write(a[path].data);
	}
	return 0;
}

switch(args[0]) {
	case "a": return addFiles(args[1], args[2..]);
	case "d": return deleteFiles(args[1], args[2..]);
	case "l": return listFiles(args[1], args[2..]);
	case "x": return extractFiles(args[1], args[2..]);
	default: return 1;
}
