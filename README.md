# Examples

Contentful Upload Tool

```
cut help

cut auth

cut logout

cut version

cut info

cut list | l
    --content-type a | -ct
    --skip n
    --page m

cut gen-types | gt
    --content-types a,b,c | -ct
    --language typescript,dotnet | -l

cut download | dl
    --content-types a,b,c | -ct
    --format json,xls,tsv,csv,yaml | -f
    --path /cut/output | -p
    --what-if

cut upload | ul
    --content-types a,b,c | -ct
    --format json,xls,tsv,csv,yaml | -f
    --path /cut/output | -p
    --what-if

cut excel | s
    --content-types a,b,s

```