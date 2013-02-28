using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;

namespace TdnhMLSharp
{
    /// <summary>
    /// Compiles XML source code statements into an expression tree
    /// and provides a delegate that encapsulates the instructions 
    /// and can be run (executed) at a later time.
    /// </summary>
    static class Compiler
    {
        /// <summary>
        /// Main entry point of the compiler. 
        /// </summary>
        /// <param name="args"></param>
        /// 
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: {0} sourceCode.xml ", Environment.CommandLine);
                return 1;
            }   

            // Grab the XML file from the command line
            string sourceFilePath = args[0];
            FileStream sourceStream = File.OpenRead(sourceFilePath);
            StreamReader sourceReader = new StreamReader(sourceStream);
            string programXmlText = sourceReader.ReadToEnd();

            // this is the point of the demo
            Delegate programFunc = Compiler.Compile(programXmlText);

            // run the compiled delegate 
            programFunc.DynamicInvoke(null);
#if DEBUG
            Console.WriteLine("\nHit [enter] to exit ...");
            Console.ReadLine();
#endif
            return 0;
        }

        /// <summary>
        /// Compiles the provided XML source code into a delegate
        ///  for deferred execution at a later point. 
        /// </summary>
        /// <param name="xmlText">The XML source code to compile.</param>
        /// <returns>A parameterless delegate representing the instructions of the XML source.</returns>
        /// 
        static Delegate Compile(string xmlText)
        {
            // convert text to XML 
            XDocument xdoc = XDocument.Parse(xmlText);

            // Convert XML to expression tree 
            BlockExpression exprProgramBlock = Compiler.ConvertXmlElementsIntoExpressionTree(xdoc.Root.Elements());

            // Compile expression tree into a Delegate. 
            LambdaExpression lambdaProgram = Expression.Lambda(exprProgramBlock, new ParameterExpression[0]);
            var programFunc = lambdaProgram.Compile();

            // return the delegate instance
            return programFunc;
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

                    /* Create the core logic of the loop. This includes 
                     * - a generated check to break out the loop properly based on counter attributes
                     * - the XML logic of the loop itself. 
                     */
                    BlockExpression loopCore = Expression.Block(new[] { peCounterVar }, new List<Expression> { 

                        // Write the loop's conditional check and break action for when it's reached
                        Expression.IfThen(
                                Expression.GreaterThanOrEqual(peCounterVar, Expression.Constant(targetNum)),
                                Expression.Break(breakTarget)
                                ),
                        // increment the loop counter 
                        Expression.AddAssign(peCounterVar, Expression.Constant(incNum)),

                        /* Recurse into child XML elements of the 'for' element and parse them
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

                    // add the loop logic to the program instructions 
                    expressions.Add(loopWithExit);
                }

                // process 'printLine' command

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
            }

            // Convert all the created expressions into one execution block for the caller 
            return Expression.Block(expressions);
        }

    }//class 
}
