using Cute.Lib.Exceptions;
using GraphQLParser;
using GraphQLParser.AST;
using GraphQLParser.Visitors;
using System.Text;

namespace Cute.Lib.Contentful.GraphQL;

internal class GraphQLValidator
{
    public static string EnsureFieldExistsOrAdd(string query, string field)
    {
        var document = Parser.Parse(query);

        var userField = FindSelectionSet(document, "Collection", "items")
            ?? throw new CliException("The query does not contain a 'Collection' field with an 'items' field.");

        if (HasField(userField, field))
        {
            return query;
        }

        AddField(userField, field);

        return Serialize(document);
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