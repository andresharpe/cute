# Contentful Upload Tool

<div style="text-align:center">

![image](docs/images/cut.png)

</div>

## Introduction 

[Contentful](https://www.contentful.com/) is a headless content management system (CMS) that allows teams to store, manage and retrieve content for websites and apps.

***cut*** is a stand-alone  cross-platform command line interface (CLI) that allows bulk downloads, editing and uploads to and from a Contentful space and supports the following :-

- **CSV** - Contentful -> Comma delimeted files -> Contentful
- **TSV** - Contentful -> Tab delimeted files -> Contentful
- **Excel** - Contentful -> Excel xlsx workbook -> Contentful
- **Json** - Contentful -> Json -> Contentful
- **Yaml** - Contentful -> Yaml -> Contentful

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

Install the ***cut*** cli by typing.
```
dotnet tool install -g cut
```

### To test whether the installation worked
Simply type
```
cut
```
This will display the 
cut help. You are ready to go! 🚀

## Getting Help

```
cut --help
```
![cut help screenshot](docs/images/help.png)

## Logging into Contentful
```
cut auth
```

![cut auth screenshot](docs/images/auth.png)

## Display space summary
``` 
cut info
```
![cut info screenshot](docs/images/info.png)

## Downloading data
The default format is 'excel' so the following is equivelent.
```
cut download --content-type <contentType> 

cut download --content-type <contentType> --format excel
```
For comma seperated values:
```
cut download --content-type <contentType> --format csv
```
For tab seperated values:
```
cut download --content-type <contentType> --format tsv
```
For json output:
```
cut download --content-type <contentType> --format json
```
For downloading to Yaml:
```
cut download --content-type <contentType> --format yaml
```
![cut download screenshot](docs/images/download.png)

## Uploading/synchronizing data

***This feature will be coming soon...***

## For generating Javascript or Dotnet types

***This feature will be coming soon...***
