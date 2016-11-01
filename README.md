# regis3

This is a library that provides convenient support for registry file import/export in .NET applications.

# The basic concept

`regis3` provides an abstraction that allows you to import registry data from different formats, handle it, and export it to other formats. It looks something like this:

	[import] => [data] => [export]

Therefor this overview shows

1. how to import registry data
2. how to treat registry data
3. how to export registry data

# Importing registry data from files

This being a library that is supposed to "provides convenient support for registry file import/export in .NET applications", you will be shocked to know that it deals in files:

## Supported file formats

`regis3` supports three file formats:

- **Registry files** in the current format. Their main characteristics are:

 - the files are UTF16LE encoded
 - the files start with `Windows Registry Editor Version 5.00` 

- **Win9x/WinNT registry files** in the older format. Their main characteristics are:

 - the files are ANSI-encoded and thus have only limited support for international characters
 - the files start with the string `REGEDIT4`

- **XML registry files**. This is a `regis3`-specific XML file format. 

 - the files are UTF8 encoded
 - the files can be parsed in a traditional way

For all these three file formats, registry importers exist:

- `RegFileFormat5Importer` imports registry files
- `RegFileFormat4Importer` imports Win9x/WinNT registry files
- `XmlRegFileImporter` imports XML registry files.

However, what do you do when you don't know the actual file format used? You do this:

    var ri = RegFile.CreateImporterFromFile("your filename goes here", options);

where `options` is this bitmask:

    /// <summary>
    /// Parser options for .REG files
    /// </summary>
    [System.Flags]
    public enum RegFileImportOptions
    {
        /// <summary>
        /// No specific options
        /// </summary>
        None = 0,

        /// <summary>
        /// Allow Hashtag-style line comments.
        /// </summary>
        AllowHashtagComments = (1<<0),

        /// <summary>
        /// Allow Semicolon-style line comments.
        /// </summary>
        AllowSemicolonComments = (1<<1),

        /// <summary>
        /// If this option is set, the parser is more relaxed about whitespaces in the .REG file (Recommended, especially if you manually edit the file yourself.)
        /// </summary>
        IgnoreWhitespaces = (1<<2),

        /// <summary>
        /// If this option is set, a .REG file can have a statement like this:
        /// 
        /// "something"=dword:$$VARIABLE$$
        /// 
        /// where $$VARIABLE$$ is replaced at runtime with the respective -numeric- variable. 
        /// </summary>
        AllowVariableNamesForNonStringVariables = (1<<3),

    }
    
## Direct import

In addition to this, you can also import data directly from the registry:

- `RegistryImporter` can be used to import the registry directly. This allows you to

 - treat data from the registry in the same way you would treat data from a registry file
 - export data from the registry in any of the supported export formats
 - compare data from a registry file with data from a registry key

# Registry data representation

Registry data in memory will be represented by a `RegKeyEntry` object. The idiomatic use from files would be this:

    var ri = RegFile.CreateImporterFromFile("your filename goes here", options);
    var rk = ri.Import()
    
    // TODO: treat rk
    ...

The most important data in a RegKeyEntry is defined right at the top:

    /// <summary>
    /// This class represents a registry key in memory
    /// </summary>
    public class RegKeyEntry
    {
        /// <summary>
        /// Name of the key (not the complete path name: use the Path member for that)
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Parent key or null if this is a root key
        /// </summary>
        public RegKeyEntry Parent { get; protected set; }

        /// <summary>
        /// Subkeys relative to this key
        /// </summary>
        public readonly Dictionary<string, RegKeyEntry> Keys = new Dictionary<string, RegKeyEntry>();

        /// <summary>
        /// Values in this key
        /// </summary>
        public readonly Dictionary<string, RegValueEntry> Values = new Dictionary<string, RegValueEntry>();


For example,

- to enumerate subkeys of a  registry key, you enumerate the `Keys` dictionary
- to enumerate values in a registry key, you enumerate the `Values` dictionary

Registry values are a bit harder, because they can have different types: 

    /// <summary>
    /// This class represents a registry value in a 
    /// </summary>
    public class RegValueEntry
    {
        /// <summary>
        /// Name of this value. Warning: for default values this is going to be null.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Value data
        /// </summary>
        public object Value  { get; private set; }

        /// <summary>
        /// Type of data encoded in this object
        /// </summary>
        public RegValueEntryKind Kind { get; private set; }

So you need to implement logic to treat different types differently (for example, obviously a REG_DWORD is not the same as a REG_SZ)

# Exporting registry data

Now that you have manipulated registry data at will, what can you do with the resulting output? You can

1. write the results back to registry files
2. write the results directly to your local registry

## Exporting registry data to files

For all three file formats, registry exporters exist:

- `RegFileFormat5Exporter` exports registry files
- `RegFileFormat4Exporter` exports Win9x/WinNT registry files
- `XmlRegFileExporter` exports XML registry files.

All registry exporters implement the same interface:

    /// <summary>
    /// This is the export interface supported by all regis3 exporter functions: given a RegKeyEntry, create a file or a string.
    /// </summary>
    public interface IRegistryExporter
    {
        /// <summary>
        /// Given a registry key description, create a file
        /// </summary>
        /// <param name="key">Existing registry key description</param>
        /// <param name="filename">Filename to be created</param>
        /// <param name="options">Export options</param>
        void Export(RegKeyEntry key, string filename, RegFileExportOptions options);

        /// <summary>
        /// Given a registry key description, write a file to a stream
        /// </summary>
        /// <param name="key">Existing registry key description</param>
        /// <param name="file">Stream to be written to</param>
        /// /// <param name="options">Export options</param>
        void Export(RegKeyEntry key, TextWriter file, RegFileExportOptions options);
    }

## Exporting registry data to the registry

Sometimes you will want to write the data directly to your local registry. This is **not** done through one of the exporter interfaces, because we don't want to write to a string, or a file: we want to write to the registry. Therefor, the most convenient way is to take a `RegKeyEntry` instance and do this:

        /// <summary>
        /// Write the contents of this object back to the registry (possibly recursively)
        /// </summary>
        /// <param name="registryWriteOptions">Options for writing to the registry</param>
        /// <param name="env">Optional handler for environment variable replacement</param>
        /// <param name="registryView">Type of registry you want to see (32-bit, 64-bit, default).</param>
        public void WriteToTheRegistry(RegistryWriteOptions registryWriteOptions, RegEnvReplace env, RegistryView registryView)

The `RegistryWriteOptions` are straightforward:

    /// <summary>
    /// Available options when exporting a RegKeyEntry tree back to the registry
    /// </summary>
    [System.Flags]
    public enum RegistryWriteOptions
    {
        /// <summary>
        /// Export the data recursively. If omitted, export only the top level keys
        /// </summary>
        Recursive = (1<<0),

        /// <summary>
        /// Grant all access to everyone. I know I am lazy and one of these days hackers will probably take me down, but
        /// it is a lot easier this way ;)
        /// </summary>
        AllAccessForEveryone = (1<<1),
    }

The `RegEnvReplace` instance is basically a dictionary that allows you to lookup data on the fly and replace it when writing data back. For example, you can do this

	var rep = new RegEnvReplace()
    rep.Variables["INSTALLDIR"] = @"C:\marusha\somewhere\over\the\rainbow";

Then, if you have a registry string like this:

	"ApplName" : REG_SZ : "$$INSTALLDIR$$\\rave.exe"

the variable will be replaced when writing it back to the registry.



