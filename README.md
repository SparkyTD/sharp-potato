# SharpPotato
A fully managed C# implementation of [JuicyPotato](https://github.com/ohpe/juicy-potato)

### Project Status / TODO
- [x] CreateProcessWithTokenW
- [ ] CreateProcessAsUserW
- [x] Custom COM Server Address/Port
- [x] Custom CLSIDs
- [x] Custom Process (currently hard-coded to cmd.exe)
- [x] Custom CmdLine Arguments
- [x] Custom RPC Server Address/Port
- [ ] Error checking
- [ ] Stream redirection (stdout, stderr, stdin)

### Example Usage
The following code will start a `cmd.exe` process as `nt authority\system`.
```csharp
var process = new SystemProcess();
process.StartInfo.FileName = "cmd.exe";
process.StartInfo.Arguments = "/K whoami";

process.Start();
process.WaitForExit();
```
