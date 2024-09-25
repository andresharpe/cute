## cute login

Log in to Contentful. Run this first.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| -n, --openai-deployment-name <NAME> | No | The Azure OpenAI deployment name. |
| -a, --openai-endpoint <ENDPOINT> | No | The Azure OpenAI endpoint. |
| -k, --openai-token <TOKEN> | No | The Azure OpenAI Api key. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute logout

Log out of contentful.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| -n, --openai-deployment-name <NAME> | No | The Azure OpenAI deployment name. |
| -a, --openai-endpoint <ENDPOINT> | No | The Azure OpenAI endpoint. |
| -k, --openai-token <TOKEN> | No | The Azure OpenAI Api key. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute info

Display information about a Contentfult space.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content

Manage content entries in bulk.

## cute content download

Download Contentful entries to a local csv/tsv/yaml/json/excel file.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -c, --content-type-id <ID> | No | The Contentful content type id. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| -f, --format <FORMAT> | No | The output format for the download operation (Excel/Csv/Tsv/Json/Yaml) |
| -l, --locale <CODE> | No | The locale code (eg. 'en') to apply the command to. Default is all. |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| -p, --path <PATH> | No | The output path and filename for the download operation |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content upload

Upload and sync Contentful entries from a local csv/tsv/yaml/json/excel file.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -a, --apply | No | Apply and publish all the calculated changes. The default behaviour is to only list the detected changes. |
| -c, --content-type-id <ID> | No | The Contentful content type id. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| -f, --format <FORMAT> | No | The format of the file specified in '--path' (Excel/Csv/Tsv/Json/Yaml) |
| -l, --locale <CODE> | No | The locale code (eg. 'en') to apply the command to. Default is all. |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -m, --match-field <NAME> | No | The optional name of the field to match in addition to the entry id. |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| -p, --path <PATH> | No | The local path to the file containg the data to sync |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content edit

Edit Contentful entries in bulk with an optional filter.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -a, --apply | No | Apply and publish all the required edits. The default behaviour is to only list the detected changes. |
| -c, --content-type-id <ID> | No | The Contentful content type id. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| -f, --field | No | The field to update. |
| --force | No | Specifies whether warning prompts should be bypassed |
| -l, --locale <CODE> | No | The locale code (eg. 'en') to apply the command to. |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -r, --replace | No | The value to update it with. Can contain an expression. |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content replace

Find and Replace values in Contentful entries in bulk with an optional filter.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -a, --apply | No | Apply and publish all the required edits. The default behaviour is to only list the detected changes. |
| -c, --content-type-id <ID> | No | The Contentful content type id. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| -f, --field | No | The field to update. |
| -i, --find | No | The text to find. |
| --force | No | Specifies whether warning prompts should be bypassed |
| -l, --locale <CODE> | No | The locale code (eg. 'en') to apply the command to. |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -r, --replace | No | The value to update it with. Can contain an expression. |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content publish

Bulk publish all unpublished Contentful entries.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -c, --content-type-id <ID> | No | The Contentful content type id. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content unpublish

Unpublish all published Contentful entries.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -c, --content-type-id <ID> | No | The Contentful content type id. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content delete

Unpublish and delete all Contentful entries.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -c, --content-type-id <ID> | No | The Contentful content type id. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content sync-api

Synchromise data to Contentful from an API.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -a, --apply | No | Apply and publish all the required edits. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| -k, --key | No | The key of the cuteContentSyncApi entry. |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| -u, --use-filecache | No | Whether or not to cache responses to a local file cache for subsequent calls. |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content seed-geo

Synchromise data to Contentful from an API.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -c, --content-type-prefix | No | The id of the content type containing location data. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| -h, --huge-population | No | The city or town minimum population for large cities |
| -i, --input-file | No | The path to the input file. |
| -l, --large-kilometer-radius | No | The distance in kilometers for large city to nearest location |
| -n, --large-population | No | The city or town minimum population for large cities |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| -o, --output-folder | No | The output folder. |
| -p, --password | No | The password to protect the Zip file with |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -m, --small-kilometer-radius | No | The distance in kilometers for small city to nearest location |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| -u, --upload | No | Uploads the csv file to Contentful. |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |
| -z, --zip | No | Output a zip file instead of a csv. Can be password protected with '--password'. |

