Async Diagnostics
=================

All async diagnostics helpers are in this namespace:

    using Nito.AsyncEx.AsyncDiagnostics;

To apply async diagnostics to your assembly, add this line to one of your files:

    [assembly: AsyncDiagnosticAspect]

This will track the "logical stack" for all methods in your assembly and attach the current logical stack to all exceptions when they are thrown.

If you'd like to limit the async diagnostic stack tracking to certain types, you can apply [AsyncDiagnosticAspect] directly to those types (instead of assembly-wide), or you can use PostSharp multicasting.

Displaying the Logical Stack for an Exception
=============================================

The easiest way to see the logical stack is to change ToString to ToAsyncDiagnosticString (which will return ToString followed by the logical stack):

    //Console.WriteLine(exception.ToString());
    Console.WriteLine(exception.ToAsyncDiagnosticString());

Using the Logical Stack Directly
================================

If you'd like to inject your own values into the async diagnostic stack (e.g., dumping important parameters), you can do this:

    using (AsyncDiagnosticStack.Enter("Parameter x is: " + x.ToString()))
	{
	  ... // Any exceptions raised here will include Parameter x information.
	  ... // And this is perfectly async-safe; you can await in here and whatnot.
	}

If you'd like to know what the logical stack is at any time (e.g., for logging without raising an exception), you can read it like this:

    IEnumerable<string> currentLogicalStack = AsyncDiagnosticStack.Current;

Advanced Display of the Logical Stack in Exceptions
===================================================

The actual logical stack is saved (as a string) into the Exception.Data dictionary under the key AsyncDiagnosticStack.DataKey.

An individual exception's logical stack can be retrieved like this:

    string exceptionLogicalStack = exception.AsyncDiagnosticStack(); // returns string.Empty if there is no logical stack

The ToAsyncDiagnosticString extension method calls AsyncDiagnosticStack for all inner exceptions as well as the parent exception itself.

Limitations
===========

There is no support for partial trust.

There is a definite runtime impact.

Only works on the full .NET framework. There's no support for Windows Store, Phone, or Silverlight.

Works best if you build in Debug mode. If a call is inlined, it won't show up in the logical stack.