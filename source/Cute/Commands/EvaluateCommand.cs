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
        string pythonDLL = @"C:\Python312\python312.dll";
        string setupFile = Path.GetFullPath(@"..\Cute.Lib\PythonScripts\setup.py");
        string evalRagFile = Path.GetFullPath(@"..\Cute.Lib\PythonScripts\eval_rag.py");

        try
        {
            Runtime.PythonDLL = pythonDLL; // Set the python dll path
            PythonEngine.Initialize();

            ExecutePython(filePath: setupFile); // Execute the setup.py file first

            string input = @"
                Write a 200-210 word website intro as to why Canton of Zug in Switzerland is good for setting up your business 
                and having an office there. Mention some of the famous businesses that are headquartered there, 
                industries based there, major transport hubs in the vicinity and other facts relevant to doing business there.

                Make sure you include the phrase '''Office space in Canton of Zug''' in the intro.

                Output your response in bullets using valid Markdown syntax. Use '''-''' for a list prefix 
                but don't make use of bold, underline or other Markdown.";

            string actualOutput = @" 
                - Nestled in the heart of Switzerland, Canton of Zug is a prime spot for creative entrepreneurs looking to establish their next venture. Its strategic location offers quick access to major cities such as Zurich and Lucerne, making it a vibrant nexus for business. The region's excellent transport connectivity, including proximity to Zurich International Airport, ensures seamless global operations. Modern office buildings and state-of-the-art coworking spaces cater perfectly to the needs of contemporary enterprises.
                - Zug stands out with its business-friendly environment characterized by low tax regimes and supportive economic policies. This favorable climate has attracted numerous global companies like Glencore and Siemens, underscoring its appeal to industry giants. The diverse business ecosystem here supports a myriad of industries from finance and tech to commodities trading and pharmaceuticals. This diversity not only propels innovation but also enriches networking opportunities across various sectors.
                - Moreover, Zug boasts access to an exceptionally skilled workforce that is both highly educated and multilingual—an invaluable asset for any growing company. Whether you're launching a startup or relocating an established business, Zug provides all the resources you need for success in one dynamic locale. Embrace this unique opportunity; set up your next venture amid the inspiring backdrop of Canton of Zug today!.";

            string expectedOutput = @" 
                - Office space in Canton of Zug offers a strategic advantage for businesses looking to establish a presence in Switzerland.
                - Zug is home to numerous renowned companies, including Roche Diagnostics, Siemens Smart Infrastructure, and Glencore.
                - The canton boasts a diverse range of industries such as life sciences, high-tech, fintech, and commodity trading.
                - Its central location in Switzerland provides excellent connectivity, with major transport hubs like Zurich Airport and Zurich Hauptbahnhof nearby.
                - Zug's business-friendly environment is characterized by low taxes, a stable political climate, and a high quality of life, making it an attractive destination for both startups and established enterprises.
                - The region also offers a robust infrastructure, including innovation platforms and institutions like the Switzerland Innovation Park Central and the Central Switzerland University of Applied Sciences and Arts.
                - With a strong network of industry-specific associations and a reputation for efficiency and professionalism, Zug is well-equipped to support business growth and innovation.
                - Whether you're in life sciences, high-tech, or finance, office space in Canton of Zug provides the ideal setting for your business to thrive.";

            string retrievalContext = @"address: Neugasse 25, 6300 Zug, Switzerland; population: 30000";
           
            PyString pyGptModel = new PyString("gpt-4o-mini");
            PyFloat pyThreshold = new PyFloat(0.7);
            PyString pyInput = new PyString(input);
            PyString pyActualOutput = new PyString(actualOutput);
            PyString pyExpectedOutput = new PyString(expectedOutput);
            PyString pyRetrievalContext = new PyString(retrievalContext);

            // First, we need to execute the __init__ method to initialize the EvalRAG class (in eval_rag.py)
            PyObject? ragEvalObj = ExecutePython(
                filePath: evalRagFile, 
                pyClass: "EvalRAG", 
                pyMethod: "__init__", 
                pyArgs: new List<PyObject> { pyGptModel, pyThreshold, pyInput, pyActualOutput, pyExpectedOutput, pyRetrievalContext }
            );
            
            // Check if EvalRAG was properly instantiated before invoking EvalRAG methods
            if (ragEvalObj == null) { throw new NullReferenceException("ragEvalObj is null"); }

            //PyObject? ragEvalEvaluateResult = ExecutePython(filePath: evalRagFile, pyClass: "EvalRAG", pyMethod: "evaluate", pyArgs: new List<PyObject> { ragEvalObj }); // Execute evaluate method on the EvalRAG class (eval_rag.py)
            //Console.WriteLine(ragEvalEvaluateResult.AsManagedObject(typeof(string)) as string);

            PyString pyMetric = new PyString("faithfulness"); // Specify the metric to be used for evaluation
            // Execute the measure method on the EvalRAG class (in eval_rag.py) to get the result of a specified measure
            PyObject? ragEvalMeasureResult = ExecutePython(
                filePath: evalRagFile, 
                pyClass: "EvalRAG", 
                pyMethod: "measure", 
                pyArgs: new List<PyObject> { ragEvalObj, pyMetric }
            );
            pyMetric.Dispose(); // Dispose object to free up memory

            // Check if ragEvalMeasureResult is not null before proceeding
            if (ragEvalMeasureResult == null) { throw new NullReferenceException("ragEvalMeasureResult is null"); }
            Console.WriteLine(ragEvalMeasureResult.AsManagedObject(typeof(string)) as string);

            pyGptModel.Dispose(); // Dispose pyGptModel to free up memory
            pyThreshold.Dispose(); // Dispose pyThreshold to free up memory
            pyInput.Dispose(); // Dispose pyInput to free up memory
            pyActualOutput.Dispose(); // Dispose pyActualOutput to free up memory
            pyExpectedOutput.Dispose(); // Dispose pyExpectedOutput to free up memory
            pyRetrievalContext.Dispose(); // Dispose pyRetrievalContext to free up memory

            PythonEngine.Shutdown(); // Shutdown Python engine

        }
        catch (PythonException ex)
        {
            Console.WriteLine(ex.Message);
        }
        catch (NullReferenceException ex)
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

    private static PyObject? ExecutePython(string filePath, string? pyClass = null, string? pyMethod = null, List<PyObject>? pyArgs = null)
    {        
        PyObject? result = null; // Create a PyObject to hold the result
        string file = Path.GetFullPath(filePath);            
            
        if (!PythonEngine.IsInitialized) // Since using asp.net, we may need to re-initialize
        {
            PythonEngine.Initialize();
        }

        using(Py.GIL()) // acquire the GIL before using the Python interpreter
        {
            using (PyModule scope = Py.CreateScope()) // create a Python scope
            {
                string code = File.ReadAllText(file); // Get code as raw text
                var codeCompiled = PythonEngine.Compile(code, file); // Compile the code/file
                scope.Execute(codeCompiled); // Execute the compiled python so we can start calling it.

                // Execute the python method if provided with its arguments
                if (pyClass != null && pyMethod != null && pyArgs != null)
                {
                    
                    PyObject? pyObj = null; // Create a PyObject to hold the class
                    PyObject[]? param = new PyObject[pyArgs.Count]; // Create an array of PyObject to hold the arguments

                    if (pyMethod == "__init__") // If the method is __init__, we need to create an instance of the class
                    {
                        Array.Resize(ref param, pyArgs.Count + 1); // Resize the array to hold the class and the arguments
                        pyObj = scope.Get(pyClass); // Get an instance of pyClass 
                        param[0] = pyObj; // Append the class to the arguments
                        for (int i = 1; i < param.Length; i++) // Loop through the arguments
                        {
                            param[i] = pyArgs[i-1]; // Append the method arguments to the PyObject[] array
                        }
                    }
                    else // If the method is not __init__, we call the method on the provided instance of the class
                    {
                        pyObj = pyArgs[0]; // Get class instance from the arguments 
                        param[0] = pyObj; // Append instance to the arguments (i.e., self)
                        for (int i = 1; i < param.Length; i++) // Loop through the arguments
                        {
                            param[i] = pyArgs[i]; // Append the method arguments to the PyObject[] array
                        }
                    }

                    result = pyObj.InvokeMethod(pyMethod, param); // Call the pyMethod with pyArgs on the pyObject of pyClass
                }

                scope.Dispose(); // Dispose scope
            }
            Py.GIL().Dispose(); // release the GIL
        }

        return result; // Return the result   
    }
}