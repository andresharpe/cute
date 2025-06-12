# Cute CLI Usage

## cute login

Run this command first to configure your Contentful profile along with AI and Translation Services.

### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --openai-deployment-name, -n <NAME> | The Azure OpenAI deployment name. |
| --openai-endpoint, -a <ENDPOINT> | The Azure OpenAI endpoint. |
| --openai-token, -k <TOKEN> | The Azure OpenAI API key. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute logout

Log out of Contentful and clear your session credentials.

### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --purge | Specifies the content type to bulk edit. |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute info

Display information about a Contentful space.

### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute chat

Make the robots do the work! Interact with your space using cute's AI assistant.

### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --douglas | Summons Douglas. He knows most things about your Contentful space. |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --frequency-penalty, -f | Reduces repetition of frequently used phrases in bot responses. |
| --key, -k | Optional key for fetching a specific 'cuteContentGenerate' entry. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --max-tokens, -m | Maximum number of tokens (words) allowed in the bot's responses. |
| --memory-length | The total number of user and agent messages to keep in memory and send with new prompt. |
| --no-banner | Do not display the startup banner or the copyright message. |
| --presence-penalty | Discourages reusing phrases already present in the conversation. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --system-prompt, -p | System prompt to initialize the bot's starting context. |
| --temperature, -t | Controls randomness: higher values generate more creative responses. |
| --topP | TopP controls diversity by limiting the token pool for bot responses. |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content

Manage your content entries in Contentful using bulk operations.

### cute content download

Download Contentful entries to a local CSV/TSV/YAML/JSON/Excel file.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --format, -f <FORMAT> | The output format for the download operation (Excel/CSV/TSV/JSON/YAML) |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. Default is all. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --path, -p <PATH> | The output path and filename for the download operation |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content upload

Upload and sync Contentful entries from a local CSV/TSV/YAML/JSON/Excel file.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Apply and publish all the calculated changes. The default behaviour is to only list the detected changes. |
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --format, -f <FORMAT> | The format of the file specified in '--path' (Excel/CSV/TSV/JSON/YAML) |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. Default is all. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --match-field, -m <NAME> | The optional name of the field to match in addition to the entry ID. |
| --no-banner | Do not display the startup banner or the copyright message. |
| --path, -p <PATH> | The local path to the file containing the data to sync |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content edit

Edit Contentful entries in bulk with an optional filter.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Apply and publish all the required edits. The default behaviour is to only list the detected changes. |
| --content-type-id, -c <ID> | The Contentful content type ID. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --field, -f | The field to update. |
| --force | Specifies whether warning prompts should be bypassed |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
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
| --content-type-id, -c <ID> | The Contentful content type ID. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --field, -f | The field to update. |
| --find, -i | The text to find. |
| --force | Specifies whether warning prompts should be bypassed |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
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
| --content-type-id, -c <ID> | The Contentful content type ID. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content unpublish

Unpublish all published Contentful entries.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | The Contentful content type ID. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content delete

Unpublish and delete all Contentful entries.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | The Contentful content type ID. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content sync-api

Synchronise data to Contentful from an API.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Apply and publish all the required edits. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The key of the cuteContentSyncApi entry. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --use-filecache, -u | Whether or not to cache responses to a local file cache for subsequent calls. |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content seed-geo

Seed geographical test data to start your project.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Uploads and applies the changes to Contentful. |
| --content-type-prefix, -c | The id of the content type containing location data. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --google-places, -g | Update google places Id where missing? |
| --huge-population, -u | The minimum population for a city or town to be considered huge in scale |
| --input-file, -i | The path to the input file or the URL of a password protected ZIP containing CSV data. |
| --large-kilometer-radius, -l | The distance in kilometers for large city to nearest location |
| --large-population, -n | The minimum population for a city or town to be considered large in scale |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --password, -p | The password of the online zip file containing CSV data. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --small-kilometer-radius, -m | The distance in kilometers for small city to nearest location |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content sync-db

Synchronize data to Contentful from a database.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
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
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The key of the 'cuteContentGenerate' entry. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --operation, -o | Specify the generation operation to perform. (GenerateSingle, GenerateParallel, GenerateBatch or ListBatches) |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --target-entry-key | The key of the target entry to generate content for. |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content generate-test

Test generation of content using a Large Language Model (LLM).

#### Parameters

| Option | Description |
|--------|-------------|
| --comparison-operation, -o | The comparison operator to apply to the field. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --deployment-models, -m | The deployment models to test. |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --field-id, -f | The field ID to filter on. |
| --field-value, -v | The field value to filter on. |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The key of the 'cuteContentGenerate' entry. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content translate

Translate content using an LLM or Translation Service.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | The Contentful content type id. |
| --custom-model <CODE> | Specifies whether custom model translation should be used |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --field, -f <CODE> | List of fields to translate. |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The key of the entry to translate. |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. Default is all. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --use-glossary <CODE> | Specifies whether custom glossary (cuteTranslationGlossary) should be used |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content testdata

Generate test data.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
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
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --entry-id, -i | Id of source 2 entry to join content for. |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The key of the 'cuteContentJoin' entry. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content clear-fields

Clear localized fields for content type

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | The Contentful content type id. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --field, -f <CODE> | List of fields to clear. |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | The key of the entry to clear. |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. Default is all. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute content set-default

Set default value for Contentful entries in bulk with an optional filter.

#### Parameters

| Option | Description |
|--------|-------------|
| --apply, -a | Apply and publish all the required edits. The default behaviour is to only list the detected changes. |
| --content-type-id, -c <ID> | The Contentful content type ID. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --field, -f | The fields to update. |
| --filter-field | The field to update. |
| --filter-field-value | The value to update it with. Can contain an expression. |
| --force | Specifies whether warning prompts should be bypassed |
| --locale, -l <CODE> | The locale code (eg. 'en') to apply the command to. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --replace, -r | The values to update it with. Can contain an expression. |
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
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --language, -l | The language to generate types for (TypeScript/CSharp/Excel). |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
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
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
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
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --entries-per-batch, -b | Number of entries processed in parallel. |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
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
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --new-id, -n | The id to rename the content type to. |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --publish, -p | Whether to publish the created content or not. Useful if no circular references exist. |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute type delete

Delete a content type along with its entries.

#### Parameters

| Option | Description |
|--------|-------------|
| --content-type-id, -c <ID> | Specifies the content type id to be deleted. |
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute app

Generate a website or app from your content space in Contentful.

### cute app generate

Generate an app or website based on user configuration settings in Contentful.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute eval

Tools to evaluate the quality of generated content using the LLM and translation engine.

### cute eval content-generator

Use DeepEval to measure the quality of content generation.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute eval content-translator

Measure the quality of the translation engine output.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute eval naming

Check and remediate violations of site naming conventions.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute server

Run cute in server mode supporting scheduled tasks and webhooks.

### cute server scheduler

Schedule and run cuteContentSyncApi entries.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --key, -k | CuteSchedule key. |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --port, -p | The port to listen on |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

### cute server webhooks

Process callbacks from contentful.

#### Parameters

| Option | Description |
|--------|-------------|
| --delivery-token <TOKEN> | Your Contentful Content Delivery API (CDA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --environment-id, -e <ID> | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | Specifies whether warning prompts should be bypassed |
| --log-output | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | Your Contentful Management API (CMA) token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | Do not display the startup banner or the copyright message. |
| --port, -p | The port to listen on |
| --preview-token <TOKEN> | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --space-id, -s <ID> | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute version

Display the current installed version of the cute CLI.

