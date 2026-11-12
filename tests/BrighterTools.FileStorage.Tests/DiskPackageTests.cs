using BrighterTools.FileStorage.Disk;
using Xunit;
namespace BrighterTools.FileStorage.Tests;
public class DiskPackageTests {
 [Fact] public async Task Immutable_bounded_disk_upload_roundtrips_and_rejects_traversal() {
  var root=Path.Combine(Path.GetTempPath(),"bt-disk-test-"+Guid.NewGuid().ToString("N"));var disk=new DiskFileStorage(root);
  try { using var source=new MemoryStream([1,2,3]);var meta=await disk.CreateAsync("image.png","media",source,"image/png",10);
   Assert.Equal(3,meta.Length);Assert.Equal(new byte[]{1,2,3},await disk.DownloadBoundedAsync("image.png","media",10,TimeSpan.FromSeconds(5)));
   await Assert.ThrowsAsync<IOException>(()=>disk.CreateAsync("image.png","media",new MemoryStream([4]),"image/png",10));
   await Assert.ThrowsAsync<ArgumentException>(()=>disk.CreateAsync("../escape.png","media",new MemoryStream([4]),"image/png",10));
   await Assert.ThrowsAsync<IOException>(()=>disk.CreateAsync("large.png","media",new MemoryStream([1,2,3]),"image/png",2));
   Assert.False(File.Exists(Path.Combine(root,"media","large.png")));
  } finally { if(Directory.Exists(root))Directory.Delete(root,true); }
 }
}
