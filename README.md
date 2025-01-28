[![Nuget][version-shield]][version-url][![contributors][contributors-shield]][contributors-url][![issues][issues-shield]][issues-url][![stars][stars-shield]][stars-url][![build][build-shield]][build-url][![forks][forks-shield]][forks-url]

<br /><div align="center"><a href="https://github.com/andresharpe/cute"><img src="https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-logo.png" alt="Logo" width="500"></a></div>

<p align="center">A Contentful Update Tool & Extractor</p>

<div align="center"><a href="https://github.com/andresharpe/cute/tree/master/source">View the Source Code</a> · <a href="https://www.nuget.org/packages/cute">Download @ Nuget</a></div><br />

<!-- TOC ignore:true -->
### Table of Content
<details>
<summary>Expand/Collapse</summary>

<!-- TOC -->

- [Introduction](#introduction)
  - [What is ***cute***?](#what-is-cute)
  - [Key Features](#key-features)
  - [Installation](#installation)
- [Commands: Your ***cute*** Session](#commands-your-cute-session)
  - [Getting `help`](#getting-help)
  - [Configure your ***cute*** session using `login`](#configure-your-cute-session-using-login)
  - [Display your Contentful Space `info`](#display-your-contentful-space-info)
- [Commands: Working with `content`](#commands-working-with-content)
  - [`download` Content](#download-content)
  - [`upload` and Synchronize Content](#upload-and-synchronize-content)
  - [Content Aggregates using `join`](#content-aggregates-using-join)
    - [Example](#example)
  - [Sync Content with APIs using `sync-api`](#sync-content-with-apis-using-sync-api)
    - [Example](#example-1)
  - [`generate` Content using OpenAI](#generate-content-using-openai)
    - [Configuring AI Content Generation](#configuring-ai-content-generation)
    - [Example 1](#example-1)
    - [Example 2](#example-2)
  - [`translate` Content](#translate-content)
    - [Criteria for translating an entry](#criteria-for-translating-an-entry)
    - [Working with multiple AI Translators](#working-with-multiple-ai-translators)
    - [Example](#example-2)
- [Commands: Running ***cute*** as a `server`](#commands-running-cute-as-a-server)
  - [Run ***cute*** as a `scheduler` server](#run-cute-as-a-scheduler-server)
  - [Run ***cute*** as a `webhooks` server](#run-cute-as-a-webhooks-server)
    - [Example](#example-3)
- [Commands: Manage Content Types using `type`](#commands-manage-content-types-using-type)
  - [`scaffold` strong JavaScript or .NET Types](#scaffold-strong-javascript-or-net-types)
- [Configuring ***cute*** in Contentful](#configuring-cute-in-contentful)
  - [Scaffold ***cute*** Content using `content testdata`](#scaffold-cute-content-using-content-testdata)
  - [Configuring `cuteContentJoin`](#configuring-cutecontentjoin)
  - [Configuring `cuteContentSyncApi`](#configuring-cutecontentsyncapi)
  - [Configuring `cuteContentGenerate`](#configuring-cutecontentgenerate)
  - [Configuring `cuteDataQuery`](#configuring-cutedataquery)
    - [Example 1](#example-1-1)
    - [Example 2](#example-2-1)
- [Command Structure for v2.0](#command-structure-for-v20)
- [Contributing to Cute](#contributing-to-cute)

<!-- /TOC -->
</details>
</br>

# Introduction

## What is ***cute***?

***cute*** is a cross-platform CLI tool that brings several advanced features and capabilities including bulk operations, AI generation and translation to working with your content hosted on [Contentful](https://www.contentful.com).

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

## Installation

Start by ensuring you have the required version of the [.NET SDK installed](https://dotnet.microsoft.com/en-us/download) by confirming what version ***cute*** is currently running on [here](https://github.com/andresharpe/cute/blob/master/source/Cute/Cute.csproj).

> 💡 Depending on your operating system, you can also use package managers like [WinGet](https://learn.microsoft.com/en-us/windows/package-manager/winget/), [apt-get](https://linux.die.net/man/8/apt-get) and [Homebrew](https://brew.sh/) to manage your installation from the shell.

Install the ***cute*** CLI tool by running the command listed below:
```powershell
dotnet tool install --global cute
```
Alternatively, if you already have ***cute*** installed but would like to update to the latest version you can simply run the command listed below:
```powershell
dotnet tool update --global cute
```
Confirm your installation typing `cute` and you should see a display similar to the screenshot below listing the installed version and help:

![cute help screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/help.png)

You are ready to go, let's get started! 🚀

[Back to Index](#table-of-content)

# Commands: Your ***cute*** Session

## Getting `help`

You can easily access help for ***cute*** and its various command options by entering the relevant command, followed by `-h` or `--help` in the command shell:
```powershell
cute --help
```
![cute help screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/help.png)

[Back to Index](#table-of-content)

## Configure your ***cute*** session using `login`
Start your ***cute*** session by running the login command. This will configure your Contentful session profile using the selected space, environment and API keys.
You can also enter your AI and Translation service credentials here. 
```powershell
cute login
```

![cute login --help screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/login.png)

[Back to Index](#table-of-content)

## Display your Contentful Space `info`
Display a comprehensive overview of your Contentful session information including space, environment, content types and locales. Info related to CLI display settings is also shown. 
```powershell 
cute info
```
![cute info screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/info.png)

[Back to Index](#table-of-content)

# Commands: Working with `content`

The `cute content` and its respective command options represents the real workhorse of the ***cute*** tool. It essentially presents the user with a suite of bulk operation options to interact with their content in Contentful.

![cute content --help screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/content-help.png)

## `download` Content
Content can easily be downloaded from your Contentful space in one of several popular formats including Excel, comma separated (CSV), tab separated (TSV), JSON and YAML. If no format is specified, the downloaded file will default to the Excel format.

```powershell
cute content download --content-type <contentType> 
cute content download --content-type <contentType> --format [excel|csv|tsv|json|yaml]
```
Issuing any `content download` command will yield a result similar to the display below.

![cute download screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/download.png)

Typing `cute content download --help` will list all currently available options and usage.

```powershell
USAGE:
    cute content download [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information
    -c, --content-type-id <ID>  The Contentful content type id
    -l, --locale <CODE>         The locale code (eg. 'en') to apply the command to. Default is all
    -f, --format <FORMAT>       The output format for the download operation (Excel/CSV/TSV/JSON/YAML)
    -p, --path <PATH>           The output path and filename for the download operation
```
[Back to Index](#table-of-content)

## `upload` and Synchronize Content

You can upload content from a local file to your Contentful space. The local file can be a previously downloaded and updated Excel, CSV, TSV, JSON or YAML file.

![cute upload progress screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/upload-progress.png)

***cute*** will prompt you to confirm a 2-digit code to prevent you from updating your content accidentally.

![cute upload screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/upload.png)

Typing `cute content upload --help` will show the full usage and options.

```powershell
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

[Back to Index](#table-of-content)

## Content Aggregates using `join`

***cute*** offers a convenient and simple way of creating aggregated content types from two other content types within your space.

Let's start by having a look at the command options and usage. Typing `cute content join --help` will show the full usage and options.

```powershell
USAGE:
    cute content join [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information
    -k, --key                   The id of the Contentful join entry to generate content for
    -i, --entry-id              Id of source 2 entry to join content for
```

### Example

We'll start by configuring our content aggregate. We'll use our existing `diplomaCourse` content type and combine that with a new `branchLocation` content type in order to create an aggregated content view of `viewCourseByLocation`.

> 💡 See [this section](#configuring-cutecontentjoin) if you need to define a `cuteContentJoin` content type within your Contentful space if you're using this feature for the first time.

See the attached screenshot below. The `targetContentType` refers to our new aggregated content type. For source 1 our content type is `branchLocation` and we're selecting all entries `*`. For source 2 our content type is `diplomaCourse` and we're selecting only `MATHEMATICS`. 

![contentful cuteContentJoin entry configuration screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteContentJoin-entry.png)

Running the `cute content join --key ViewCourseByLocation` command yields the following output:

![cute content join shell output screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-content-join.png)

And the following entries are created in the `viewCourseByLocation` content type:

![cute content join result screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteContentJoin-result.png)

And if we look at one of the entries we can see that it concatenates the key and title fields and provides direct access to the underlying content entries of which the aggregate is comprised.

![cute content join result detail](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteContentJoin-result-detail.png)

[Back to Index](#table-of-content)

## Sync Content with APIs using `sync-api`

You can synchronize your Contentful content with external APIs by using the `cute content sync-api` command option.

```powershell
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

### Example

Prior to running the command, you should configure API settings and field mappings in your Contentful space under the `cuteContentSyncApi` content type.

> 💡 See [this section](#configuring-cutecontentsyncapi) if you need to define a `cuteContentSyncApi` content type within your Contentful space if you're using this feature for the first time.

Select `cuteContentSyncApi` and then click the 'Add Entry' button:

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

Running the `cute content sync-api -k dataUser -a` command yields the following output in the terminal:

![cute content sync-api screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/sync-api.png)

[Back to Index](#table-of-content)

## `generate` Content using OpenAI

You can generate content using OpenAI Generative Pre-trained Transformer (GPT) using the bulk operation feature of ***cute***.

OpenAI ChatGPT uses a state-of-the-art Large Language Model (LLM) to generate text that is difficult to distinguish from human-written content.

Prompts and system messages that are generally used to interact with ChatGPT are configured and persisted in your Contentful space. This is especially useful as your AI prompts are persisted and backed up in the cloud right alongside your content.

> 💡 In order to use ***cute's*** content generation feature, you'll need a valid [OpenAI API key](https://platform.openai.com/settings/organization/api-keys) which is entered when you run the `cute login` command along with a valid endpoint and deployment model.

### Configuring AI Content Generation

`cuteContentGenerate` entries can be added and configured under the `🤖 Cute / ContentGenerate` view of your Contentful space.

[This section](#configuring-cutecontentgenerate) outlines how to create a `cuteContentGenerate` content type in your Contentful space if you have not done so yet.

A `cuteContentGenerate` entry has an id, a system message, a prompt, points to a data query and an output field.  Something like :-

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
|cuteDataQueryEntry|A link to the associated data query in `🤖 Cute / DataQuery`. See [this section](#configuring-data-queries-in-cute) for a detailed overview of creating `cuteDataQuery` entries.|
|promptOutputContentField|The target field of the content entry where the generated response is stored.|

```powershell
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

### Example 1

Let's extend our earlier example of an educational institution by adding a content type `diplomaCourse` with a key, title and motivation. We'll use ***cute's*** content generation feature to generate a motivation on why it's a good idea to study a specific subject to advance your career.

We'll add a `cuteContentGenerate` entry called `Diploma Course | Motivation` in our Contentful space as per the attached screenshot below. We'll configure a `systemMessage` and `prompt` as per the highlighted areas and link the entry to our `Diploma Course | All` entry we configured in `cuteDataQuery` in [this section](#configuring-data-queries-in-cute) previously.

![contentful cuteSchedule model screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteContentGenerate-entry.png)

Let's run the content generate command and have a look at the results.

```powershell
cute content generate -k DiplomaCourse.Motivation
```
You'll notice from the output that the `Diploma Course | All` query returns 3 entries, Economics, Fine Art and Mathematics, and the ChatGPT responses are displayed for each prompt.

![cute content generate screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-content-generate.png)

And having a look at the `Mathematics`, `Fine Art` and `Economics` entries under the `diplomaCourse` content type confirms that the `motivation` field have been populated with the ChatGPT response for each respective entry. The screenshot below shows the `Economics` entry.

![cute content generate screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-diplomacourse-economics-entry.png)

### Example 2

For our second example, we'll work with the `viewCourseByLocation` content type we created [here](#example-2). This content type is an aggregate of `branchLocation` and `diplomaCourse` and we'll reference both those content types in our prompt.

As with example 1 above, we'll [start by configuring](#example-2) a `cuteDataQuery` entry that we'll reference in our `cuteContentGenerate` entry.

Let's add a `cuteContentGenerate` entry called `Course By Location | Motivation` in our Contentful space as per the attached screenshot below. We'll configure a `systemMessage` and `prompt` as per the highlighted areas and link the entry to our `Diploma Course | All` entry we configured in `cuteDataQuery` in [this section](#configuring-data-queries-in-cute) previously.

Note that in our `prompt` we're referencing properties from the aggregate data objects rather than the root object.

![contentful cuteSchedule model screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteContentGenerate-entry2.png)

Let's run the `cute content generate -k CourseByLocation.Motivation` command and have a look at the terminal output:

You'll notice from the output that the `Course By Location | All` query returns 2 entries, Madrid and London, and the ChatGPT responses are displayed for each prompt.

![cute content generate screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-content-generate2.png)

And having a look at the `United Kingdom | London | 1001 | Mathematics` and `Spain | Madrid | 2001 | Mathematics` entries under the `viewCourseByLocation` content type confirms that the `motivation` field have been populated with the ChatGPT response for each respective entry. The screenshot below shows the `Spain | Madrid | 2001 | Mathematics` entry.

![cute content generate screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-viewCourseByLocation-madrid-entry.png)

[Back to Index](#table-of-content)

## `translate` Content

You can translate your content into languages of your choice using various popular AI translation services including Azure, DeepL, Google Translation and ChatGPT.

Typing `cute content translate --help` will show the full usage and options.

```powershell
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

Within your Contentful model, locate the `cuteLanguageTranslation` section. Here you add `language` entries and assign `Azure`, `Google`, `DeepL` or `GPT4o` to the `translationService` field.

If no translation service is specified, Azure Translation Service will be used.

### Example

I work in the admissions department for a technical college with students from all over the globe. I'd like to translate the opening and closing paragraph of our acceptance letter for French, Russian, Georgian and Spanish.

```powershell
cute content translate -c dataAcceptanceLetter --field paragraphOpening, paragraphClosing --locale fr,ru,ka,es
```
This command will get all the dataAcceptanceLetter entries and will translate opening and closing paragraph fields to locales fr (French), ru (Russian), ka (Georgian) and es (Spanish) where applicable.

[Back to Index](#table-of-content)

# Commands: Running ***cute*** as a `server`

***cute*** can be run as a stand-alone server in two modes:
- [Scheduler](#scheduler) mode runs all or specific entries from the `CuteSchedule` content type in your Contentful space.
- [Webhooks](#webhooks) mode will process callbacks configured in—and triggered from—your Contentful space.

## Run ***cute*** as a `scheduler` server

Typing `cute server scheduler --help` will show the full usage and options.

```powershell
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

```powershell
cute server scheduler --port 2345
```
![cute server scheduler terminal ready](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-server-scheduler.png)

***cute*** also exposes a monitoring interface on the port that the server is running. All the `cuteSchedule` entries are listed along with their configured properties.

![cute server scheduler monitor](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/localhost-cute-scheduler.png)

When the scheduled entries are triggered, either by a cron schedule or a sequenced condition, the relevant command will be run and information will be displayed as per the screenshot below:

![cute server scheduler terminal output](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-server-scheduler-output.png)

## Run ***cute*** as a `webhooks` server

Running ***cute*** in `server webhooks` mode is a convenient way to process any ***cute*** command resulting from a content event from within Contentful. The steps required are simple:

- Start a ***cute*** server in webhooks mode
- Configure a webhook in your Contentful space along with a relevant payload
- Trigger the event from within Contentful 

Typing `cute server webhooks --help` will show the full usage and options.

```powershell
USAGE:
    cute server webhooks [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information
    -p, --port                  The port to listen on
```

### Example

Let's start a ***cute*** server in webhooks mode by executing the `cute server webhooks --port 8080` in a shell of our choice.

The ***cute*** webhooks server is now ready and listening on port 8080, as per the screenshot below:

![cute server webhooks running](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-server-webhooks.png)

As with the scheduler mode, ***cute*** server also exposes a monitoring interface on the port that the server is running in webhooks mode. See the attached screenshot below:

![cute server scheduler monitor](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/localhost-cute-webhooks.png)

Next we'll configure a webhook within Contentful that will be triggered when we publish any changes to our `diplomaCourse` content type, be it an addition or change.

![contentful configure webhook](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-webhook-config.png)

All that remains is to test our webhook server. Now that the server is up and running, and we've configured the webhook for our `diplomaCourse` content type publish event, we can go ahead an add a new course and click the 'publish' button.

As per the attached screenshot animation below, after a few seconds the `motivation` field is populated.

![contentful webhook example](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-webhook-example.gif)

To enable local testing of our ***cute*** webhooks server, we've used [ngrok](https://ngrok.com/use-cases/webhook-testing). They have a [useful guide here](contentful-webhook-example) detailing how to test Contentful webhooks with local servers.

Contentful also has documentation on configuring and implementing webhooks [here](https://www.contentful.com/developers/docs/webhooks/).

[Back to Index](#table-of-content)

# Commands: Manage Content Types using `type`

## `scaffold` strong JavaScript or .NET Types

***cute*** supports structural subtyping through the `type scaffold` command option. You can export TypeScript (TS) or .NET (CS) interface declarations, or a simple Excel file with individual worksheets detailing your content model. This feature is especially useful to keep your JavaScript or .NET projects in sync with your content types.

```powershell
USAGE:
    cute type scaffold [OPTIONS]

OPTIONS:
    -h, --help            Prints help information
    -c, --content-type    Specifies the content type to generate types for. Default is all
    -o, --output          The local path to output the generated types to
    -l, --language        The language to generate types for (TypeScript/CSharp)
    -n, --namespace       The optional namespace for the generated type
```

[Back to Index](#table-of-content)

# Configuring ***cute*** in Contentful

***cute*** has a number of native content types that it uses internally to configure and enable a number of the bulk operation (`cuteContentSyncApi` and `cuteContentJoin`), AI options (`cuteDataQuery` and `cuteContentGenerate`), Server options (`cuteSchedule`) and other (`cuteLanguage`).

## Scaffold ***cute*** Content using `content testdata`

***cute*** offers a useful command that will do all the heavy lifting for you related to creating all native content types required by ***cute*** for all its advanced features.

Typing `cute content testdata --help` will show the full usage and options.

```powershell
USAGE:
    cute server webhooks [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information
    -s, --space-id <ID>         The Contentful space identifier
    -e, --environment-id <ID>   The Contentful environment identifier
        --force                 Specifies whether warning prompts should be bypassed
    -n, --number                The number of user entries to generate. (default=1000)
```

Let's execute the command and review the results.

> 💡 Remember to ensure your ***cute*** session is configured correctly. It might be a good idea to run `cute logout` followed by `cute login` prior to running this command to reset your session configuration and credentials.

```powershell
cute content testdata
```
The terminal output confirms that the cute content types have been generated, along with 10 entries for the `testUser` content type, as seen in the screenshot below:

![cute content testdata output](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-content-testdata.png)

Clicking the *Content model* tab in our Contentful space reveals that all of the required ***cute*** content types have been scaffolded. In addition there are a few test data content types to get you started. Feel free to delete those should you not require them.

![cute content types generated](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cute-native-content-types.png)

It might be a good idea to organize these content types under a separate view folder in Contentful. It's certainly not mandatory, but it might aid in overall organization of your data, presentation and native ***cute*** content types.

![configuring cute within Contentful](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cute-views.png)

## Configuring `cuteContentJoin`

If you're using the `cute content join` feature for the first time, you'll need to define a `cuteContentJoin` content type within your Contentful space in which to configure your aggregates, as per the attached screenshot below:

![contentful cuteContentJoin model screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteContentJoin-model.png)

## Configuring `cuteContentSyncApi`

If you're using the `cute content sync-api` feature for the first time, you'll need to define a `cuteContentSyncApi` content type within your Contentful space and configure the fields as per the screenshot below:

![contentful cuteContentSyncApi model screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteContentSyncApi-model.png)

## Configuring `cuteContentGenerate`

If it's the first time you're using the content generation feature of ***cute*** you will need to define a `cuteContentGenerate` content type in your Contentful space as per the attached screenshot below:

![contentful cuteSchedule model screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteContentGenerate-model.png)

## Configuring `cuteDataQuery`

Certain of the built-in features of ***cute***—like AI content generation—will operate on data sets which is the result of queries on your content in your Contentful space.

These queries are conveniently defined and persisted alongside your other content under the `🤖 Cute / DataQuery` section of your Contentful space.

If it's the first time you're configuring a data query, you will need to define a `cuteDataQuery` content type in your Contentful space which is comprised of a key, title, query, jsonSelector and variablePrefix. See the attached screenshot below:

![contentful cuteSchedule model screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-cuteDataQuery-model.png)

### Example 1

Our example below will query the `diplomaCourse` content type and return all entries.

![contentful cuteDataQuery entry screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cuteDataQuery-entry.png)

Your `query` should be a valid [GraphQL]() query and can be validated prior to running your command using a method of your choosing.

You can validate your GraphQL query using Contentful's built-in [GraphiQL](https://app.contentful.com/spaces/tgugit5v0cu5/apps/list?app=graphiql) playground which can be installed from the App sub-menu.

![contentful GraphiQL app screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-GraphiQL.png)

Alternatively, you could use a tool like [Postman's](https://www.postman.com/) GraphQL request feature.

![contentful GraphiQL app screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/postman-GraphQL.png)

### Example 2

Our second example shows a slightly more complicated configuration where we query a content type which is an aggregate of the `branchLocation` and `diplomaCourse` content types. Pay specific attention to the GraphQL syntax used to include the additional data objects.

The `where` clause of the GraphQL query is empty and as a result all entries from the set will be returned.

![contentful cuteDataQuery entry screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cuteDataQuery-entry2.png)

Testing the query in Contentful's GraphiQL app yields the expected result:

![contentful GraphiQL app screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/contentful-GraphiQL2.png)

[Back to Index](#table-of-content)

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