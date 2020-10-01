# immudb4dotnet

[![License](https://img.shields.io/github/license/codenotary/immudb4dotnet)](LICENSE)

[![Slack](https://img.shields.io/badge/join%20slack-%23immutability-brightgreen.svg)](https://slack.vchain.us/)
[![Discuss at immudb@googlegroups.com](https://img.shields.io/badge/discuss-immudb%40googlegroups.com-blue.svg)](https://groups.google.com/group/immudb)

.NET Client for immudb


### Official [immudb] .NET Standard client.

[immudb]: https://grpc.io/


## Contents

- [Introduction](#introduction)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Supported Versions](#supported-versions)
- [Quickstart](#quickstart)
- [Step by step guide](#step-by-step-guide)
    * [Creating a Client](#creating-a-client)
    * [User sessions](#user-sessions)
    * [Creating a database](#creating-a-database)
    * [Setting the active database](#setting-the-active-database)
    * [Traditional read and write](#traditional-read-and-write)
    * [Verified or Safe read and write](#verified-or-safe-read-and-write)
    * [Multi-key read and write](#multi-key-read-and-write)
    * [Closing the client](#creating-a-database)
- [Contributing](#contributing)

## Introduction

immudb4dotnet implements a [grpc] immudb client. A minimalist API is exposed for applications while cryptographic
verifications and state update protocol implementation are fully implemented by this client.

[grpc]: https://grpc.io/
[immudb research paper]: https://immudb.io/
[immudb]: https://immudb.io/

## Prerequisites

immudb4dotnet assumes an already running immudb server. Running `immudb` is quite simple, please refer to the
following link for downloading and running it: https://docs.immudb.io/quickstart.html

## Installation

Use NuGet package Immudb4DotNet

## Supported Versions

immudb4dotnet supports the [latest immudb release].

[latest immudb release]: https://github.com/codenotary/immudb/releases/tag/v0.7.1

## Step by step guide

### Creating a Client

The following code snippets shows how to create a client.

Using default configuration:
```C# 

  var client = new CodeNotary.ImmuDb.ImmuClient("localhost"))
```

client implements IDisposable so you can wrap it with using

```C# 
using (var client = new CodeNotary.ImmuDb.ImmuClient("localhost", 3322))
{
}
```

### User sessions

Use `LoginAsync` and `LogoutAsync` methods to initiate and terminate user sessions. You can specify optional database name to be used by default. If database does not exists it will be created

```C#
    await immuClient.LoginAsync("user", "password", "database");

    // Interact with immudb using logged user

    await immuClient.LogoutAsync();
```

alternativly you can call Close() to completele end your connection. After Logout() the same client can be used, but after Close() you have to create a new one. The Close() method called on dispose automatically

### Creating a database

Creating a new database is quite simple:

```C#
    immuClient.CreateDatabaseAsync("database");
```

### Setting the active database

Specify the active database with:

```C#
    immuClient.UseDatabaseAsync("database", false);
```

Second optional parameter indicates that database needs to be created if it's not exists. Default is true

### Traditional read and write

immudb provides read and write operations that behave as a traditional
key-value store i.e. no cryptographic verification is done. This operations
may be used when validations can be post-poned:

```C#
    await client.SetAsync("Key", "Value");
    
    var result = await client.GetAsync("Key");
```

You can use generic methods that takes class as value. it will be serialized as json and written to immudb, and de-serialized on get

```C#
await client.SetAsync("key", new MyClass() { Property = "Value" });

var result = await client.GetAsync<MyClass>("key");
```

TryGet methods are also avaiable. They will not throw exceptions if specific key is missing in a database

```C#
 if (await client.TryGet("key", out var value))
 {
  // use value
 }
```

### Verified or Safe read and write

immudb provides built-in cryptographic verification for any entry. The client
implements the mathematical validations while the application uses as a traditional
read or write operation:

```C#
    try
    {
        await client.SafeSetAsync("key", "value");
    
        var result = await client.SafeGetAsync("key");
    } 
    catch (VerificationException e) 
    {
       //TODO: tampering detected!
    }
```



### Closing the client

To programatically close the connection with immudb server use the `shutdown` operation:
 
```C#
    immuClient.Close();
```

Note: after shutdown, a new client needs to be created to establish a new connection.

## Contributing

We welcome contributions. Feel free to join the team!

To report bugs or get help, use [GitHub's issues].

[GitHub's issues]: https://github.com/codenotary/immudb4dotnet/issues
