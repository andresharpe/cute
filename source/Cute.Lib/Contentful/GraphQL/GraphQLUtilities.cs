﻿using Cute.Lib.Exceptions;
using GraphQLParser;
using GraphQLParser.AST;
using GraphQLParser.Visitors;
using System.Text;

namespace Cute.Lib.Contentful.GraphQL;

public class GraphQLUtilities
{
    public static string EnsureFieldExistsOrAdd(string query, string field, string? searchKey = null)
    {
        var document = Parser.Parse(query);

        if(!string.IsNullOrEmpty(searchKey))
        {
            AddKeySearch(document, "Collection", searchKey);
        }

        var userField = FindSelectionSet(document, "Collection", "items")
            ?? throw new CliException("The query does not contain a 'Collection' field with an 'items' field.");

        if (HasField(userField, field))
        {
            return query;
        }

        AddField(userField, field);

        return Serialize(document);
    }

    public static string GetContentTypeId(string query)
    {
        var document = Parser.Parse(query);

        var contenTypeId = FindField(document, "Collection")
            ?? throw new CliException("The query does not contain a 'Collection' field.");

        return contenTypeId.Name.StringValue[..^10];
    }

    private static GraphQLField? FindField(GraphQLDocument document, string parentFieldPostFix)
    {
        foreach (var definition in document.Definitions)
        {
            if (definition is GraphQLOperationDefinition operation)
            {
                foreach (var selection in operation.SelectionSet.Selections)
                {
                    if (selection is GraphQLField parent && parent.Name.StringValue.EndsWith(parentFieldPostFix))
                    {
                        return parent;
                    }
                }
            }
        }
        return null;
    }

    private static GraphQLSelectionSet? FindSelectionSet(GraphQLDocument document, string parentFieldPostFix, string childField)
    {
        foreach (var definition in document.Definitions)
        {
            if (definition is GraphQLOperationDefinition operation)
            {
                foreach (var selection in operation.SelectionSet.Selections)
                {
                    if (selection is GraphQLField parent && parent.Name.StringValue.EndsWith(parentFieldPostFix))
                    {
                        foreach (var subSelection in parent.SelectionSet!.Selections)
                        {
                            if (subSelection is GraphQLField child && child.Name.StringValue == childField)
                            {
                                // Return the selection set for the child field (e.g., 'items')
                                return child.SelectionSet;
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    private static void AddKeySearch(GraphQLDocument document, string parentFieldPostFix, string searchKey)
    {
        foreach (var definition in document.Definitions)
        {
            if (definition is GraphQLOperationDefinition operation)
            {
                foreach (var selection in operation.SelectionSet.Selections)
                {
                    if (selection is GraphQLField parent && parent.Name.StringValue.EndsWith(parentFieldPostFix))
                    {
                        bool hasWhere = false;
                        foreach (var argument in parent.Arguments!.Items)
                        {
                            if (argument is GraphQLArgument arg && arg.Name.StringValue == "where")
                            {
                                if(arg.Value is GraphQLObjectValue values)
                                {
                                    values.Fields!.Add(new GraphQLObjectField(new GraphQLName("key"), new GraphQLStringValue(searchKey)));
                                    hasWhere = true;
                                    break;
                                }
                            }
                        }

                        if (!hasWhere)
                        {
                            parent.Arguments.Items.Add(new GraphQLArgument(new GraphQLName("where"), new GraphQLObjectValue
                            {
                                Fields = new List<GraphQLObjectField>
                                {
                                    new GraphQLObjectField(new GraphQLName("key"), new GraphQLStringValue(searchKey))
                                }
                            }));
                        }
                    }
                }
            }
        }
    }

    // Function to check if a selection set has a specific field
    private static bool HasField(GraphQLSelectionSet selectionSet, string fieldName)
    {
        foreach (var selection in selectionSet.Selections)
        {
            if (selection is GraphQLField field && field.Name.StringValue == fieldName)
            {
                return true;
            }
        }
        return false;
    }

    // Function to add a field to a selection set
    private static void AddField(GraphQLSelectionSet selectionSet, string fieldName)
    {
        var newField = new GraphQLField(new GraphQLName(fieldName));
        selectionSet.Selections.Add(newField);
    }

    private static string Serialize(GraphQLDocument document)
    {
        var sb = new StringBuilder();

        new SDLPrinter().Print(document, sb);

        return sb.ToString();
    }
}