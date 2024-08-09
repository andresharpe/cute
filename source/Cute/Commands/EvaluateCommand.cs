using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Services;
using Python.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace Cute.Commands;

public sealed class EvaluateCommand : LoggedInCommand<EvaluateCommand.Settings>
{
    private readonly ILogger<EvaluateCommand> _logger;

    private IReadOnlyDictionary<string, string?> _allEnvSettings = new Dictionary<string, string?>();

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
        [Description("The seo metric to evaluate.")]
        public string? SeoMetric { get; set; } = default!;

        [CommandOption("-i|--prompt-id")]
        [Description("The id of the Contentful prompt entry to generate prompts from.")]
        public string PromptId { get; set; } = default!;

        [CommandOption("-p|--prompt-field")]
        [Description("The field containing the prompt template for the LLM.")]
        public string PromptField { get; set; } = default!;

        [CommandOption("-c|--generated-content")]
        [Description("The field containing the LLM's generated content.")]
        public string GeneratedContentField { get; set; } = default!;

        [CommandOption("-r|--reference-content")]
        [Description("The field containing the reference content.")]
        public string ReferenceContentField { get; set; } = default!;

        [CommandOption("-f|--facts")]
        [Description("The field containing the facts for the generation evaluation.")]
        public string FactsField { get; set; } = default!;

        [CommandOption("-k|--keyword")]
        [Description("The keyword used to evaluate SEO.")]
        public string KeywordField { get; set; } = default!;

        [CommandOption("-w|--related-keywords")]
        [Description("The list of related keywords to evaluate SEO.")]
        public string RelatedKeywordsField { get; set; } = default!;

        [CommandOption("-u|--seo-input-method")]
        [Description("The input method to evaluate SEO. Can be 'url' or 'content'. Default is 'url'.")]
        public string SeoInputField { get; set; } = "url";

        [CommandOption("-h|--threshold")]
        [Description("The threshold to either pass or fail the metric's evaluation. Default is 0.7.")]
        public float Threshold { get; set; } = 0.7f;

        [CommandOption("-m|--llm-model")]
        [Description("The LLM model to use for the evaluation. Default is 'gpt-4o'.")]
        public string LlmModel { get; set; } = "gpt-4o";
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

#pragma warning disable CS1998

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await base.ExecuteAsync(context, settings);

        var pythonPath = InstallPythonIfNeededAndReturnPath();

        var metricsResult = string.Empty;

        var generationMetric = settings.GenerationMetric;
        var translationMetric = settings.TranslationMetric;
        var seoMetric = settings.SeoMetric;
        var promptMainPrompt = settings.PromptField;
        var generatedContentField = settings.GeneratedContentField;
        var referenceContentField = settings.ReferenceContentField;
        var factsField = settings.FactsField;
        var keywordField = settings.KeywordField;
        var relatedKeywordsField = settings.RelatedKeywordsField;
        var seoInputField = settings.SeoInputField;
        var threshold = settings.Threshold;
        var llmModel = settings.LlmModel;

        _allEnvSettings = _appSettings.GetSettings();

        if (!_allEnvSettings.TryGetValue("Cute__PythonDLL", out var pythonDLL))
        {
            pythonDLL = Path.Combine(pythonPath, "python312.dll");
        }

        var runTimeScriptFolder = Path.Combine((Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty)
            , "PythonScripts");

        var setupFile = Path.Combine(runTimeScriptFolder, "setup.py");

        var evalGenerationFile = Path.Combine(runTimeScriptFolder, "eval_generation.py"); // Path to the eval_generation.py file

        var evalTranslationFile = Path.Combine(runTimeScriptFolder, "eval_translation.py"); // Path to the eval_translation.py file

        var evalSEOFile = Path.Combine(runTimeScriptFolder, "eval_seo.py"); // Path to the eval_seo.py file

        EnsureAllFilesExist(pythonDLL!, evalGenerationFile, evalSEOFile, evalTranslationFile, setupFile);