## cute content sync-db

Synchronize data to Contentful from a database.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content generate

Generate content using a Large Language Model (LLM).

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -a, --apply | No | Apply and publish all the required edits. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| -k, --key | No | The key of the 'cuteContentGenerate' entry. |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| -o, --operation | No | Specify the generation operation to perform. (GenerateSingle, GenerateParallel, GenerateBatch or ListBatches) |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content generate-test

Test generation of content using a Large Language Model (LLM).

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -o, --comparison-operation | No | The comparison operator to apply to the field. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -m, --deployment-models | No | The deployment models to test. |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| -f, --field-id | No | The field id to filter on. |
| -v, --field-value | No | The field value to filter on. |
| --force | No | Specifies whether warning prompts should be bypassed |
| -k, --key | No | The key of the 'cuteContentGenerate' entry. |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content translate

Translate content using an LLM or Translation Service.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content testdata

Generate test data.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| -n, --number | No | The number of user entries to generate. (default=1000). |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute content join

Join multiple content types to a destination content type.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -i, --entry-id | No | Id of source 2 entry to join content for. |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| -k, --key | No | The id of the Contentful join entry to generate content for. |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute type

Manage Contentful content types (models).

## cute type scaffold

Automatically scaffold Typescript or c# classes from Contentful.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -c, --content-type | No | Specifies the content type to generate types for. Default is all. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| -l, --language | No | The language to generate types for (TypeScript/CSharp/Excel). |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -n, --namespace | No | The optional namespace for the generated type. |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| -o, --output | No | The local path to output the generated types to. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute type diff

Compare content types across two environments and view with VS Code.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -c, --content-type-id <ID> | No | Specifies the content type id to generate types for. Default is all. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --source-environment-id | No | Specifies the source environment id to do comparison against |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute type clone

Clone a content type and its entries between environments.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -c, --content-type-id <ID> | No | Specifies the content type id to generate types for. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -b, --entries-per-batch | No | Number of entries processed in parallel. |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -p, --publish | No | Whether to publish the created content or not. Useful if no circular references exist. |
| --source-environment-id | No | Specifies the source environment id. |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute type rename

Rename a content type including all references to it.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -a, --apply-naming-convention | No | The id to rename the content type to. |
| -c, --content-type | No | Specifies the content type to generate types for. Default is all. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -n, --new-id | No | The id to rename the content type to. |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -p, --publish | No | Whether to publish the created content or not. Useful if no circular references exist. |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute type delete

Delete a content type and its entries.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| -c, --content-type-id <ID> | No | Specifies the content type id to be deleted. |
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute app

Generate a website or app from Contentful.

## cute app generate

Generate an app or website based on configuration in Contentful.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute eval

Tools to evaluate the quality the site and of LLM and translation output.

## cute eval content-generator

Use deepeval to measure the quality of content generation.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute eval content-translator

Measure the quality of translation engine output.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute eval naming

Check and remediate violations of site naming conventions.

### Parameters

| Option | Required | Description |
|--------|----------|-------------|
| --delivery-token <TOKEN> | No | Your Contentful Content Delivery API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -e, --environment-id <ID> | No | The Contentful environment identifier. See https://www.contentful.com/developers/docs/concepts/multiple-environments/ |
| --force | No | Specifies whether warning prompts should be bypassed |
| --log-output | No | Outputs logs to the console instead of the standard messages. |
| --management-token <TOKEN> | No | Your Contentful management API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| --no-banner | No | Do not display the startup banner or the copyright message. |
| --preview-token <TOKEN> | No | Your Contentful Content Preview API token. See https://www.contentful.com/developers/docs/references/authentication/ |
| -s, --space-id <ID> | No | The Contentful space identifier. See https://www.contentful.com/help/spaces-and-organizations/ |
| --verbosity <LEVEL> | No | Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic. |

## cute version

Display the current version of the CLI.

