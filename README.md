[![Nuget][version-shield]][version-url][![contributors][contributors-shield]][contributors-url][![issues][issues-shield]][issues-url][![stars][stars-shield]][stars-url][![build][build-shield]][build-url][![forks][forks-shield]][forks-url]

<br /><div align="center"><a href="https://github.com/andresharpe/cute"><img src="https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-logo.png" alt="Logo" width="500"></a></div>

<p align="center">A Contentful Update Tool & Extractor</p>

<div align="center"><a href="https://github.com/andresharpe/cute/tree/master/source">View the Source Code</a> · <a href="https://www.nuget.org/packages/cute">Download @ Nuget</a></div><br />

# Introduction

***cute*** is a cross-platform CLI tool that brings several advanced features and capabilities to working with your content hosted on [Contentful](https://www.contentful.com).

<br /><div align="center"><img src="https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-overview-graphic.png" alt="cute help screenshot" width="70%"><br /></div>

## Key Features

- Bulk processing capabilities lets you download, modify and upload data in most popular formats including Comma delimited files (CSV), Tab delimited files (TSV), MS-Excel workbooks (XLSX), Javascript Object Notation (JSON) and YAML.
- Input data can be sourced and synced from many external sources including flat files, databases, webAPIs, your Contentful space or other popular sources like [WikiData](https://www.wikidata.org/).
- Content can be translated or even generated from scratch using popular technologies like [OpenAI](https://openai.com/) and [Azure AI Translator](https://azure.microsoft.com/en-us/products/ai-services/ai-translator).
- Deploy ***cute*** as a Web Server with [OpenTelemetry](https://opentelemetry.io/) compliant logging and a service terminal to reflect health, configuration and scheduled tasks.
- Support for structural subtyping through the `typegen` command option which exports TypeScript (TS) interface declarations. This feature is especially useful to keep your JavaScript or .NET projects in sync with your content types.
- ***cute*** auto-magically "learns" your Contentful space and generates required configuration nodes to enable process automation.
- Interact with ***Douglas***, cute's very own AI assistant that will answer questions about your content, or even help formulate queries to interact with your content.

> 💡 [Contentful](https://www.contentful.com) is a content infrastructure platform that lets you create, manage and distribute content to any platform. 
Contentful offers a simple UI to declare and manage a content model, independent from the presentation layer.

# Installation

## Firstly, make sure you have the Dotnet SDK 8.0 package installed.

For windows (cmd or powershell):
```
winget install Microsoft.DotNet.SDK.8
```

Or, on linux and iOS
``` 
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

## Then:
On windows you may have to close and re-open the command line prompt (or Windows Terminal).

Install the ***cute*** cli by typing.
```
dotnet tool install -g cute
```

## To test whether the installation worked
Simply type
```
cute
```
This will display the 
cute help. You are ready to go! 🚀

# Getting Help

```
cute --help
```
![cute help screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/help.png)

# Logging into Contentful
Start your ***cute*** session by running the login command. This will configure your Contentful session profile using the selected space, environment and API keys.
You can also enter your AI and translation services keys here. 
```
cute login
```

![cute login --help screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/login.png)

# Display a summary of your Contentful Space
Display a comprehensive overview of your Contentful session information including space, environment, content types and locales. Info related to CLI display settings is also shown. 
``` 
cute info
```
![cute info screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/info.png)

# Working with Content

The ```cute content``` and its respective command options represents the real workhorse of the cute tool. It essentially presents the user with a suite of bulk operation options to interact with their content in Contentful.

![cute content --help screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/content-help.png)

## Downloading data
Content can easily be downloaded from your Contentful space in one of several popular formats including Excel, comma separated (CSV), tab separated (TSV), JSON and YAML. If no format is specified, the downloaded file with default to the Excel format.

```
cute content download --content-type <contentType> 
cute content download --content-type <contentType> --format [excel|csv|tsv|json|yaml]
```
Issuing any ```content download``` command will yield a result similar to the display below.

![cute download screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/download.png)

Typing `cute download --help` will list all currently available options and usage.

```
USAGE:
    cute content download [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information
    -c, --content-type-id <ID>  The Contentful content type id
    -l, --locale <CODE>         The locale code (eg. 'en') to apply the command to. Default is all
    -f, --format <FORMAT>       The output format for the download operation (Excel/CSV/TSV/JSON/YAML)
    -p, --path <PATH>           The output path and filename for the download operation
```

## Uploading/Synchronizing Content

You can upload content from a local file to your Contentful space. The local file can be a previously downloaded and updated Excel, CSV, TSV, JSON or YAML file.

![cute upload progress screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/upload-progress.png)

***cute*** will prompt you to confirm a 2-digit code to prevent you from updating your content accidentally.

![cute upload screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/upload.png)

Typing `cute content upload --help` will show the full usage and options.

```
USAGE:
    cute content upload [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information
    -c, --content-type-id <ID>  The Contentful content type id
    -l, --locale <CODE>         The locale code (eg. 'en') to apply the command to. Default is all
    -p, --path <PATH>           The local path to the file containing the data to sync
    -f, --format <FORMAT>       The format of the file specified in '--path' (Excel/CSV/TSV/JSON/YAML)
    -m, --match-field <NAME>    The optional name of the field to match in addition to the entry id
    -a, --apply                 Apply and publish all the calculated changes. The default behaviour is to only list the detected changes
```

## Sync Content with APIs

You can synchronize your Contentful content with external APIs by using the `cute content sync-api` command option.

```
USAGE:
    cute content sync-api [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information
    -s, --space-id <ID>         The Contentful space identifier.
    -e, --environment-id <ID>   The Contentful environment identifier.
        --force                 Specifies whether warning prompts should be bypassed
    -k, --key                   The key of the cuteContentSyncApi entry
    -a, --apply                 Apply and publish all the required edits
    -u, --use-filecache         Whether or not to cache responses to a local file cache for subsequent calls
```

Prior to running the command, you should configure API settings and field mappings in your Contentful space under the ```cuteContentSyncApi``` content type.

If you have not yet created a `cuteContentSyncApi` content type in your Contentful space you can add it and configure the fields as per the screenshot below:

![contentful cuteContentSyncApi model screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteContentSyncApi-model.png)

Then click the 'Add Entry' button:

![contentful contentSyncApi screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentSyncApi.png)

Create a new entry for the relevant content as per the graphic below:

![contentful contentSyncApi yaml screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentSyncApi-yaml.png)

We're going to sync to the [users endpoint](https://jsonplaceholder.typicode.com/users) over at [{JSON} Placeholder](https://jsonplaceholder.typicode.com/) to populate our `Users` content. A small sample is shown below:

```json
[
  {
    "id": 1,
    "name": "Leanne Graham",
    "username": "Bret",
    "email": "Sincere@april.biz",
    "address": {
      "street": "Kulas Light",
      "suite": "Apt. 556",
      "city": "Gwenborough",
      "zipcode": "92998-3874",
      "geo": {
        "lat": "-37.3159",
        "lng": "81.1496"
      }
    },
    "phone": "1-770-736-8031 x56442",
    "website": "hildegard.org",
    "company": {
      "name": "Romaguera-Crona",
      "catchPhrase": "Multi-layered client-server neural-net",
      "bs": "harness real-time e-markets"
    }
  },
  {
    "id": 2,
    "name": "Ervin Howell",
    "username": "Antonette",
    "email": "Shanna@melissa.tv",
    "address": {
      "street": "Victor Plains",
      "suite": "Suite 879",
      "city": "Wisokyburgh",
      "zipcode": "90566-7771",
      "geo": {
        "lat": "-43.9509",
        "lng": "-34.4618"
      }
    },
    "phone": "010-692-6593 x09125",
    "website": "anastasia.net",
    "company": {
      "name": "Deckow-Crist",
      "catchPhrase": "Proactive didactic contingency",
      "bs": "synergize scalable supply-chains"
    }
  }
]
```
Our `Users` content entry has a few matching fields and some which we'll map.

![contentful Users model screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-user.png)

Basic identifiers, API headers and endpoints as well as field mappings can be configured as per the code snippet below.

```yaml
# dataUser.yaml

contentType: user
contentKeyField: "id.en"
contentDisplayField: "name.en"

endPoint: https://jsonplaceholder.typicode.com/users

headers:
    Accept: "application/json"
    User-Agent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"

mapping:
    - fieldName: id.en
      expression: '{{ row.id }}'

    - fieldName: userName.en
      expression: '{{ row.username }}'

    - fieldName: name.en
      expression: '{{ row.name }}'

    - fieldName: email.en
      expression: '{{ row.email }}'

    - fieldName: phoneNumber.en
      expression: '{{ row.phone }}'
```

Running the `cute content sync-api -c dataUser.

![cute content sync-api screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/sync-api.png)

## Translating Content

You can translate your content into languages of your choice using various popular AI translation services including Azure, DeepL, Google Translation and ChatGPT.

Typing `cute content translate --help` will show the full usage and options.

```
USAGE:
    cute content translate [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information
    -c, --content-type-id <ID>  The Contentful content type id
    -f, --field                 The field(s) which will be translated. If not specified, all localized fields will be translated
    -l, --locale <CODE>         The locale code (eg. 'en') linked to the relevant language. If unspecified, all localized fields and languages will be translated
    -k, --key                   The key of a single entry to be translated
    -a, --apply                 Apply and publish all the calculated changes. The default behaviour is to only list the detected changes
```

### Criteria for translating an entry

***cute*** will filter your content entries and process all entries where:
- The target translated content field is empty, *AND*
- The default locale content field (source) is not empty. 

### Working with multiple AI Translators

***cute*** let's you work with one or several AI translation services, depending on your requirement. You're not limited to a single translation service for all your languages. You can choose the translation service that yields the best result for all or any of the languages you are translating content to.

Within your Contentful model, locate the ```cuteLanguageTranslation``` section. Here you add `language` entries and assign `Azure`, `Google`, `DeepL` or `GPT4o` to the `translationService` field.

If no translation service is specified, Azure Translation Service will be used.

### Example

I work in the admissions department for a technical college with students from all over the globe. I'd like to translate the opening and closing paragraph of our acceptance letter for French, Russian, Georgian and Spanish.

```
cute content translate -c dataAcceptanceLetter --field paragraphOpening, paragraphClosing --locale fr,ru,ka,es
```
This command will get all the dataAcceptanceLetter entries and will translate opening and closing paragraph fields to locales fr (French), ru (Russian), ka (Georgian) and es (Spanish) where applicable.

# Running cute as a Server

***cute*** can be run as a stand-alone server in two modes:
- Schedule and run all or specific entries from the `CuteSchedule` content type in your Contentful space.
- Webhooks mode will process callbacks configured in—and triggered from—your Contentful space.

## Scheduler

Typing `cute server scheduler --help` will show the full usage and options.

```
USAGE:
    cute server scheduler [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information
    -p, --port                  The port to listen on
    -k, --key                   CuteSchedule Key
```

Prior to running ***cute*** as a scheduler for the first time you will need to define a `cuteSchedule` content type in your Contentful space as per the attached screenshot below:

![contentful cuteSchedule model screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteSchedule-model.png)

You can then configure your scheduled commands, all of which will be loaded when the scheduler is started. Alternatively, scheduled commands can be individually invoked by referencing them with the optional `--key` command option.

The screenshot below illustrates how we create a scheduled entry for the `dataUser` content type which we synced from an external API.

We'll configure it to invoke the `dataUser` entry we created in the `cuteContentSyncApi` section, and we'll schedule it to run at 2:01 p.m. every day.

![contentful cuteSchedule screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteSchedule-dataUser.png)

All that remains is to run the command. We'll invoke it to listen on port 2345.

```shell
cute server scheduler --port 2345
```
![cute server scheduler terminal ready](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-server-scheduler.png)

***cute*** also exposes a monitoring interface on the port that the server is running. All the `cuteSchedule` entries are listed along with their configured properties.

![cute server scheduler monitor](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/localhost-cute-scheduler.png)

When the scheduled entries are triggered, either by a cron schedule or a sequenced condition, the relevant command will be run and information will be displayed as per the screenshot below:

![cute server scheduler terminal output](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-server-scheduler-output.png)


# Generating strong JavaScript or .NET Types

***cute*** supports structural subtyping through the `type scaffold` command option. You can export TypeScript (TS) or .NET (CS) interface declarations, or a simple Excel file with individual worksheets detailing your content model. This feature is especially useful to keep your JavaScript or .NET projects in sync with your content types.

```
USAGE:
    cute type scaffold [OPTIONS]

OPTIONS:
    -h, --help            Prints help information
    -c, --content-type    Specifies the content type to generate types for. Default is all
    -o, --output          The local path to output the generated types to
    -l, --language        The language to generate types for (TypeScript/CSharp)
    -n, --namespace       The optional namespace for the generated type
```

# Content generation using OpenAI

You can generate content using OpenAI Generative Pre-trained Transformer (GPT) using the bulk operation feature of ***cute***.

OpenAI ChatGPT uses a state-of-the-art Large Language Model (LLM) to generate text that is difficult to distinguish from human-written content.

Prompts and system messages that are generally used to interact with ChatGPT are configured and persisted in your Contentful space. This is especially useful as your AI prompts are persisted and backed up in the cloud right alongside your content.

Prompts can be added and configured in the ```🤖 Cute / ContentGenerate``` section your Contentful space. A typical prompt entry has an id, a system message, a prompt, points to a content type and field.  Something like :-

|Title|Note|
|-|-|
|title|A short title by which the prompt entry is referred to.|
|systemMessage|Used to communicate instructions or provide context to the model at the beginning of a conversation.|
|prompt|A question or instruction that you issue to ChatGPT. This *prompt* is used to generate an appropriate response.|
|deploymentModel|Select which Large Language Model (LLM) is used for your interaction.|
|maxTokenLimit|The maximum tokens to be used for the interaction|
|temperature|Controls the randomness of the generated response. A higher temperature value increases randomness, making the responses more diverse and creative, while a lower value makes them more focused and deterministic.|
|topP|Controls the diversity of the generated output by truncating the probability distribution of words. It functions as a filter to determine the number of words or phrases the language model examines while predicting the next word. For instance, when the Top P value is set at 0.4, the model only considers 40% of the most probable words or phrases. A higher Top P value results in more diverse creative responses. A lower value will result in more focused and coherent responses.|
|frequencyPenalty|Controls the repetitiveness of words in generated responses. Increasing this value is like telling ChatGPT not to use the same words too often.|
|presencePenalty|Manages the appearance of words in generated text based on their position, rather than frequency. This parameter encourages ChatGPT to employ a more diverse vocabulary|
|cuteDataQueryEntry|A link to the associated data query in ```🤖 Cute / DataQuery```|
|promptOutputContentField|The target field of the content entry where the generated response is stored.|

```
DESCRIPTION:
Generate content using a Large Language Model (LLM).

USAGE:
    cute content generate [OPTIONS]

OPTIONS:
    -h, --help          Prints help information
    -k, --key           The key of the 'cuteContentGenerate' entry
    -a, --apply         Apply and publish all the required edits
    -o, --operation     Specify the generation operation to perform. (GenerateSingle, GenerateParallel,
                                      GenerateBatch or ListBatches)
```


# Command Structure for v2.0

The full command structure for the usage of version 2 of ***cute*** can be found [in this document](./docs/CUTE-USAGE.md).

# Contributing to Cute

We welcome community pull requests for bug fixes, enhancements, and documentation. See [How to contribute](./docs/CONTRIBUTING.md) for more information.

[version-shield]: https://img.shields.io/nuget/v/cute.svg?style=for-the-badge

[version-url]: https://www.nuget.org/packages/cute

[build-shield]: https://img.shields.io/github/actions/workflow/status/andresharpe/cute/cute-cd.yaml?branch=main&event=push&label=Build&style=for-the-badge

[build-url]: https://github.com/andresharpe/cute/actions/workflows/cute-cd.yaml?query=branch%3Amain

[contributors-shield]: https://img.shields.io/github/contributors/andresharpe/cute.svg?style=for-the-badge

[contributors-url]: https://github.com/andresharpe/cute/graphs/contributors

[forks-shield]: https://img.shields.io/github/forks/andresharpe/cute.svg?style=for-the-badge

[forks-url]: https://github.com/andresharpe/cute/network/members

[stars-shield]: https://img.shields.io/github/stars/andresharpe/cute.svg?style=for-the-badge

[stars-url]: https://github.com/andresharpe/cute/stargazers

[issues-shield]: https://img.shields.io/github/issues/andresharpe/cute.svg?style=for-the-badge

[issues-url]: https://github.com/andresharpe/cute/issues