        try
        {
            Runtime.PythonDLL = pythonDLL; // Set the python dll path
            PythonEngine.Initialize();

            ExecutePython(filePath: setupFile); // Execute the setup.py file to import any missing libraries

            if (generationMetric != null && translationMetric == null && seoMetric == null)
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
            else if (translationMetric != null && generationMetric == null && seoMetric == null)
            {
                metricsResult = translationMetric switch
                {
                    "gleu" => EvaluateTranslation(evalTranslationFile, promptMainPrompt, generatedContentField, referenceContentField,
                        translationMetric, llmModel, threshold),
                    "meteor" => EvaluateTranslation(evalTranslationFile, promptMainPrompt, generatedContentField, referenceContentField,
                        translationMetric, llmModel, threshold),
                    "lepor" => EvaluateTranslation(evalTranslationFile, promptMainPrompt, generatedContentField, referenceContentField,
                        translationMetric, llmModel, threshold),
                    "all" => EvaluateTranslation(evalTranslationFile, promptMainPrompt, generatedContentField, referenceContentField,
                        translationMetric, llmModel, threshold),
                    _ => throw new ArgumentException("Invalid metric provided"),
                };
            }
            else if (seoMetric != null && generationMetric == null && translationMetric == null)
            {
                metricsResult = EvaluateSEO(evalSEOFile, seoInputField, keywordField, relatedKeywordsField, threshold);
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

    private static void EnsureAllFilesExist(params string[] files)
    {
        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                throw new CliException($"The required file '{file}' does not exist.");
            }
        }
    }

    private string InstallPythonIfNeededAndReturnPath()
    {
        // TODO: Make this work for non windows enviroments
        // We will assume the PATH for now - but can be extracted by running a new process

        var pythonPath = Environment.GetEnvironmentVariable("PATH")?
            .Split(';')
            .FirstOrDefault(p => p.EndsWith(@"\Python312\", StringComparison.OrdinalIgnoreCase));

        if (pythonPath is null)
        {
            _console.WriteRuler();
            _console.WriteBlankLine();
            _console.WriteNormal("You need Python, let's get it for you...");
            _console.WriteBlankLine();

            var process = new Process();
            process.StartInfo.FileName = "winget";
            process.StartInfo.Arguments = "install Python.Python.3.12";
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();
            var installResult = process.StandardOutput.ReadToEnd();

            _console.WriteNormal("Completed...");

            if (!installResult.Contains("Successfully installed"))
            {
                throw new CliException($"You need Python installed. On Windows you can simply use 'winget install Python.Python.3.12'\n{installResult}");
            }

            _console.WriteBlankLine();
            _console.WriteRuler();
            _console.WriteBlankLine();
        }

        return $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\Programs\\Python\\Python312\\";
    }

    private string? EvaluateGeneration(string filePath, string prompt, string actualOutput, string expectedOutput,
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

    private string? EvaluateTranslation(string filePath, string prompt, string actualOutput, string expectedOutput,
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

    private string? EvaluateSEO(string filePath, string seoInput, string keyword, string relatedKeywords, double threshold)
    {
        string? result = null;

        // Define the Python parameters for the SEO test case
        PyFloat pySeoThreshold = new(threshold);
        PyString pySeoInput = new(seoInput);
        PyString pySeoKeyword = new(keyword);
        PyString pySeoRelatedKeywords = new(relatedKeywords);

        // First, execute __init__ method to initialize the EvalSEO class (eval_seo.py)
        PyObject? seoEvalObj = ExecutePython(
            filePath: filePath,
            pyClass: "EvalSEO",
            pyMethod: "__init__",
            pyArgs: [pySeoInput, pySeoKeyword, pySeoRelatedKeywords, pySeoThreshold]
        ) ?? throw new NullReferenceException("seoEvalObj is null");

        return result;
    }

    private PyObject? ExecutePython(string filePath, string? pyClass = null, string? pyMethod = null, List<PyObject>? pyArgs = null)
    {
        PyObject? result = null; // Create a PyObject to hold the result
        string file = Path.GetFullPath(filePath);

        if (!PythonEngine.IsInitialized) // Since using asp.net, we may need to re-initialize
        {
            PythonEngine.Initialize();
        }

        using var gil = Py.GIL();

        using var scope = Py.CreateScope(); // create a Python scope

        string code = File.ReadAllText(file); // Get code as raw text

        var codeCompiled = PythonEngine.Compile(code, file); // Compile the code/file

        scope.Set("settings", _allEnvSettings.ToPython());

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
                    param[i] = pyArgs[i - 1]; // Append the method arguments to the PyObject[] array
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

        return result; // Return the result
    }
}