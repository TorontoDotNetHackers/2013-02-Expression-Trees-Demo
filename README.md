This is a very simple demo of creating .NET expression trees. It was part of a session in which the Toronto .NET Hackers focused on the theme of ".NET Expression Trees" in a February 2013 meeting.

This project was an attempt to create a simple compiler that takes an XML source code file containing programming instructions and converting those instructions into a .NET expression tree that is compiled and executed. 

This Console program is a basic compiler that accepts an XML file on the command line (note, some source code samples are predefined in the sources/ subfolder) builds an equivalent .NET expression tree and runs it. The compiler doesn't produce an executable file and the executable behaviour of the compiled delegate generated is discarded when the XML program finishes running. 

This program is buggy, for example, nested for loops don't work as expected.  It is meant to be an expression tree demo more than an actual compiler.  The potential of expression trees can be witnessed. 