# SharpPotato
A fully managed C# implementation of [JuicyPotato](https://ohpe.it/juicy-potato/)

Note that this is currently just a library, not a command-line executable tool. You'll have to write your own CLI wrapper around the `JuicyPotato` or `SystemProcess` classes.

### Project Status / TODO
- [x] CreateProcessWithTokenW
- [ ] CreateProcessAsUserW
- [x] Custom COM Server Address/Port
- [x] Custom CLSIDs
- [x] Custom Process (currently hard-coded to cmd.exe)
- [x] Custom CmdLine Arguments
- [x] Custom RPC Server Address/Port
- [ ] Error checking and custom Exceptions
- [x] Stream redirection (stdout, stderr, stdin)
- [ ] Asynchronous COM/RPC socket I/O
- [ ] Reliability (packet 72 fix)

### Example Usage
The following code will start a `cmd.exe` process as `nt authority\system`.
```csharp
var process = new SystemProcess();
process.StartInfo.FileName = "cmd.exe";
process.StartInfo.Arguments = "/K whoami";

process.Start();
process.WaitForExit();
```

You can use the `SystemProcess` class as you would use a regular `Process` class. Some `ProcessStartInfo` features are supported, such as RedirectStandardInput/Output/Error, CreateNoConsole, and custom filename or command line arguments. If you redirect the standard streams, you can use the `StandardInput`, `StandardOutput` and `StandardError` interfaces of the `SystemProcess` class to interact with the process.
