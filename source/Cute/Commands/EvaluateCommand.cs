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

namespace Cute.Commands;

public sealed class EvaluateCommand : LoggedInCommand<EvaluateCommand.Settings>
{
    private readonly ILogger<EvaluateCommand> _logger;

    public EvaluateCommand(IConsoleWriter console, ILogger<EvaluateCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _logger = logger;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-g|--generation")]
        [Description("The generation metric to evaluate. Can be 'answer', 'faithfulness' or 'all'.")]
        public string? GenerationMetric { get; set; } = default!;

        [CommandOption("-t|--translation")]
        [Description("The translation metric to evaluate. Can be 'gleu', 'meteor', 'lepor', or 'all'.")]
        public string? TranslationMetric { get; set; } = default!;

        [CommandOption("-s|--seo")]
        [Description("The seo metric to evaluate. Default is 'all'.")]
        public string? SeoMetric { get; set; } = default!;

        [CommandOption("-i|--prompt-id")]
        [Description("The id of the Contentful prompt entry to generate prompts from.")]
        public string PromptId { get; set; } = default!;

        [CommandOption("-p|--prompt-field")]
        [Description("The field containing the prompt template for the LLM.")]
        public string PromptField { get; set; } = "prompt";

        [CommandOption("-c|--generated-content")]
        [Description("The field containing the LLM's generated content.")]
        public string GeneratedContentField { get; set; } = "generated_content";

        [CommandOption("-r|--reference-content")]
        [Description("The field containing the reference content.")]
        public string ReferenceContentField { get; set; } = "reference_content";

        [CommandOption("-f|--facts")]
        [Description("The field containing the facts for the generation evaluation.")]
        public string FactsField { get; set; } = "facts";
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

    #pragma warning disable CS1998
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {

        var result = await base.ExecuteAsync(context, settings);
        var metricsResult = string.Empty;

        var generationMetric = settings.GenerationMetric;
        var translationMetric = settings.TranslationMetric;
        var seoMetric = settings.SeoMetric;       
        //var promptMainPrompt = settings.PromptField;
        var promptMainPrompt = @"
            Write a 200-210 word website intro as to why Canton of Zug in Switzerland is good for setting up your business 
            and having an office there. Mention some of the famous businesses that are headquartered there, 
            industries based there, major transport hubs in the vicinity and other facts relevant to doing business there.

            Make sure you include the phrase '''Office space in Canton of Zug''' in the intro.

            Output your response in bullets using valid Markdown syntax. Use '''-''' for a list prefix 
            but don't make use of bold, underline or other Markdown.";

        var promptMainPromptTrans = "Translate the following text to English: Es una guía para la acción que asegura que el ejército siempre obedecerá los mandatos del Partido.";

        // var generatedContentField = settings.GeneratedContentField;
        var generatedContentField = @" 
            - Nestled in the heart of Switzerland, Canton of Zug is a prime spot for creative entrepreneurs looking to establish their next venture. Its strategic location offers quick access to major cities such as Zurich and Lucerne, making it a vibrant nexus for business. The region's excellent transport connectivity, including proximity to Zurich International Airport, ensures seamless global operations. Modern office buildings and state-of-the-art coworking spaces cater perfectly to the needs of contemporary enterprises.
            - Zug stands out with its business-friendly environment characterized by low tax regimes and supportive economic policies. This favorable climate has attracted numerous global companies like Glencore and Siemens, underscoring its appeal to industry giants. The diverse business ecosystem here supports a myriad of industries from finance and tech to commodities trading and pharmaceuticals. This diversity not only propels innovation but also enriches networking opportunities across various sectors.
            - Moreover, Zug boasts access to an exceptionally skilled workforce that is both highly educated and multilingual—an invaluable asset for any growing company. Whether you're launching a startup or relocating an established business, Zug provides all the resources you need for success in one dynamic locale. Embrace this unique opportunity; set up your next venture amid the inspiring backdrop of Canton of Zug today!.";

        var translatedContentField = "It is a guide to action which ensures that the military always obeys the commands of the party.";

        // var referenceContentField = settings.ReferenceContentField;
        var referenceContentField = @" 
            - Office space in Canton of Zug offers a strategic advantage for businesses looking to establish a presence in Switzerland.
            - Zug is home to numerous renowned companies, including Roche Diagnostics, Siemens Smart Infrastructure, and Glencore.
            - The canton boasts a diverse range of industries such as life sciences, high-tech, fintech, and commodity trading.
            - Its central location in Switzerland provides excellent connectivity, with major transport hubs like Zurich Airport and Zurich Hauptbahnhof nearby.
            - Zug's business-friendly environment is characterized by low taxes, a stable political climate, and a high quality of life, making it an attractive destination for both startups and established enterprises.
            - The region also offers a robust infrastructure, including innovation platforms and institutions like the Switzerland Innovation Park Central and the Central Switzerland University of Applied Sciences and Arts.
            - With a strong network of industry-specific associations and a reputation for efficiency and professionalism, Zug is well-equipped to support business growth and innovation.
            - Whether you're in life sciences, high-tech, or finance, office space in Canton of Zug provides the ideal setting for your business to thrive.";

        var translatedExpectedOutput = "It is a guide to action that ensures that the military will forever heed Party commands.";


        // var factsField = settings.FactsField;
        var factsField = @"address: Neugasse 25, 6300 Zug, Switzerland; population: 30000";

        string pythonDLL = @"C:\Python312\python312.dll";
        string setupFile = Path.GetFullPath(@"..\Cute.Lib\PythonScripts\setup.py");
        string evalGenerationFile = Path.GetFullPath(@"..\Cute.Lib\PythonScripts\eval_generation.py");
        string evalTranslationFile = Path.GetFullPath(@"..\Cute.Lib\PythonScripts\eval_translation.py");
        string evalSEOFile = Path.GetFullPath(@"..\Cute.Lib\PythonScripts\eval_seo.py");
        string llmModel = "gpt-4o-mini";
        double threshold = 0.7;

        try
        {
            Runtime.PythonDLL = pythonDLL; // Set the python dll path
            PythonEngine.Initialize();

            //ExecutePython(filePath: setupFile); // Execute the setup.py file first

            if (generationMetric != null)
            {
                metricsResult = generationMetric switch
                {
                    "answer" => EvaluateGeneration(evalGenerationFile, promptMainPrompt, generatedContentField, referenceContentField, factsField, 
                        generationMetric, llmModel, threshold),
                    "faithfulness" => EvaluateGeneration(evalGenerationFile, promptMainPrompt, generatedContentField, referenceContentField, factsField, 
                        generationMetric, llmModel, threshold),
                    "all" => EvaluateGeneration(evalGenerationFile, promptMainPrompt, generatedContentField, referenceContentField, factsField, 
                        generationMetric, llmModel, threshold),
                    _ => throw new ArgumentException("Invalid metric provided"),
                };
            }
            else if (translationMetric != null)
            {
                metricsResult = translationMetric switch
                {
                    "gleu" => EvaluateTranslation(evalTranslationFile, promptMainPromptTrans, translatedContentField, translatedExpectedOutput, 
                        translationMetric, llmModel, threshold),
                    "meteor" => EvaluateTranslation(evalTranslationFile, promptMainPromptTrans, translatedContentField, translatedExpectedOutput, 
                        translationMetric, llmModel, threshold),
                    "lepor" => EvaluateTranslation(evalTranslationFile, promptMainPromptTrans, translatedContentField, translatedExpectedOutput, 
                        translationMetric, llmModel, threshold),
                    "all" => EvaluateTranslation(evalTranslationFile, promptMainPromptTrans, translatedContentField, translatedExpectedOutput, 
                        translationMetric, llmModel, threshold),
                    _ => throw new ArgumentException("Invalid metric provided"),
                };
            }
            else if (seoMetric != null)
            {
                // TODO: Implement SEO evaluation
            }
            else
            {
                Console.WriteLine("No metric provided for evaluation");
            }

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
        catch (NotSupportedException)
        {
            /*
            BinaryFormatter serialization and deserialization are disabled within this application.
            This is a known issue when using .NET 8, as BinaryFormatter serialization and deserialization are disabled by default for security reasons.
            */
        }

        Console.WriteLine(metricsResult);

        return 0;
    }

    private static string? EvaluateGeneration(string filePath, string prompt, string actualOutput, string expectedOutput, 
        string retrievalContext, string metric, string llmModel, double threshold)
    {
        string? result;

        // Define the Python parameters for the Generation test case
        PyObject? pyResult;
        PyString? pyGenMetric;
        PyString pyLlmModel = new(llmModel);
        PyFloat pyThreshold = new(threshold);
        PyString pyInput = new(prompt);
        PyString pyActualOutput = new(actualOutput);
        PyString pyExpectedOutput = new(expectedOutput);
        PyString pyRetrievalContext = new(retrievalContext);

        // First, execute __init__ method to initialize the EvalGenearation class (eval_generation.py)
        PyObject? genEvalObj = ExecutePython(
            filePath: filePath, 
            pyClass: "EvalGeneration", 
            pyMethod: "__init__", 
            pyArgs: [pyLlmModel, pyThreshold, pyInput, pyActualOutput, pyExpectedOutput, pyRetrievalContext]
        ) ?? throw new NullReferenceException("genEvalObj is null");
        
        switch (metric)
        {
            case "answer":
                pyGenMetric = new PyString(metric); // Specify the metric to be used for evaluation
                /* 
                    Execute the measure method on the EvalGeneration class 
                    (in eval_generation.py) to get the result of a answer relevancy metric
                */
                pyResult = ExecutePython(
                    filePath: filePath,
                    pyClass: "EvalGeneration",
                    pyMethod: "measure",
                    pyArgs: [genEvalObj, pyGenMetric]);

                // Check if pyResult is not null before proceeding
                if (pyResult == null) { throw new NullReferenceException("pyResult for answer relevancy metric is null"); }

                result = pyResult.AsManagedObject(typeof(string)) as string; // Convert the PyObject to a string
                pyGenMetric.Dispose(); // Dispose object to free up memory
                break;

            case "faithfulness":
                pyGenMetric = new PyString(metric); // Specify the metric to be used for evaluation
                /* 
                    Execute the measure method on the EvalGeneration class 
                    (in eval_generation.py) to get the result of faithfulness metric
                */
                pyResult = ExecutePython(
                    filePath: filePath,
                    pyClass: "EvalGeneration",
                    pyMethod: "measure",
                    pyArgs: [genEvalObj, pyGenMetric]);

                // Check if pyResult is not null before proceeding
                if (pyResult == null) { throw new NullReferenceException("pyResult for faithfulness metric is null"); }

                result = pyResult.AsManagedObject(typeof(string)) as string; // Convert the PyObject to a string
                pyGenMetric.Dispose(); // Dispose object to free up memory
                break;

            case "all":
                /* 
                    Execute evaluate method on the EvalGeneration class 
                    (in eval_generation.py) to get the result of the evaluation
                */
                pyResult = ExecutePython(
                    filePath: filePath,
                    pyClass: "EvalGeneration",
                    pyMethod: "evaluate",
                    pyArgs: [genEvalObj]);

                // Check if pyResult is not null before proceeding
                if (pyResult == null) { throw new NullReferenceException("pyResult for all metrics is null"); }

                result = pyResult.AsManagedObject(typeof(string)) as string; // Convert the PyObject to a string
                break;

            default:
                throw new ArgumentException("Invalid metric provided");
        }

        // Dispose all the Python objects to free up memory
        pyLlmModel.Dispose();
        pyThreshold.Dispose();
        pyInput.Dispose();
        pyActualOutput.Dispose();
        pyExpectedOutput.Dispose();
        pyRetrievalContext.Dispose();

        return result;
    }

    private static string? EvaluateTranslation(string filePath, string prompt, string actualOutput, string expectedOutput, 
        string metric, string llmModel, double threshold)
    {
        string? result;

        // Define the Python parameters for the Translation test case
        PyObject? pyResult;
        PyString? pyTranslationMetric;
        PyString pyLlmModel = new(llmModel);
        PyFloat pyThreshold = new(threshold);
        PyString pyInput = new(prompt);
        PyString pyActualOutput = new(actualOutput);
        PyString pyExpectedOutput = new(expectedOutput);

        // First, execute __init__ method to initialize the EvalTranslation class (eval_translation.py)
        PyObject? translationEvalObj = ExecutePython(
            filePath: filePath, 
            pyClass: "EvalTranslation", 
            pyMethod: "__init__", 
            pyArgs: [pyLlmModel, pyThreshold, pyInput, pyActualOutput, pyExpectedOutput]
        ) ?? throw new NullReferenceException("translationEvalObj is null");

        switch (metric)
        {
            case "gleu":
                pyTranslationMetric = new PyString(metric); // Specify the metric to be used for evaluation
                /* 
                    Execute the measure method on the EvalTranslation class 
                    (in eval_translation.py) to get the result of gleu metric
                */
                pyResult = ExecutePython(
                    filePath: filePath,
                    pyClass: "EvalTranslation",
                    pyMethod: "measure",
                    pyArgs: [translationEvalObj, pyTranslationMetric]
                );

                // Check if pyResult is not null before proceeding
                if (pyResult == null) { throw new NullReferenceException("pyResult for gleu measure is null"); }

                result = pyResult.AsManagedObject(typeof(string)) as string; // Convert the PyObject to a string
                pyTranslationMetric.Dispose(); // Dispose object to free up memory              
                break;

            case "meteor":
                pyTranslationMetric = new PyString(metric); // Specify the metric to be used for evaluation
                /* 
                    Execute the measure method on the EvalTranslation class 
                    (in eval_translation.py) to get the result of meteor metric
                */
                pyResult = ExecutePython(
                    filePath: filePath,
                    pyClass: "EvalTranslation",
                    pyMethod: "measure",
                    pyArgs: [translationEvalObj, pyTranslationMetric]
                );

                // Check if pyResult is not null before proceeding
                if (pyResult == null) { throw new NullReferenceException("pyResult for meteor metric is null"); }

                result = pyResult.AsManagedObject(typeof(string)) as string; // Convert the PyObject to a string
                pyTranslationMetric.Dispose(); // Dispose object to free up memory               
                break;

            case "lepor":
                pyTranslationMetric = new PyString(metric); // Specify the metric to be used for evaluation
                /* 
                    Execute the measure method on the EvalTranslation class 
                    (in eval_translation.py) to get the result of lepor metric
                */
                pyResult = ExecutePython(
                    filePath: filePath,
                    pyClass: "EvalTranslation",
                    pyMethod: "measure",
                    pyArgs: [translationEvalObj, pyTranslationMetric]
                );

                // Check if pyResult is not null before proceeding
                if (pyResult == null) { throw new NullReferenceException("pyResult for lepor metric is null"); }

                result = pyResult.AsManagedObject(typeof(string)) as string; // Convert the PyObject to a string
                pyTranslationMetric.Dispose(); // Dispose object to free up memory
                break;

            case "all":
                /* 
                    Execute evaluate method on the EvalTranslation class 
                    (in eval_translation.py) to get the result of the evaluation
                */
                pyResult = ExecutePython(
                    filePath: filePath,
                    pyClass: "EvalTranslation",
                    pyMethod: "evaluate",
                    pyArgs: [translationEvalObj]
                );

                // Check if pyResult is not null before proceeding
                if (pyResult == null) { throw new NullReferenceException("pyResult for all metrics is null"); }

                result = pyResult.AsManagedObject(typeof(string)) as string; // Convert the PyObject to a string
                break;

            default:
                throw new ArgumentException("Invalid metric provided");
        }

        // Dispose all the Python objects to free up memory
        pyLlmModel.Dispose();
        pyThreshold.Dispose();
        pyInput.Dispose();
        pyActualOutput.Dispose();
        pyExpectedOutput.Dispose();

        return result;
    }

    private static string? EvaluateSEO(string filePath, string actualOutput, string keyword, string relatedKeywords, double threshold)
    {

        string? result = null;

        // Define the parameters for the SEO test case
        string seoActualOutput = @"
            <html>
                <head>
                    <title>SEO Title</title>
                    <meta name='description' content='SEO Description'>
                    <meta name='keywords' content='SEO, Keywords'>
                </head>
                <body>
                    <h1>SEO Heading</h1>
                    <p>SEO Content</p>
                </body>
        ";
        string seoKeyword = "Hybrid Office Space";
        string seoRelatedKeywords = "flexible office|virtual office|co-working space|shared office|meeting room|conference room|event space";

        // Define the Python parameters for the SEO test case
        PyFloat pySeoThreshold = new PyFloat(0.75);
        PyString pySeoActualOutput = new PyString(seoActualOutput);
        PyString pySeoKeyword = new PyString(seoKeyword);
        PyString pySeoRelatedKeywords = new PyString(seoRelatedKeywords);

        // First, execute __init__ method to initialize the EvalSEO class (eval_seo.py)
        PyObject? seoEvalObj = ExecutePython(
            filePath: filePath, 
            pyClass: "EvalSEO", 
            pyMethod: "__init__", 
            pyArgs: [pySeoThreshold, pySeoActualOutput]
        ) ?? throw new NullReferenceException("seoEvalObj is null");
        
        return result;
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