/*
 * The MIT License (MIT)
 * Copyright (c) 2013 Toronto .NET Hackers meetup group
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * http://opensource.org/licenses/MIT
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;

namespace TdnhMLSharp
{
    /// <summary>
    /// Parses XML source code statements into an expression tree that is compiled and run.
    /// </summary>
    static class Compiler
    {
        /// <summary>
        /// The parsed command line arguments. 
        /// </summary>
        /// 
        static Arguments ParsedCommandArgs = null;

        /// <summary>
        /// Main entry point of our "compiler". 
        /// </summary>
        /// <param name="args"></param>
        /// 
        static int Main(string[] args)
        {
            // process command line arguments 

            Exception[] commandLineErrors = null;
            if (!TryProcessCommandLineArgs(args, out ParsedCommandArgs, out commandLineErrors))
            {
                foreach (Exception e in commandLineErrors)
                    Console.WriteLine("Command Line Error: {0}", e.Message);
                return 1;
            }

            // Read the XML file
            string sourceFilePath = ParsedCommandArgs.RequiredInfileSpec;
            FileStream sourceStream = File.OpenRead(sourceFilePath);
            StreamReader sourceReader = new StreamReader(sourceStream);
            string programXmlText = sourceReader.ReadToEnd();

            // Compile the XML source code into an Assembly (builds expression trees to support the process).
            LambdaExpression exprLambdaProgram = Compiler.CompileXml(programXmlText);

            if (ParsedCommandArgs.IsOutputFileSpecified)
            {
                SaveToDisk(exprLambdaProgram, ParsedCommandArgs.OptionalOutfileSpec);
            }

            if (ParsedCommandArgs.RunAfterBuild)
            {
                // compile a delegate of our program
                Delegate func = exprLambdaProgram.Compile();
                // run the compiled delegate 
                func.DynamicInvoke(null);
#if DEBUG
                // A hack to pause the Visual Studio window before it closes (whether we're debugging from VS or not!)
                Console.WriteLine("\nHit [enter] to exit ...");
                Console.ReadLine();
#endif
            }

            return 0; // success
        }

        /// <summary>
        /// Converts the given list of XML elements into a BlockExpression. 
        /// This method has recursive nature to address sub-elements.
        /// </summary>
        /// <param name="elements">The elements to turn into expressions.</param>
        /// <returns>A block expression</returns>
        /// 
        static BlockExpression ConvertXmlElementsIntoExpressionTree(IEnumerable<XElement> elements)
        {
            List<Expression> expressions = new List<Expression>();

            foreach (XElement el in elements)
            {
                // process 'for' keyword (counter based loop)

                if (el.Name == "for")
                {
                    string varName = Convert.ToString(el.Attribute("varName").Value);
                    int initNum = Convert.ToInt32(el.Attribute("initValue").Value);
                    int targetNum = Convert.ToInt32(el.Attribute("lessThan").Value);
                    int incNum = Convert.ToInt32(el.Attribute("incrementBy").Value);

                    // make a variable for the loop counter
                    ParameterExpression peCounterVar = Expression.Parameter(typeof(int), varName);
                    // assign the intialization value to the loop counter 
                    BinaryExpression exprInitLoopCounter = Expression.Assign(peCounterVar, Expression.Constant(initNum));

                    // create a loop break target
                    LabelTarget breakTarget = Expression.Label();

                    /* Create the core logic of the loop. This includes: 
                     * - generate a check to break out the loop based on counter value;
                     * - the loop itself;
                     * - the execution body of the loop taken from nested XML.
                     */
                    BlockExpression loopCore = Expression.Block(new[] { peCounterVar }, new List<Expression> { 

                        // Write the loop's conditional check and break action 
                        Expression.IfThen(
                                Expression.GreaterThanOrEqual(peCounterVar, Expression.Constant(targetNum)),
                                Expression.Break(breakTarget)
                                ),
                        // increment the loop counter 
                        Expression.AddAssign(peCounterVar, Expression.Constant(incNum)),

                        /* Recurse into XML child elements of the 'for' element and parse them
                         * into expressions to become the core execution logic of the loop iteration. 
                         */ 
                        Compiler.ConvertXmlElementsIntoExpressionTree(el.Elements()),
                    });

                    // create the the loop and surrounding logic 
                    BlockExpression loopWithExit = Expression.Block(
                        new[] { peCounterVar },

                        new Expression[] {
                            exprInitLoopCounter,
                            Expression.Loop(loopCore, breakTarget)
                        });

                    // add the loop logic to the program's instructions 
                    expressions.Add(loopWithExit);
                }

                // process a 'printLine' command

                else if (el.Name == "printLine")
                {
                    // extract the value to display 
                    string displayVal = el.Attribute("value").Value;

                    // Obtain a reference to the System.Console.WriteLine(string) method. 
                    MethodInfo writeLineInfo = typeof(System.Console)
                        .GetMethod("WriteLine", new Type[] { typeof(string) });

                    // Create an expression to print the dispay value on the Console. 
                    Expression exprConsoleWriteLine_Value = Expression.Call(
                        writeLineInfo,
                        Expression.Constant(displayVal)
                        );

                    // Add the printline expression to the program's expressions 
                    expressions.Add(exprConsoleWriteLine_Value);
                }
                else
                {
                    ; // << intentional ... tsk tsk. 

                    /* We're skipping anything we don't understand. 
                     * Yep, a really bad practice for a compiler: to LEAVE OUT logic. 
                     * DISCLAIMER: Demo app only!
                     */
                }

            }

            // Convert all the created expressions into one execution block for the caller 
            return Expression.Block(expressions);
        }

        /// <summary>
        /// Processes the given command line arguments and parses them into an <see cref="Arguments"/> instance. 
        /// </summary>
        /// <param name="commandArgs">command line args to process</param>
        /// <param name="parsedArgs">the parsed arguments if no parsing errors occur</param>
        /// <param name="errors">a list of errors in the commnand line arguments</param>
        /// <returns>true if the command line args were succesfully fully parsed, otherwise false</returns>
        /// <remarks>
        /// If this method returns false then the <paramref name="parsedArgs"/> value will be null and
        ///  the <paramref name="errors"/> array will be null.
        ///  If the method returns true then the <paramref name="parsedArgs"/> value will be present and
        ///  the <paramref name="errors"/> array will have at least one element. 
        /// </remarks>
        /// 
        static bool TryProcessCommandLineArgs(string[] commandArgs, out Arguments parsedArgs, out Exception[] errors)
        {
            List<Exception> problems = new List<Exception>();
            parsedArgs = new Arguments();

            if (commandArgs != null)
            {
                try
                {
                    for (int i = 0; i < commandArgs.Length; ++i)
                    {
                        switch (commandArgs[i])
                        {
                            case Arguments.ParamInfileSpec:
                                parsedArgs.RequiredInfileSpec = commandArgs[++i];
                                break;
                            case Arguments.ParamOutfileSpec:
                                parsedArgs.OptionalOutfileSpec = commandArgs[++i];
                                break;
                            case Arguments.ParamRun:
                                parsedArgs.RunAfterBuild = true;
                                break;
                            default:
                                problems.Add(new Exception(string.Format("uknown param '{0}'", commandArgs[i])));
                                break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(parsedArgs.RequiredInfileSpec))
                        problems.Add(new Exception(string.Format("Must specify a source code file to process with the {0} parameter.", Arguments.ParamInfileSpec)));
                }
                catch (Exception e)
                {
                    problems.Add(e);
                }

            }

            bool failedParsing = problems.Any();
            if (failedParsing)
            {
                errors = problems.ToArray();
                parsedArgs = null;
            }
            else
            {
                errors = null;
            }
            return !failedParsing;
        }

        /// <summary>
        /// To represent command line parameters and hold a set of parsed arguments. 
        /// </summary>
        /// 
        class Arguments
        {
            /// <summary>
            /// The command line input file specification parameter 
            /// </summary>
            public const string ParamInfileSpec = "/in";

            /// <summary>
            /// The command line output file specification parameter
            /// </summary>
            public const string ParamOutfileSpec = "/out";

            /// <summary>
            /// The command line parameter to tell the compiler to also run the program. 
            /// </summary>
            public const string ParamRun = "/run";

            /// <summary>
            /// Parsed value of the run option. 
            /// </summary>
            public bool RunAfterBuild = false;

            /// <summary>
            /// Parsed value of the input file specification. 
            /// </summary>
            public string RequiredInfileSpec = null;

            /// <summary>
            /// Parsed value of the output file specification. 
            /// </summary>
            public string OptionalOutfileSpec = null;

            public bool IsOutputFileSpecified { get { return !string.IsNullOrEmpty(ParamOutfileSpec); } }

        }

        /// <summary>
        /// "Compiles" the provided XML source code into a lambda expression. 
        /// </summary>
        /// <param name="xmlText">The XML source code to compile.</param>
        /// <returns>A LambdaExpression.</returns>
        /// 
        static LambdaExpression CompileXml(string xmlText)
        {
            // convert text to XML 
            XDocument xdoc = XDocument.Parse(xmlText);

            // Convert XML into an expression tree 
            BlockExpression exprProgramBlock = Compiler.ConvertXmlElementsIntoExpressionTree(xdoc.Root.Elements());

            // Turn expression tree into a lambda. 
            LambdaExpression exprLambdaProgram = Expression.Lambda(exprProgramBlock, new ParameterExpression[0]);
            return exprLambdaProgram;

        }

        /// <summary>
        /// Saves the given lambda expression to disk as an executable assembly 
        /// under the given filename. 
        /// </summary>
        /// <param name="exprLambdaProgram">Lambda to save.</param>
        /// <param name="fileSpec">Output file specification.</param>
        /// <remarks>
        /// Code from: http://stackoverflow.com/questions/15168150/how-to-save-an-expression-tree-as-the-main-entry-point-to-a-new-executable-disk
        /// </remarks>
        /// 
        static void SaveToDisk(LambdaExpression exprLambdaProgram, string fileSpec)
        {
            string outputFilename = Path.GetFileName(fileSpec);
            string assemblyName = outputFilename.Substring(0, outputFilename.Length - Path.GetExtension(outputFilename).Length);

            // Attach the expression as a method to a dynamic assembly
            var asmName = new AssemblyName(assemblyName);
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly
                (asmName, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = asmBuilder.DefineDynamicModule(assemblyName, outputFilename);

            var typeBuilder = moduleBuilder.DefineType("Program", TypeAttributes.Public);
            var methodBuilder = typeBuilder.DefineMethod("Main",
                MethodAttributes.Static, typeof(void), new[] { typeof(string) });

            exprLambdaProgram.CompileToMethod(methodBuilder);

            // Save the dynamic assembly to disk as an executable. 
            typeBuilder.CreateType();
            asmBuilder.SetEntryPoint(methodBuilder);
            asmBuilder.Save(outputFilename);
        }

    }
}
