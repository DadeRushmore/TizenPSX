using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[DataContract]internal sealed class TransferFile {[DataMember(Name="path")]public string Path {get;set;}[DataMember(Name="size")]public long Size {get;set;}}
[DataContract]internal sealed class TransferGroup {[DataMember(Name="name")]public string Name {get;set;}[DataMember(Name="files")]public List<TransferFile> Files {get;set;}}
[DataContract]internal sealed class TransferManifest {[DataMember(Name="protocol")]public int Protocol {get;set;}[DataMember(Name="groups")]public List<TransferGroup> Groups {get;set;}}
[DataContract]internal sealed class PairResult {[DataMember(Name="token")]public string Token {get;set;}}
[DataContract]internal sealed class UploadReceipt {[DataMember(Name="saved")]public string Saved {get;set;}[DataMember(Name="sha256")]public string Hash {get;set;}[DataMember(Name="size")]public long Size {get;set;}}
[DataContract]internal sealed class DiscoveryReply {[DataMember(Name="service")]public string Service {get;set;}[DataMember(Name="port")]public int Port {get;set;}}

internal sealed class CompanionClient : IDisposable {
 readonly HttpClient http;readonly string endpoint,token;
 internal CompanionClient(string address,int port,string pairedToken){
  if(!IPAddress.TryParse(address,out var parsed)||parsed.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork||IPAddress.IsLoopback(parsed)||address=="0.0.0.0"||port<1||port>65535)throw new ArgumentException("Enter the PC's LAN IPv4 address");
  endpoint="http://"+parsed+":"+port;token=pairedToken;
  http=new HttpClient(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(3)};
 }
 // Explicit test endpoint is restricted to loopback and never used by the TV UI.
 internal CompanionClient(Uri testEndpoint,string pairedToken){if(!testEndpoint.IsLoopback)throw new ArgumentException("Test endpoint must be loopback");endpoint=testEndpoint.GetLeftPart(UriPartial.Authority);token=pairedToken;http=new HttpClient(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(3)};}
 HttpRequestMessage Request(HttpMethod method,string path){var request=new HttpRequestMessage(method,endpoint+path);if(!string.IsNullOrEmpty(token))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return request;}
 static T Json<T>(byte[] bytes){using(var stream=new MemoryStream(bytes))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
 internal static int DiscoveryPort(byte[] bytes){if(bytes.Length>1024)throw new IOException("Invalid discovery response");var reply=Json<DiscoveryReply>(bytes);if(reply.Service!="tizenps-companion"||reply.Port<1||reply.Port>65535)throw new IOException("Unrecognised PC discovery reply");return reply.Port;}
 async Task<byte[]> Bytes(HttpResponseMessage response,CancellationToken cancel){
  if(!response.IsSuccessStatusCode)throw new IOException("PC returned "+(int)response.StatusCode+"; check pairing and the companion window");
  if(response.Content.Headers.ContentLength>8*1024*1024)throw new IOException("PC response is too large");
  using(var output=new MemoryStream())using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false)){
   byte[] buffer=new byte[65536];int count;while((count=await input.ReadAsync(buffer,0,buffer.Length,cancel).ConfigureAwait(false))>0){if(output.Length+count>8*1024*1024)throw new IOException("PC response is too large");output.Write(buffer,0,count);}return output.ToArray();
  }
 }
 internal async Task<string> Pair(string code,CancellationToken cancel){
  if(code==null||code.Length!=6||code.Any(c=>c<'0'||c>'9'))throw new ArgumentException("Enter the six-digit code shown on the PC");
  using(var request=Request(HttpMethod.Post,"/v1/pair")){request.Content=new StringContent("{\"code\":\""+code+"\"}",Encoding.UTF8,"application/json");using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false)){
   var result=Json<PairResult>(await Bytes(response,cancel).ConfigureAwait(false));if(!TransferFiles.ValidHash(result.Token))throw new IOException("Invalid pairing reply");return result.Token;
  }}
 }
 internal async Task<TransferManifest> Library(CancellationToken cancel){using(var request=Request(HttpMethod.Get,"/v1/library"))using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false)){
  var result=Json<TransferManifest>(await Bytes(response,cancel).ConfigureAwait(false));if(result.Protocol!=1||result.Groups==null||result.Groups.Count>1000)throw new IOException("Unsupported PC library");
  if(result.Groups.Any(g=>g.Files==null||g.Files.Count>20000||string.IsNullOrWhiteSpace(g.Name)))throw new IOException("Invalid PC library");return result;
 }}
 internal async Task Download(string root,TransferGroup group,Action<string> progress,CancellationToken cancel){
  foreach(var file in group.Files){
   cancel.ThrowIfCancellationRequested();if(file.Size<0)throw new IOException("Invalid file size");string target=TransferFiles.Target(root,file.Path);
   if(File.Exists(target)){if(new FileInfo(target).Length==file.Size){progress("Already present: "+file.Path);continue;}throw new IOException("Existing file differs and was preserved: "+file.Path);}
   Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));string partial=target+".tizenps.part",meta=partial+".sha256";
   foreach(var path in new[]{partial,meta})if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked partial transfer files are excluded");
   long offset=File.Exists(partial)?new FileInfo(partial).Length:0;if(offset>=file.Size)offset=0;
   for(int attempt=0;attempt<2;attempt++){
    using(var request=Request(HttpMethod.Get,"/v1/file?path="+Uri.EscapeDataString(file.Path))){
     if(offset>0)request.Headers.Range=new RangeHeaderValue(offset,null);
     using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false)){
      if(response.StatusCode!=HttpStatusCode.OK&&response.StatusCode!=HttpStatusCode.PartialContent)throw new IOException("Download failed: PC returned "+(int)response.StatusCode);
      string digest=response.Headers.TryGetValues("X-Content-SHA256",out var values)?values.FirstOrDefault():null;
      if(!TransferFiles.ValidHash(digest))throw new IOException("PC omitted the file checksum");
      if(offset>0&&(!File.Exists(meta)||File.ReadAllText(meta)!=digest)){offset=0;continue;}
      if(response.StatusCode==HttpStatusCode.OK)offset=0;
      if(response.StatusCode==HttpStatusCode.PartialContent&&(response.Content.Headers.ContentRange?.From!=offset||response.Content.Headers.ContentRange?.Length!=file.Size))throw new IOException("Invalid resumed download range");
      if(response.Content.Headers.ContentLength!=file.Size-offset)throw new IOException("File changed on the PC; refresh the library");
      File.WriteAllText(meta,digest);
      using(var output=new FileStream(partial,FileMode.OpenOrCreate,FileAccess.Write,FileShare.None))using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false)){
       output.SetLength(offset);output.Position=offset;byte[] buffer=new byte[65536];int count;long completed=offset;var tick=System.Diagnostics.Stopwatch.StartNew();
       while((count=await input.ReadAsync(buffer,0,buffer.Length,cancel).ConfigureAwait(false))>0){if(completed+count>file.Size)throw new IOException("Download exceeded expected size");await output.WriteAsync(buffer,0,count,cancel).ConfigureAwait(false);completed+=count;if(tick.ElapsedMilliseconds>=350){progress(file.Path+" — "+(file.Size==0?100:completed*100/file.Size)+"%");tick.Restart();}}
       output.Flush(true);
      }
      cancel.ThrowIfCancellationRequested();TransferFiles.Commit(partial,target,file.Size,digest);File.Delete(meta);progress("Verified: "+file.Path);break;
     }
    }
   }
   if(!File.Exists(target))throw new IOException("Source changed; partial transfer kept for retry");
  }
 }
 internal async Task<UploadReceipt> Upload(string kind,string file,CancellationToken cancel){
  if(kind!="backups"&&kind!="diagnostics")throw new ArgumentException("Unsupported upload");
  long size=new FileInfo(file).Length;if(size>64*1024*1024)throw new IOException("Backup exceeds the PC upload limit");string digest=TransferFiles.Digest(file);
  using(var input=File.OpenRead(file))using(var request=Request(HttpMethod.Post,"/v1/"+kind)){
   request.Content=new StreamContent(input);request.Content.Headers.ContentLength=size;
   using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false)){
    var receipt=Json<UploadReceipt>(await Bytes(response,cancel).ConfigureAwait(false));if(receipt.Size!=size||receipt.Hash!=digest||string.IsNullOrEmpty(receipt.Saved))throw new IOException("PC upload verification failed");return receipt;
   }
  }
 }
 public void Dispose(){http.Dispose();}
}
