# Drexel.Half
A library providing half-precision floating point number support for older versions of C#

# Summary
Half-precision floating point number support was introduced as part of .NET 5, and is not available in older versions.
This library is provided for compatibility purposes, where half-precision support is needed in older versions of .NET.
The vast majority of this library is ~stolen~ graciously borrowed from the
[.NET Reference Source](https://source.dot.net/#System.Private.CoreLib/Half.cs,7895d5942d33f974).

# **Caveat Emptor**
I have done nothing except ~steal~ borrow the reference source and mangled it until it builds in .NET Standard 1.0. I
can make no guarantees as to its functionality, though I do guarantee that I have managed to break the `Parse`,
`TryParse`, and `ToString` contracts such that this library is not interchangeable with the official implementation.