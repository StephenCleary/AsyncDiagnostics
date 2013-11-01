Async Diagnostics
=================

All async diagnostics helpers are in this namespace:

    using Nito.AsyncEx.AsyncDiagnostics;

To apply async diagnostics to your assembly, add this line to one of your files:

    [assembly: AsyncDiagnosticAspect]

Then, change how your exceptions are logged from ToString() to ToAsyncDiagnosticString():

    Console.WriteLine(exception.ToString());

becomes:

    Console.WriteLine(exception.ToAsyncDiagnosticString());

If you have your own exception dumping code, you can use exception.LogicalStack() or exception.AsyncDiagnosticStack().

Advanced
========

If you'd like to inject your own values into the async diagnostic stack (e.g., dumping important parameters), you can do this:

    using (AsyncDiagnosticStack.Enter("Parameter x is: " + x.ToString()))
	{
	  ... // Any exceptions raised here will include Parameter x information.
	}

If you'd like to limit the async diagnostic stack tracking to certain types, you can apply [AsyncDiagnosticAspect] directly to those types (instead of assembly-wide), or you can use PostSharp multicasting.

Limitations
===========

This currently requires a paid version of PostSharp. You can evaluate PostSharp for free for 30 days. Hopefully this restriction will be removed in PostSharp 3.1.

There is no support for partial trust. This is not likely to change.

[AsyncDiagnosticAspect] may be applied to assemblies or types. It does *not* work correctly when multicast onto methods. Hopefully this will be fixed after PostSharp 3.1 is released.