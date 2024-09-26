[![Nuget][version-shield]][version-url][![contributors][contributors-shield]][contributors-url][![issues][issues-shield]][issues-url][![stars][stars-shield]][stars-url][![build][build-shield]][build-url][![forks][forks-shield]][forks-url]

<br /><div align="center"><a href="https://github.com/andresharpe/cute"><img src="https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-logo.png" alt="Logo" width="500"></a></div>

<p align="center">A Contentful Update Tool & Extractor</p>

<div align="center"><a href="https://github.com/andresharpe/cute/tree/master/source">View the Source Code</a> · <a href="https://www.nuget.org/packages/cute">Download @ Nuget</a></div><br />

# Introduction

***cute*** is a cross-platform CLI tool that brings several advanced features and capabilities to working with your content hosted on [Contentful](https://www.contentful.com).

<br /><div align="center"><img src="https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-overview-graphic.png" alt="cute help screenshot" width="70%"><br /></div>

## Key Features

- Bulk processing capabilities lets you download, modify and upload data in most popular formats including Comma delimited files (CSV), Tab delimited files (TSV), MS-Excel workbooks (XLSX), Javascript Object Notation (JSON) and YAML.
- Input data can be sourced and synced from many external sources including flat files, databases, webAPIs or other popular sources like [WikiData](https://www.wikidata.org/).
- Content can be enriched or even generated using popular technologies like [OpenAI](https://openai.com/) and [Azure AI Translator](https://azure.microsoft.com/en-us/products/ai-services/ai-translator).
- Deploy ***cute*** as a Web Server with [OpenTelemetry](https://opentelemetry.io/) compliant logging and a service terminal to reflect health, configuration and scheduled tasks.
- Support for structural subtyping through the `typegen` command option which exports TypeScript (TS) interface declarations. This feature is especially useful to keep your JavaScript or .NET projects in sync with your content types.
- ***cute*** auto-magically "learns" your Contentful space and generates required configuration nodes to enable process automation.

## Why Contentful?

[Contentful](https://www.contentful.com) is a content infrastructure platform that lets you create, manage and distribute content to any platform.

Contentful bills itself as a Content Infrastructure Platform rather than a traditional Content Management System (CMS) that is often no more than a simple web publishing tool.

It aims to transcend traditional Content Management Systems (CMS) by structuring its technology offering around three principals:

- Firstly, by enabling the definition of a content model which is independent from the presentation layer.
- Secondly, if offers a easy-to-use UI to manage content in a collaborative manner.
- Finally, content is served in a presentation independent manner.

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
```
cute login
```

![cut auth screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/login.png)

# Display space summary
``` 
cute info
```
![cut info screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/info.png)

# Downloading data
The default format is 'excel' so the following is equivalent.
```
cute download --content-type <contentType> 

cute download --content-type <contentType> --format excel
```
For comma separated values:
```
cute download --content-type <contentType> --format csv
```
For tab separated values:
```
cute download --content-type <contentType> --format tsv
```
For json output:
```
cute download --content-type <contentType> --format json
```
For downloading to Yaml:
```
cute download --content-type <contentType> --format yaml
```
![cute download screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/download.png)

Typing `cute download --help` will list all currently available options and usage

```
USAGE:
    cute download [OPTIONS]

OPTIONS:
    -h, --help            Prints help information
    -c, --content-type    Specifies the content type to download data for
    -f, --format          The output format for the download operation (Excel/Csv/Tsv/Json/Yaml)
```

# Uploading/synchronizing data

You can upload content from a local file to contentful. The local file can be a previously downloaded and updated excel, sdv, tsv, json or yaml file.

![cute upload progress screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/upload-progress.png)

Changes are only applied to Contentful if `--apply` is specified. By default no changes will be applied so it works a bit like a "what-if" powershell switch without `--apply`.

![cute upload screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/upload.png)

Typing `cute upload --help` will sow the full usage and options.

```
USAGE:
    cute upload [OPTIONS]

OPTIONS:
    -h, --help            Prints help information
    -c, --content-type    Specifies the content type to download data for
    -p, --path            The local path to the file containing the data to sync
    -f, --format          The format of the file specified in '--path' (Excel/Csv/Tsv/Json/Yaml)
    -a, --apply           Apply and publish all the calculated changes. The default behaviour is to only list the detected changes
```

# For generating strong Javascript or Dotnet types

You can generate strongly typed classes for both c# and TypeScript using `cute`.

```
USAGE:
    cute typegen [OPTIONS]

OPTIONS:
    -h, --help            Prints help information
    -c, --content-type    Specifies the content type to generate types for. Default is all
    -o, --output          The local path to output the generated types to
    -l, --language        The language to generate types for (TypeScript/CSharp)
    -n, --namespace       The optional namespace for the generated type
```

# Content generation using OpenAI

You can generate content using OpenAI in bulk. Prompts are retrieved from your Contentful space. A typical prompt entry has an id, a system message, a prompt, points to a content type and field.  Something like :-

|Title|EntryField|
|-|-|
|title|Short text|
|SystemMessage|Long text|
|MainPrompt|Long text|
|ContentTypeId|Short text|
|ContentFieldId|Short text|

```
DESCRIPTION:
Use generative AI to help build drafts of your content.

USAGE:
    cute generate [OPTIONS]

OPTIONS:
    -h, --help                   Prints help information
    -c, --prompt-content-type    The id of the content type containing prompts. Default is 'prompts'
    -f, --prompt-field           The id of the field that contains the prompt key/title/id. Default is 'title'
    -i, --prompt-id              The title of the Contentful prompt entry to generate content from
    -l, --limit                  The total number of entries to generate content for before stopping. Default is five
    -s, --skip                   The total number of entries to skip before starting. Default is zero
```


# Command Structure for v2.0

## Main Commands

### Verb Commands
```
cute login ...
cute logout ...
```
### Noun Commands
```
cute info
cute profile ...
cute content ...
cute type ...
cute app ...
cute server...
cute report ...
cute version
```


## cute login

Log in to Contentful. Run this first.

### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --openai-deployment-name, -n <NAME> | The Azure OpenAI deployment name. |
| --openai-endpoint, -a <ENDPOINT> | The Azure OpenAI endpoint. |
| --openai-token, -k <TOKEN> | The Azure OpenAI Api key. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute logout

Log out of contentful.

### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --openai-deployment-name, -n <NAME> | The Azure OpenAI deployment name. |
| --openai-endpoint, -a <ENDPOINT> | The Azure OpenAI endpoint. |
| --openai-token, -k <TOKEN> | The Azure OpenAI Api key. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute info

Display information about a Contentfult space.

### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content

Manage content entries in bulk.

### cute content download

Download Contentful entries to a local csv/tsv/yaml/json/excel file.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --format, -f <FORMAT> | The output format for the download operation (Excel/Csv/Tsv/Json/Yaml) |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. Default is all. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --path, -p <PATH> | The output path and filename for the download operation |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content upload

Upload and sync Contentful entries from a local csv/tsv/yaml/json/excel file.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Apply and publish all the calculated changes. The default behaviour is to only list the detected changes. |
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --format, -f <FORMAT> | The format of the file specified in '--path' (Excel/Csv/Tsv/Json/Yaml) |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. Default is all. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --match-field, -m <NAME> | The optional name of the field to match in addition to the entry id. |
| --no-banner | Do not display the startup banner or the copyright message. |
| --path, -p <PATH> | The local path to the file containg the data to sync |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content edit

Edit Contentful entries in bulk with an optional filter.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Apply and publish all the required edits. The default behaviour is to only list the detected changes. |
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --field, -f | The field to update. |
| --force | Specifies whether warning prompts should be bypassed |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --replace, -r | The value to update it with. Can contain an expression. |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content replace

Find and Replace values in Contentful entries in bulk with an optional filter.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Apply and publish all the required edits. The default behaviour is to only list the detected changes. |
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --field, -f | The field to update. |
| --find, -i | The text to find. |
| --force | Specifies whether warning prompts should be bypassed |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --replace, -r | The value to update it with. Can contain an expression. |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content publish

Bulk publish all unpublished Contentful entries.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content unpublish

Unpublish all published Contentful entries.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content delete

Unpublish and delete all Contentful entries.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content sync-api

Synchromise data to Contentful from an API.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Apply and publish all the required edits. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The key of the cuteContentSyncApi entry. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --use-filecache, -u | Whether or not to cache responses to a local file cache for subsequent calls. |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content seed-geo

Synchromise data to Contentful from an API.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-prefix, -c | The id of the content type containing location data. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --huge-population, -h | The city or town minimum population for large cities |
| --input-file, -i | The path to the input file. |
| --large-kilometer-radius, -l | The distance in kilometers for large city to nearest location |
| --large-population, -n | The city or town minimum population for large cities |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --output-folder, -o | The output folder. |
| --password, -p | The password to protect the Zip file with |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --small-kilometer-radius, -m | The distance in kilometers for small city to nearest location |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --upload, -u | Uploads the csv file to Contentful. |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |
| --zip, -z | Output a zip file instead of a csv. Can be password protected with '--password'. |

### cute content sync-db

Synchronize data to Contentful from a database.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content generate

Generate content using a Large Language Model (LLM).

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Apply and publish all the required edits. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The key of the 'cuteContentGenerate' entry. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --operation, -o | Specify the generation operation to perform. (GenerateSingle, GenerateParallel, GenerateBatch or ListBatches) |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content generate-test

Test generation of content using a Large Language Model (LLM).

#### Parameters

| Option | Description |
|--------|-------------|
| --comparison-operation, -o | The comparison operator to apply to the field. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --deployment-models, -m | The deployment models to test. |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --field-id, -f | The field id to filter on. |
| --field-value, -v | The field value to filter on. |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The key of the 'cuteContentGenerate' entry. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content translate

Translate content using an LLM or Translation Service.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content testdata

Generate test data.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --number, -n | The number of user entries to generate. (default=1000). |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content join

Join multiple content types to a destination content type.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --entry-id, -i | Id of source 2 entry to join content for. |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The id of the Contentful join entry to generate content for. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute type

Manage Contentful content types (models).

### cute type scaffold

Automatically scaffold Typescript or c# classes from Contentful.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type, -c | Specifies the content type to generate types for. Default is all. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --language, -l | The language to generate types for (TypeScript/CSharp/Excel). |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --namespace, -n | The optional namespace for the generated type. |
| --no-banner | Do not display the startup banner or the copyright message. |
| --output, -o | The local path to output the generated types to. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute type diff

Compare content types across two environments and view with VS Code.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | Specifies the content type id to generate types for. Default is all. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --source-environment-id | Specifies the source environment id to do comparison against |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute type clone

Clone a content type and its entries between environments.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | Specifies the content type id to generate types for. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --entries-per-batch, -b | Number of entries processed in parallel. |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --publish, -p | Whether to publish the created content or not. Useful if no circular references exist. |
| --source-environment-id | Specifies the source environment id. |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute type rename

Rename a content type including all references to it.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply-naming-convention, -a | The id to rename the content type to. |
| --content-type, -c | Specifies the content type to generate types for. Default is all. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --new-id, -n | The id to rename the content type to. |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --publish, -p | Whether to publish the created content or not. Useful if no circular references exist. |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute type delete

Delete a content type and its entries.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | Specifies the content type id to be deleted. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute app

Generate a website or app from Contentful.

### cute app generate

Generate an app or website based on configuration in Contentful.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute eval

Tools to evaluate the quality the site and of LLM and translation output.

### cute eval content-generator

Use deepeval to measure the quality of content generation.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute eval content-translator

Measure the quality of translation engine output.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute eval naming

Check and remediate violations of site naming conventions.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute version

Display the current version of the CLI.




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