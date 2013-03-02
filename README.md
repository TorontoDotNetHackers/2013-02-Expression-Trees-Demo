Quick Start Instructions 
===== 

Use a source file from the project's source/ folder: any of the XML files, or make a new one. Compile your source:  
  
    bin/Debug/TdnhMLSharp.exe /in "source\HelloWorldLoop.xml"  /out "HelloWord.exe"  /run

Now you have a compiled verion of your XML file on disk!  The /run param causes the compiled code to be run immediately.

The Visual Studio Project is set up with the above command line arguments, as a demo, out of the box.  Simply build and run. The demo EXE is output inside the bin folder.  See the project properties' Debug tab for these details. 
    
Visual Studio 2012 Console application, .NET 4.5

Overview
=========

This is a very simple demo of creating .NET expression trees for a purpose. It was part of a session in which the Toronto .NET Hackers focused on the theme of ".NET Expression Trees" in the February 2013 meeting.

This project was an attempt to create a simple compiler that takes an XML source code file containing programming instructions and convert those instructions into a .NET expression tree that is compiled and executed. 


The name TDNH-ML# is a fun combination of programming language names, because we needed a name for this thing - it stands for "Toronto .NET Hackers Markup Language Sharp". ;)

This Console program is a basic compiler that accepts an XML file on the command line (note, some source code samples are predefined in the sources/ subfolder) builds an equivalent .NET expression tree and runs it. The compiler does not produce an executable file and the executable behaviour of the compiled delegate is discarded when the console program exits. 

This program is buggy, for example, nested for loops don't work as expected.  It is meant to be an expression tree demo more than an actual compiler.  The potential of expression trees are evident. 

--jdk

Distrubited under The MIT License (MIT): http://opensource.org/licenses/MIT


Version History (reverse chronology)
------------------------------------

0.2.0.0 alpha - March 1, 2013

Added features:
* Proper command line parameters /run /out filename /in filename
* Ability to optionally save as .EXE assembly on disk

0.1.0.0 alpha - Feb 2013

Initial functionality created, includes: 
1- Accept XML input file on command line; 
2- Build expression tree from XML; 
3- Convert expression tree into a Delegate instance; 
4- Execute Delegate instance. 