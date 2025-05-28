Interception.Implementation
---------------------------
High-level implementation over Interception.net allows you to intercept, send, and cancel keystrokes from a range of devices

Example
---------------------------
```csharp
using Interception;

InterceptionImpl.OnKeyUp += key => Console.WriteLine(key);

InterceptionImpl.KeyDown(Key.A);
Thread.Sleep(50);
InterceptionImpl.KeyUp(Key.A);
```

Dependencies
------------
[Interception.net](https://github.com/Yoticc/Interception.net)
