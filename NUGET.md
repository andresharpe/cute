[![Nuget][version-shield]][version-url][![contributors][contributors-shield]][contributors-url][![issues][issues-shield]][issues-url][![stars][stars-shield]][stars-url][![build][build-shield]][build-url][![forks][forks-shield]][forks-url]

![cute overview diagram](https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/nuget-cute-overview-diagram.png)

### About

***cute*** is a cross-platform CLI tool that brings several advanced features and capabilities including bulk operations, web server mode, AI generation and language translation to working with your content hosted on [Contentful](https://www.contentful.com).

### Key Features

- *Download*, modify and *upload* data to your Contentful space in popular formats like Comma delimited files (CSV), Tab delimited files (TSV), MS-Excel workbooks (XLSX), Javascript Object Notation (JSON) and YAML.
- Perform *Bulk operations* on your content with support for publish/unpublish, edit, search & replace and delete actions.
- Input data can be sourced and synced from many external sources including flat files, databases, webAPIs, your Contentful space and other popular sources like [WikiData](https://www.wikidata.org/).
- *Generate content* from scratch using [OpenAI's](https://openai.com/api/) GPT and reasoning models with comprehensive prompt configuration support.
- Support for *content translation* using popular AI translation services like [Google Translation UI](https://cloud.google.com/translate), [Azure AI Translator](https://azure.microsoft.com/en-us/products/ai-services/ai-translator), [DeepL](https://www.deepl.com/) and [OpenAI](https://openai.com/).
- Deploy ***cute*** as a *Web Server* in `scheduler` or `webhooks` mode with [OpenTelemetry](https://opentelemetry.io/) logging and a service terminal to reflect health, configuration and scheduled tasks.
- Support for *structural subtyping* through the `typegen` command option which exports TypeScript (TS) interface declarations. This feature is especially useful to keep your JavaScript or .NET projects in sync with your content types.
- Interact with ***Douglas***, cute's very own AI assistant that will answer questions about your content, or even help formulate queries to interact with your content.

### Installation

Start by ensuring you have the required version of the [.NET SDK installed](https://dotnet.microsoft.com/en-us/download) by confirming what version ***cute*** is currently running on [here](https://github.com/andresharpe/cute/blob/master/source/Cute/Cute.csproj).

Install the ***cute*** CLI tool by running the command listed below:
```powershell
dotnet tool install --global cute
```
Confirm your installation typing `cute` at the command prompt and you should see the installed version and help displayed.

### Full Documentation

Head over to the official ***cute*** [project documentation](https://github.com/andresharpe/cute/blob/master/README.md) to get started! 🚀

### Release Notes and Change Log

Release notes [can be found on GitHub](https://github.com/andresharpe/cute/releases).

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