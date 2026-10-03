using System;
using System.IO;
using System.Threading;
using BiliGreenDownloader;

// Small HTTPS transfer through the same Fetch method used for runtime components.
internal static class ComponentFetchSmoke {
    sealed class Progress:IProgress<Update>{public void Report(Update value){Console.WriteLine(value.Message);}}
    static int Main(string[] args){
        try{
            string fixture=Path.GetFullPath(args[0]),destination=Path.GetFullPath(args[1]);Directory.CreateDirectory(Path.GetDirectoryName(destination));
            byte[] original=File.ReadAllBytes(fixture);if(original.Length<10)throw new Exception("fixture too small");
            if(File.Exists(destination))File.Delete(destination);using(var part=File.Create(destination+".part"))part.Write(original,0,10);
            System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
            using(var token=new CancellationTokenSource(TimeSpan.FromSeconds(30)))RuntimeSetup.Fetch(RuntimeSetup.FfmpegZip+".sha256",destination,Core.Hash(fixture),token.Token,new Progress(),"组件续传检查");
            if(Core.Hash(destination)!=Core.Hash(fixture)||File.Exists(destination+".part"))throw new Exception("transfer did not finish correctly");
            Console.WriteLine("PASS actual component Fetch with pre-existing partial file and SHA256 validation");return 0;
        }catch(Exception ex){for(Exception cause=ex;cause!=null;cause=cause.InnerException)Console.WriteLine(cause.GetType().Name+": "+Core.Redact(cause.Message));return 1;}
    }
}
