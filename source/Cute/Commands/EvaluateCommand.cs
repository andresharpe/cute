using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Dynamic;
using System.Text;
using Text = Spectre.Console.Text;
using Python.Runtime;
using PyModule = Python.Runtime.PyModule;
using PyObject = Python.Runtime.PyObject;

namespace Cute.Commands;

public sealed class EvaluateCommand : AsyncCommand<EvaluateCommand.Settings>
{
    public EvaluateCommand()
    {
    }

    public class Settings : CommandSettings
    {
    }

    #pragma warning disable CS1998
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            Runtime.PythonDLL = @"C:\Python312\python312.dll"; // Set the python dll path
            PythonEngine.Initialize();
            Py.GIL();

            string? result = null;

            // Python scripts are located in Cute.Bil/Evaluation folder
            string setupFile = Path.GetFullPath(@"..\Cute.Lib\Evaluation\setup.py");
            string evalRagFile = Path.GetFullPath(@"..\Cute.Lib\Evaluation\eval_rag.py");
            
            
            if (!PythonEngine.IsInitialized)// Since using asp.net, we may need to re-initialize
            {
                PythonEngine.Initialize();
                Py.GIL();
            }
            
            using (var scope = Py.CreateScope())
            {
                /*
                The setup.py file is used to import the necessary python libraries and functions needed for the eval_rag.py file.
                */
                string setupCode = File.ReadAllText(setupFile); // Get code as raw text
                var setupCompiled = PythonEngine.Compile(setupCode, setupFile); // Compile the code/file
                scope.Execute(setupCompiled); // Execute the compiled python so we can start calling it.

                /*
                    The eval_rag.py file is the python script that contains the EvalRAG class.
                */
                string evalRagCode = File.ReadAllText(evalRagFile); // Get code as raw text
                var scriptCompiled = PythonEngine.Compile(evalRagCode, evalRagFile); // Compile the code/file
                scope.Execute(scriptCompiled); // Execute the compiled python so we can start calling it.
                PyObject evalRAGClass = scope.Get("EvalRAG"); // Get an instance of the EvalRAG class in python
                PyObject pythongReturn = evalRAGClass.InvokeMethod("evaluate", evalRAGClass); // Call the evaluate function on the evalRAGClass object
                result = pythongReturn.AsManagedObject(typeof(string)) as string; // convert the returned output to managed string object
                
                /*
                    Dispose of the objects to free up memory
                */
                evalRAGClass.Dispose(); // Dispose evalRAGClass PyObject
                pythongReturn.Dispose(); // Dispose pythongReturn PyObject
                scope.Dispose(); // Dispose scope
            }

            PythonEngine.Shutdown(); // Shutdown the python engine
        }
        catch (PythonException ex)
        {
            Console.WriteLine(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            /*
            BinaryFormatter serialization and deserialization are disabled within this application.
            This is a known issue when using .NET 8, as BinaryFormatter serialization and deserialization are disabled by default for security reasons.
            */
            Console.WriteLine(ex.Message);
        }

        return 0;
    }
}