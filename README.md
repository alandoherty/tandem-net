<div align="center">

[![GitHub license](https://img.shields.io/badge/license-Apache%202-blue.svg?style=flat-square)](https://raw.githubusercontent.com/alandoherty/tandem-net/master/LICENSE)
[![GitHub issues](https://img.shields.io/github/issues/alandoherty/tandem-net.svg?style=flat-square)](https://github.com/alandoherty/tandem-net/issues)
[![GitHub stars](https://img.shields.io/github/stars/alandoherty/tandem-net.svg?style=flat-square)](https://github.com/alandoherty/tandem-net/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/alandoherty/tandem-net.svg?style=flat-square)](https://github.com/alandoherty/tandem-net/network)
[![GitHub forks](https://img.shields.io/nuget/dt/Tandem.svg?style=flat-square)](https://www.nuget.org/packages/Tandem/)

</div>


# Tandem

This repository provides a framework for performing platform-agnostic and language neutral distributed locking. Locks can be described on a resource system or format of your choice, as long as it can be embedded into an RFC 3986 URI host & path.

## Getting Started

[![NuGet Status](https://img.shields.io/nuget/v/Tandem.svg?style=flat-square)](https://www.nuget.org/packages/Tandem/)

You can install the package using either the CLI:

```
dotnet add package Tandem
```

or from the NuGet package manager:

```
Install-Package Tandem
```

### Example

The library includes a basic implementation of `ILockManager` named `ProcessLockManager` which can be used internally while testing, and is especially useful during dependency injection when tests do not communicate with an external system.

```csharp
ILockManager manager = new ProcessLockManager();

try {
	using (var handle = await manager.LockAsync("tandem://alan/balance", TimeSpan.FromSeconds(5))) {
		// do something involving alan's bank balance
	}
} catch(TimeoutException tex) {
	Console.WriteLine($"Failed to obtain lock: {tex}");
}
```

When the time comes to begin testing multiple components of the system you can switch to the capable `RedisLockManager`, which relies on atomic operations within Redis to perform distributed locking.

The manager is fault tolerant and in best conditions locks will be kept for as little time as possible. In worst conditions locks may remain until they expire after 60 seconds, and locks may be invalidated externally. You can listen for these situations by attaching an event handler to `ILockHandle.Invalidated`.

If connection is lost to Redis the manager assumes the lock is valid until expiry, so as long as Redis does not forget the lock exists and your code responsibly checks that the lock is still valid you should encounter no issues.

## Resources

The standard resource format is a URI similar to `tandem://device/93678f33-8614-4b7a-bc4b-9febc598ac8e/host`.

The protocol scheme `tandem` describes this as a tandem resource. The host `device` describes the resource type and the first component of the path `93678f33-8614-4b7a-bc4b-9febc598ac8e` determines the resource UUID. You are free to structure the URI paths however you choose, and the hierarchy of the path is not taken into account when performing locks.

## Contributing

Any pull requests or bug reports are welcome, please try and keep to the existing style conventions and comment any additions.