# Contentful Update Tool & Extractor

![image](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute-logo.png)

## Introduction 

***cute*** is a stand-alone  cross-platform command line interface (CLI) that allows bulk downloads, editing, AI generation and uploads to and from a Contentful space and supports the following :-

- **CSV** - Contentful -> Comma delimeted files -> Contentful
- **TSV** - Contentful -> Tab delimeted files -> Contentful
- **Excel** - Contentful -> Excel xlsx workbook -> Contentful
- **Json** - Contentful -> Json -> Contentful
- **Yaml** - Contentful -> Yaml -> Contentful

You can also generate types for JavaScript or dotnet to keep your project in sync with your content space.

[Contentful](https://www.contentful.com/) is a headless content management system (CMS) that allows teams to store, manage and retrieve content for websites and apps.

## Installation

### Firstly, make sure you have the Dotnet SDK 8.0 package installed.

For windows (cmd or powershell):
```
winget install Microsoft.DotNet.SDK.8
```

Or, on linux and iOS
``` 
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

### Then:
On windows you may have to close and re-open the command line propt (or Windows Terminal).

Install the ***cute*** cli by typing.
```
dotnet tool install -g cute
```

### To test whether the installation worked
Simply type
```
cute
```
This will display the 
cute help. You are ready to go! 🚀

## Getting Help

```
cute --help
```
![cute help screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/help.png)

## Logging into Contentful
```
cute login
```

![cut auth screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/login.png)

## Display space summary
``` 
cute info
```
![cut info screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/info.png)

## Downloading data
The default format is 'excel' so the following is equivelent.
```
cute download --content-type <contentType> 

cute download --content-type <contentType> --format excel
```
For comma seperated values:
```
cute download --content-type <contentType> --format csv
```
For tab seperated values:
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

## Uploading/synchronizing data

You can upload content from a local file to contentful. The local file can be a previously downloaded and updated excel, sdv, tsv, json or yaml file.

![cute upload progress screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/upload-progress.png)

Changes are only applied to Contentful if `--apply` is specified. By default no changes will be applied so it works a bit like a "what-if" powershel switch without `--apply`.

![cute upload screenshot](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/upload.png)

Typing `cute upload --help` will sow the full usage and options.

```
USAGE:
    cute upload [OPTIONS]

OPTIONS:
    -h, --help            Prints help information
    -c, --content-type    Specifies the content type to download data for
    -p, --path            The local path to the file containg the data to sync
    -f, --format          The format of the file specified in '--path' (Excel/Csv/Tsv/Json/Yaml)
    -a, --apply           Apply and publish all the calculated changes. The default behaviour is to only list the detected changes
```

## For generating strong Javascript or Dotnet types

You can generate stronly typed classes for both c# and TypeScript usinh `cute`.

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

## Content generation using OpenAI

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


## New: Proposed Command Structure for v2.0

`cute` has rapidly grown in features and needs a little refactoring to make the commands more consistent, memorable, intuative and future-proof.

Here is the new proposed command structure for v2.0:-

### Main Commands

#### Verb Commands
```
cute login ...
cute logout ...
```
#### Noun Commands
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

Except for login/logout (verb commands) that is used infrequently (mainly once), every command (of the noun commands) start with a unique letter _to make shell autocomplete work for us_. 

|Command    |Options    |Comments|
|-----------|-----------|--------|
|**cute**||**The main command line interface (CLI) command. Shows help by default.**
||*COMMON OPTIONS*|These options and switches are supported with most commands (where applicable)
||--log-output | outputs logs to the console rather than the pretty console output|
||--verbosity \<level>|q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]|
||--no-banner|Do not display the startup banner, space info, parameters or the copyright message.|
||--space-id \<space-id>|Specify the space to operate on|
||--environment-id \<environment-id>|Specify the environment to operate onm|
||--profile \<name>|Use a specific profile by name|
||--preview|Specify whether unpublished entries will be icluded in the download|
||--apply|By default all create/update/delete commands will only show changes that could change Contentful data (i.e. "what-if" or "safe" mode). This switch applies the changes! (be careful)|
||--publish|By default all applied create/update/delete commands will only be hanged and not published. This switch will publish after a change. 
||--help|Shows help|
|**cute login**||**Logs into contentful. Shows interactive login screen by default and creates a default profile.**|
||--management-token \<token>|Supply the Contentful management token to use|
||--content-delivery-token \<token>||Specify the Contentful delivery API key|
||--content-preview-token \<token>|Specify the Contentful preview API key|
||--environment-id \<environment-id>|Specify the default environment to log into|
||--space-id \<space-id>|Specify the default space to log into|
||--help|Shows help|
|**cute logout**||Logs out of Contentful 
||--purge|Remove all keys, profiles, and caches from the device|
|**cute info**||**Displays information about cute, the default space and environment, user info, and content and locale summary.**
|**cute profile**||**Interactive add/edit/delete of configurations**|
|cute profile list||List of profiles (spaces, API keys and environments)|
|cute profile add||Interactively add a profile|
||--profile \<name>|The name of the profile|                  
||--management-token \<token>|Supply the Contentful management token to use|
||--content-delivery-token \<token>||Specify the Contentful delivery API key|
||--content-preview-token \<token>|Specify the Contentful preview API key|
||--space-id \<space-id>|The active space for the profile|
||--environment-id \<environment-id>|The active environment for the profile|
||--preview|Whether to include preview (unpublished) entries|
||--default|Set this profile as the default|
|cute profile remove||Interactively removes a profile.|
||--profile \<name>||Name of the profile to remove|
||--all|Remove all configurations|
|**cute content**||**Manage your contentful content in bulk. The command without options shows help.**|
|cute content download||Downloads contentful entries interactively.|
||--content-type-id \<type-id>|Specify the content type to download|
||--output-path \<path>|Specify where to save the downloaded content
||--output-format \<markdown \| html \| csv \| tsv \| yaml \| json \| excel>|Specify the format for downloaded content.|
|cute content upload||Interactively uploads and merges changes in a file to Contentful. Use --apply to apply changes. and --publish to publish applied changes.|
||--content-type-id \<type-id>|Specify the content type to upload to|
||--file-path \<path>|Path to the content file to upload.|
||--file-format \<csv \| tsv \| yaml \| json \| excel>|The format of the specified file.|
||--apply|Applies detected changes to Contentful|
||--publish|Publish entries after changes are applied| 
|cute content edit||Bulk edit of fields on a content type| 
||--content-type-id \<type-id>|Specify the content type to edit|
||--field \<field-name>|Field to be edited. You can specify multiple fields and values|
||--value \<new-value-expression>|New value for the field. This can be an expression referencing other entry values. Multiple fields and values can be specified.
||--filter \<query-expression>|Condition to select entries for editing|
|cute content publish||Interactively publish all unpublished entries in bulk|
||--content-type-id \<type-id>|Specify the content type entries to publish|
||--filter \<query-expression>|Condition to select entries to  publish|
||--no-confirm|Force publish without confirmation|
|cute content unpublish||Interactively unpublish all entries in bulk|
||--content-type-id \<type-id>|Specify the content type to unpublish|
||--filter \<query-expression>|Condition to select entries to  unpublish|
||--no-confirm|Force unpublish without confirmation|
|cute content delete||Deletes all entries of a content type in bulk. Published entries will be unpublished first, then all entries will be deleted.|
||--content-type-id \<type-id>|Specify the content type to delete.|
||--filter \<query-expression>|Condition to select entries to  delete|
||--no-confirm|Force delete without confirmation|
|cute content sync-api||Add/Updates Contentful entries from API data. Parameters are read from 'cuteContentSyncApi' content type. Cute will automatically create the content type if it doesn't exist.|
||--key <key>|The key of the entry in 'cuteContentSyncApi' to sync.|
||--apply|Applies detected changes to Contentful|
||--publish|Publish entries after changes are applied| 
|cute content sync-db||Add/Updates Contentful entries from a database. Parameters are read from 'cuteContentSyncDatabase' content type. Cute will automatically create the content type if it doesn't exist.|
||--key <key>|The key of the entry in 'cuteContentSyncDatabase' to sync.|
||--apply|Applies detected changes to Contentful|
||--publish|Publish entries after changes are applied| 
|cute content generate||Updates Contentful entries via content generated by an A.I. or Large Language Model (LLM). Parameters are read from 'cuteContentGenerate' content type. Cute will automatically create the content type if it doesn't exist.|
||--key <key>|The key of the entry in 'cuteContentGenerate' to generate content with.|
||--apply|Applies detected changes to Contentful|
||--publish|Publish entries after changes are applied| 
|cute content translate||Translates Contentful entries via an A.I. or Large Language Model (LLM). Parameters are read from 'cuteContentTranslate' content type. Cute will automatically create the content type if it doesn't exist.|
||--key <key>|The key of the entry in 'cuteContentTranslate' with translation instructions.|
||--apply|Applies detected changes to Contentful|
||--publish|Publish entries after changes are applied| 
|**cute type**||**Operate with Contentful content types. The command without options will show help.**|
|cute type info||Shows information about a content type and its entries.|
||--content-type-id \<type-id>|Specify the content type to display information for. The default is '*' for all.|
||--output-path \<path>|Optionally specify where to save info on the downloaded content type
||--output-format \<markdown \| html \| csv \| tsv \| yaml \| json \| excel>|Specify the format for content info.|
|cute type scaffold||Generates strongly typed classes for all or a specified content type.|
||--language \<csharp \| typescript>|Specify the programming language for the scaffold|
||--content-type-id \<type-id>|Specify the content type to scaffold from|
||--output-path \<path>|Specify the output path for the scaffolded class files|
||--namespace \<namespace>|Specify the namespace  path for the scaffolded class files (specifically 'csharp').|
|cute type diff||Compares all content types across two environments. Requires VS Code in the path to perform and navigate the differences|
||--source-environment-id \<source-env>|Specify the source environment for the diff|
||--target-environment-id \<target-env>|Specify the target environment for the diff. If not specified uses the default or profile environment|
|cute type clone||Clones a content type from one environment to the other (or default)|
||--source-environment-id \<source-env>|Specify the source environment for the clone|
||--target-environment-id \<target-env>|Specify the target environment for the clone. If not specified uses the default or profile environment|
||--content-type-id \<type-id>|Specify the content type to clone|
|cute type rename||Renames a content type id and all references from other content types.|
||--content-type-id \<type-id>|Specify the content type to rename|
||--new-id \<new-id>|New id for the content type
|cute type join||Performs a join of two content types to a third. Parameters are read from 'cuteTypeJoin' content type. Cute will automatically create the content type if it doesn't exist.|
||--key <key>|The key of the entry in 'cuteTypeJoin' with translation instructions.|
|**cute app**||App and website generation using data queries and page/component definitions to generate static and dynamic websites.
|cute app generate||Generates a static website from Contentful types in the 'Ui' namespace. The command will create the necessary content types if they don't exist.|
||--platform-id \<id>|The id in the 'uiPlatform' content type to generate the site for|
|**cute report**||**Analyze and report on Contentful design, structures and AI/LLM output**|
|cute report content-generator||
|cute report content-translator||
|cute report naming-conventions||Analyse the naming convention of content type id's, fields and refrences. Optionally apply suggestions where possible.
|**cute server**||**Runs cute as a service with health checks, web interface and OpenTelemetry logging. Useful for cloud deployment.**|
|cute server schedule||Starts a server that executes all content sync commands on a schedule, similar to cron|
||--port \<port-number>|Specify the port number for the http interface to the web server|
|cute server webhooks||Services Contentful webhooks to automatically invoke the appropriate generation and translation services.|
||- --port \<port-number>|Specify the port number for the web server!
|cute version||Shows the cuttent version of cute without a banner|