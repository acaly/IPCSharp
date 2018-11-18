# IPCSharp (WIP)
A fast C# implementation of inter-process communication (IPC) mechanism on Windows.

I found it difficult to find a light-weight IPC implementation in C#. This project 
aims at providing easy-to-use methods to achieve (at different level of abstraction):

* Shared memory and user-mode synchronization objects.
* Message queue.
* Remote procedure call (RPC).

Note that currently I only support .NET Framework with Any CPU on Windows. Tests 
and implementation on other platforms are welcomed.
