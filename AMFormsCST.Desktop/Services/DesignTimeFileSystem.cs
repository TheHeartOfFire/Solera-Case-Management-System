using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using IFileSystem = System.IO.Abstractions.IFileSystem;

namespace AMFormsCST.Desktop.Services;

public class DesignTimeFileSystem : IFileSystem, AMFormsCST.Core.Interfaces.Utils.IFileSystem
{
    private readonly MockFileSystem _mockFileSystem = new();

    public DesignTimeFileSystem()
    {
        _mockFileSystem.AddFile("C:\\temp\\design_time_file.formgen", new MockFileData("<formDef/>"));
        _mockFileSystem.AddFile("C:\\temp\\design_time_file.pdf", new MockFileData(""));
    }

    public IFile File => _mockFileSystem.File;
    public IDirectory Directory => _mockFileSystem.Directory;
    public IPath Path => _mockFileSystem.Path;

    public IDirectoryInfoFactory DirectoryInfo => _mockFileSystem.DirectoryInfo;

    public IDriveInfoFactory DriveInfo => _mockFileSystem.DriveInfo;

    public IFileInfoFactory FileInfo => _mockFileSystem.FileInfo;

    public IFileStreamFactory FileStream => _mockFileSystem.FileStream;

    public IFileSystemWatcherFactory FileSystemWatcher => _mockFileSystem.FileSystemWatcher;

    public IFileVersionInfoFactory FileVersionInfo => _mockFileSystem.FileVersionInfo;

    public IRandomAccess RandomAccess => _mockFileSystem.RandomAccess;

    public bool FileExists(string? path) => _mockFileSystem.FileExists(path);
    public string? GetDirectoryName(string? path) => _mockFileSystem.Path.GetDirectoryName(path);
    public string? GetFileNameWithoutExtension(string? path) => _mockFileSystem.Path.GetFileNameWithoutExtension(path);
    public string CombinePath(string path1, string path2) => _mockFileSystem.Path.Combine(path1, path2);

    public string? GetFileName(string? path) => _mockFileSystem.Path.GetFileName(path);
   
    public string ReadAllText(string path) => _mockFileSystem.File.ReadAllText(path);
    
    public void WriteAllText(string path, string contents) => _mockFileSystem.File.WriteAllText(path, contents);
    
    public void MoveFile(string sourceFileName, string destFileName) => _mockFileSystem.File.Move(sourceFileName, destFileName);
    
    public void CreateDirectory(string path) => _mockFileSystem.Directory.CreateDirectory(path);
   
    public IEnumerable<string> GetFiles(string path) => _mockFileSystem.Directory.GetFiles(path);
    
    public DateTime GetLastWriteTime(string path) => _mockFileSystem.File.GetLastWriteTime(path);
    
    public void DeleteFile(string path) => _mockFileSystem.File.Delete(path);
}