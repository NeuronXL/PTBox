using System.IO;
using System.Diagnostics;
using PTBox.Launcher.ViewModels;

namespace PTBox.Tests;

internal static class FilePickerTests
{
    public static async Task Run(string root)
    {
        var folder=Path.Combine(root,"picker"); Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder,"子目录"));
        foreach(var file in new[] { "程序.EXE","背景.png","说明.txt" }) await File.WriteAllTextAsync(Path.Combine(folder,file),"fixture");
        var vm=new FilePickerViewModel([".exe"]);
        await vm.NavigateAsync(folder);
        Check(vm.Entries.Count==2 && vm.Entries[0].IsDirectory && vm.Entries[1].Name=="程序.EXE","目录优先且扩展名过滤忽略大小写");
        Check(vm.SelectFile("程序.EXE")==Path.Combine(folder,"程序.EXE"),"相对路径选择");
        Check(vm.SelectFile("背景.png")==null && vm.SelectFile("missing.exe")==null,"拒绝类型不匹配和不存在的文件");
        vm.Search="程序"; Check(vm.Entries.Count==1,"文件名筛选");
        await vm.NavigateAsync("子目录"); Check(vm.Entries.Count==0 && vm.Search=="","进入空目录并清空筛选");
        await vm.NavigateAsync(".."); Check(vm.DirectoryPath==folder,"返回上一级");
        await vm.NavigateAsync(Path.Combine(folder,"missing")); Check(vm.DirectoryPath==folder && vm.CanBrowse && vm.Status.Contains("无法打开"),"失败后保留可用目录");
        var images=new FilePickerViewModel([".png",".jpg"]); await images.NavigateAsync(folder);
        Check(images.Entries.Count==2 && images.SelectFile("背景.png")!=null && images.SelectFile("程序.EXE")==null,"图片模式过滤");
        ShortcutTests.CreateShortcut(Path.Combine(folder,"桌面程序.LNK"),Path.Combine(folder,"程序.EXE"));
        await File.WriteAllTextAsync(Path.Combine(folder,"游戏.URL"),"[InternetShortcut]\nURL=steam://rungameid/123\n");
        var apps=new FilePickerViewModel([".exe",".lnk",".url"]); await apps.NavigateAsync(folder);
        Check(apps.Entries.Count==4 && apps.SelectFile("桌面程序.LNK")!=null && apps.SelectFile("游戏.URL")!=null,"应用选择器接受快捷方式，扩展名大小写不敏感");
        var large=Path.Combine(root,"large-picker"); Directory.CreateDirectory(large);
        for(var i=0;i<3000;i++) File.WriteAllText(Path.Combine(large,$"entry-{i:D4}.lnk"),"not parsed while browsing");
        var changes=0; apps.Entries.CollectionChanged+=(_,_)=>changes++;
        var timer=Stopwatch.StartNew(); await apps.NavigateAsync(large); timer.Stop();
        Check(apps.Entries.Count==3000 && changes==1,"大量文件只批量通知一次，不解析目录内快捷方式");
        Console.WriteLine($"File picker: 3000 entries in {timer.ElapsedMilliseconds} ms; {changes} list refresh.");
        var first=apps.NavigateAsync(large); var latest=apps.NavigateAsync(folder);
        await Task.WhenAll(first,latest);
        Check(apps.DirectoryPath==folder && apps.Entries.Count==4 && !apps.IsBusy,"取消过时扫描，旧结果不能覆盖新目录");
        var stopped=apps.NavigateAsync(large); apps.Stop(); await stopped;
        Check(apps.DirectoryPath==folder && !apps.IsBusy,"关闭选择器时停止扫描且不更新已关闭的列表");
    }
    private static void Check(bool result,string message) { if(!result) throw new InvalidOperationException(message); }
}
